param([string]$AsepritePath)
$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
. (Join-Path $project 'tools/BuildEnvironment.ps1')
$settings = Get-AquabroBuildSettings -ProjectRoot $project -AsepritePath $AsepritePath
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('Aquabro-mask-context-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporary | Out-Null
try {
    $lua = @'
local root,output=app.params['root'],app.params['output']
local encode=assert(loadfile(root..'/tools/export_water_effects.lua'))('mask-context')
local pack=assert(loadfile(root..'/tools/export_water_effects.lua'))('mask-pack16')
local function binary(bytes)
 local text={};for i,byte in ipairs(bytes) do text[i]=string.char(byte) end;return table.concat(text)
end
local function write(name,text)
 local f=assert(io.open(output..'/'..name,'wb'));f:write(text);f:close()
end
local function store(name,count,size,compressed,checksum,bad)
 write(name..'.spec',count..' '..size..' '..#compressed..' '..checksum..' '..(bad and 1 or 0))
 write(name..'.packed',pack(compressed))
end
local function fixture(name,count,size,make)
 local bytes={};for i=1,count*size*size/8 do bytes[i]=make(i) end
 local compressed,checksum=encode(bytes,size*size/8)
 local repeated,other=encode(bytes,size*size/8)
 assert(binary(compressed)==binary(repeated) and checksum==other,'Non-deterministic context encoding')
 write(name..'.raw',binary(bytes));store(name,count,size,compressed,checksum,false)
 return compressed,checksum
end
fixture('empty',0,8,function() return 0 end)
fixture('zero-64',6,64,function() return 0 end)
fixture('one-128',3,128,function() return 255 end)
fixture('alternating-bits',2,128,function(i) return i%2==0 and 85 or 170 end)
fixture('alternating-bytes',4,64,function(i) return i%2==0 and 0 or 255 end)
fixture('frame-reset',3,64,function(i) return i<=512 and 255 or i<=1024 and 0 or 1 end)
fixture('byte-edges',7,8,function(i) return ({0,1,128,255,127,254,3,192})[(i-1)%8+1] end)
fixture('row-edges',3,16,function(i) return i%4<2 and 129 or 24 end)
fixture('diagonal',2,128,function(i) return 1 << (math.floor((i-1)/16)%8) end)
fixture('count-rescale',32,128,function(i) return i%17==0 and 255 or 0 end)
local state=123456789
local compressed,checksum=fixture('random',5,128,function() state=(state*1664525+1013904223) & 0xffffffff;return state >> 24 end)
local function copy() local b={};for i,v in ipairs(compressed) do b[i]=v end;return b end
store('bad-empty',5,128,{},checksum,true)
store('bad-truncated',5,128,{compressed[1],compressed[2]},checksum,true)
local tail=copy();tail[#tail+1]=0;tail[#tail+1]=0
store('bad-trailing',5,128,tail,checksum,true)
local flipped=copy();flipped[math.floor(#flipped/2)]=flipped[math.floor(#flipped/2)] ~ 1
store('bad-bit-flip',5,128,flipped,checksum,true)
store('bad-checksum',5,128,compressed,checksum ~ 1,true)
store('bad-dimension-zero',5,0,compressed,checksum,true)
store('bad-dimension-unaligned',5,9,compressed,checksum,true)
store('bad-dimension-overflow',5,65536,compressed,checksum,true)
store('bad-count',-1,128,compressed,checksum,true)
print('ENCODED 11 exact context fixtures and 9 malformed streams')
'@
    $source = @'
using System;
using System.IO;
public static class MaskContextTests
{
    public static void Main(string[] args)
    {
        int passed = 0, rejected = 0;
        foreach (string path in Directory.GetFiles(args[0], "*.spec"))
        {
            string[] spec = File.ReadAllText(path).Split(' ');
            int count = int.Parse(spec[0]), size = int.Parse(spec[1]), length = int.Parse(spec[2]);
            uint checksum = uint.Parse(spec[3]);
            bool bad = spec[4] == "1";
            string packed = File.ReadAllText(Path.ChangeExtension(path, ".packed"));
            char[] chars = new char[packed.Length / 6];
            if (packed.Length % 6 != 0) throw new Exception("Invalid escaped fixture");
            for (int i = 0; i < chars.Length; i++) chars[i] = (char)Convert.ToInt32(packed.Substring(i * 6 + 2, 4), 16);
            byte[][] actual;
            try { actual = AquabroMod.PixelMask.DecodeContextFrames(count, size, length, checksum, new string(chars)); }
            catch (InvalidDataException)
            {
                if (!bad) throw;
                rejected++; Console.WriteLine("REJECT " + Path.GetFileName(path)); continue;
            }
            if (bad) throw new Exception("Malformed context stream accepted: " + path);
            byte[] expected = File.ReadAllBytes(Path.ChangeExtension(path, ".raw"));
            int stride = size * size / 8;
            if (actual.Length != count || expected.Length != count * stride) throw new Exception("Frame count mismatch");
            for (int frame = 0; frame < count; frame++)
            {
                if (actual[frame].Length != stride) throw new Exception("Frame length mismatch");
                for (int i = 0; i < stride; i++)
                    if (actual[frame][i] != expected[frame * stride + i]) throw new Exception("Byte mismatch " + path + ":" + frame + ":" + i);
                for (int earlier = 0; earlier < frame; earlier++)
                    if (object.ReferenceEquals(actual[frame], actual[earlier])) throw new Exception("Frame arrays alias");
            }
            passed++; Console.WriteLine("PASS " + Path.GetFileName(path) + " frames=" + count + " size=" + size + " encoded=" + length);
        }
        if (passed != 11 || rejected != 9) throw new Exception("Missing context codec coverage");
        Console.WriteLine("PASS .NET3.5 context codec; exact=" + passed + " malformedRejected=" + rejected);
    }
}
'@
    $script = Join-Path $temporary 'fixtures.lua'
    $test = Join-Path $temporary 'MaskContextTests.cs'
    $exe = Join-Path $temporary 'MaskContextTests.exe'
    [IO.File]::WriteAllText($script, $lua)
    [IO.File]::WriteAllText($test, $source)
    $fixtureProcess = Start-Process -FilePath $settings.AsepritePath -ArgumentList @('-b','--script-param',('"root=' + $project + '"'),'--script-param',('"output=' + $temporary + '"'),'--script',('"' + $script + '"')) -WindowStyle Hidden -Wait -PassThru
    if ($fixtureProcess.ExitCode -ne 0) { throw 'Context fixture encoding failed' }
    & (Join-Path $env:WINDIR 'Microsoft.NET/Framework/v3.5/csc.exe') /nologo /target:exe /optimize+ /debug- "/out:$exe" $test (Join-Path $project 'src/PixelMask.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Context codec regression compilation failed' }
    & $exe $temporary
    if ($LASTEXITCODE -ne 0) { throw 'Context codec regression failed' }
}
finally {
    $resolved = [IO.Path]::GetFullPath($temporary)
    $parent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if (-not $resolved.StartsWith($parent, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolved) -notlike 'Aquabro-mask-context-*') { throw 'Unexpected context test directory' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}

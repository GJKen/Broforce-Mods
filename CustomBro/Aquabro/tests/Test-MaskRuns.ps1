param([string]$AsepritePath)
$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
. (Join-Path $project 'tools/BuildEnvironment.ps1')
$settings = Get-AquabroBuildSettings -ProjectRoot $project -AsepritePath $AsepritePath
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('Aquabro-mask-runs-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporary | Out-Null
try {
    $lua = @'
local root,output=app.params['root'],app.params['output']
local encode=assert(loadfile(root..'/tools/export_water_effects.lua'))('mask-deflate')
local pack=assert(loadfile(root..'/tools/export_water_effects.lua'))('mask-pack16')
local runs=assert(loadfile(root..'/tools/export_water_effects.lua'))('mask-runs')
local function write(name,text)
 local file=assert(io.open(output..'/'..name,'wb'));file:write(text);file:close()
end
local function binary(bytes)
 local text={};for i,byte in ipairs(bytes) do text[i]=string.char(byte) end
 return table.concat(text)
end
local function store(name,count,size,encoded,bad)
 local compressed=encode(encoded)
 write(name..'.spec',count..' '..size..' '..#compressed..' '..#encoded..' '..(bad and 1 or 0))
 write(name..'.packed',pack(compressed))
end
local function fixture(name,count,size,make)
 local bytes={}
 for i=1,count*size do bytes[i]=make(i) end
 local encoded=runs(bytes,size)
 local repeated=runs(bytes,size)
 assert(binary(encoded)==binary(repeated),'Non-deterministic run encoding')
 write(name..'.raw',binary(bytes));store(name,count,size,encoded,false)
end
fixture('empty',0,1,function() return 0 end)
fixture('zero-64',6,512,function() return 0 end)
fixture('one-128',3,2048,function() return 255 end)
fixture('alternating-bits',2,2048,function(i) return i%2==0 and 85 or 170 end)
fixture('alternating-bytes',4,512,function(i) return i%2==0 and 0 or 255 end)
fixture('frame-reset',3,512,function(i) return i<=512 and 255 or i<=1024 and 0 or 1 end)
fixture('byte-edges',2,9,function(i) return ({0,1,128,255,127,254,3,192,0})[(i-1)%9+1] end)
fixture('varint-127',1,32,function(i) return i<16 and 0 or i==16 and 128 or 255 end)
fixture('varint-128',1,32,function(i) return i<=16 and 0 or 255 end)
fixture('long-run',2,32768,function(i) return i<=32768 and 255 or 0 end)
local state=123456789
fixture('random',5,2048,function() state=(state*1664525+1013904223) & 0xffffffff;return state >> 24 end)
store('bad-missing',1,1,{},true)
store('bad-varint-truncated',1,1,{128},true)
store('bad-overflow',1,1,{255,255,255,255,127},true)
store('bad-frame-overrun',1,1,{9},true)
store('bad-interior-zero',1,1,{1,0,7},true)
store('bad-double-zero',1,1,{0,0,8},true)
store('bad-trailing',1,1,{8,1},true)
store('bad-frame-reset',2,1,{16},true)
print('ENCODED 11 exact run fixtures and 8 malformed streams')
'@
    $source = @'
using System;
using System.IO;
public static class MaskRunTests
{
    public static void Main(string[] args)
    {
        int passed = 0, rejected = 0;
        foreach (string path in Directory.GetFiles(args[0], "*.spec"))
        {
            string[] spec = File.ReadAllText(path).Split(' ');
            int count = int.Parse(spec[0]), size = int.Parse(spec[1]);
            int compressed = int.Parse(spec[2]), runs = int.Parse(spec[3]);
            bool bad = spec[4] == "1";
            string encoded = File.ReadAllText(Path.ChangeExtension(path, ".packed"));
            char[] chars = new char[encoded.Length / 6];
            if (encoded.Length % 6 != 0) throw new Exception("Invalid fixture escape length");
            for (int i = 0; i < chars.Length; i++)
            {
                if (encoded[i * 6] != '\\' || encoded[i * 6 + 1] != 'u') throw new Exception("Invalid fixture escape");
                chars[i] = (char)Convert.ToInt32(encoded.Substring(i * 6 + 2, 4), 16);
            }
            byte[][] actual;
            try { actual = AquabroMod.PixelMask.DecodeRunFrames(count, size, compressed, runs, new string(chars)); }
            catch (InvalidDataException)
            {
                if (!bad) throw;
                rejected++;
                Console.WriteLine("REJECT " + Path.GetFileName(path));
                continue;
            }
            if (bad) throw new Exception("Malformed stream accepted: " + path);
            byte[] expected = File.ReadAllBytes(Path.ChangeExtension(path, ".raw"));
            if (actual.Length != count || expected.Length != count * size) throw new Exception("Frame count mismatch");
            for (int frame = 0; frame < count; frame++)
            {
                if (actual[frame].Length != size) throw new Exception("Frame length mismatch");
                for (int i = 0; i < size; i++)
                    if (actual[frame][i] != expected[frame * size + i]) throw new Exception("Byte mismatch " + path + ":" + frame + ":" + i);
                for (int earlier = 0; earlier < frame; earlier++)
                    if (object.ReferenceEquals(actual[frame], actual[earlier])) throw new Exception("Frames alias");
            }
            passed++;
            Console.WriteLine("PASS " + Path.GetFileName(path) + " frames=" + count + " bytesPerFrame=" + size + " compressed=" + compressed);
        }
        if (passed != 11 || rejected != 8) throw new Exception("Missing run codec coverage");
        Console.WriteLine("PASS .NET3.5 run codec; exact=" + passed + " malformedRejected=" + rejected);
    }
}
'@
    $script = Join-Path $temporary 'fixtures.lua'
    $test = Join-Path $temporary 'MaskRunTests.cs'
    $exe = Join-Path $temporary 'MaskRunTests.exe'
    [IO.File]::WriteAllText($script, $lua)
    [IO.File]::WriteAllText($test, $source)
    $fixtureProcess = Start-Process -FilePath $settings.AsepritePath -ArgumentList @('-b','--script-param',('"root=' + $project + '"'),'--script-param',('"output=' + $temporary + '"'),'--script',('"' + $script + '"')) -WindowStyle Hidden -Wait -PassThru
    if ($fixtureProcess.ExitCode -ne 0) { throw 'Run encoder fixtures failed' }
    & (Join-Path $env:WINDIR 'Microsoft.NET/Framework/v3.5/csc.exe') /nologo /target:exe /optimize+ /debug- "/out:$exe" $test (Join-Path $project 'src/PixelMask.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Run codec regression compilation failed' }
    & $exe $temporary
    if ($LASTEXITCODE -ne 0) { throw 'Run codec regression failed' }
}
finally {
    $resolved = [IO.Path]::GetFullPath($temporary)
    $parent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if (-not $resolved.StartsWith($parent, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolved) -notlike 'Aquabro-mask-runs-*') { throw 'Unexpected run test directory' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}

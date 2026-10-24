param([string]$AsepritePath)
$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
. (Join-Path $project 'tools/BuildEnvironment.ps1')
$settings = Get-AquabroBuildSettings -ProjectRoot $project -AsepritePath $AsepritePath
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('Aquabro-mask-codec-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporary | Out-Null
try {
    # Exercise the actual build encoder with empty, tiny, overlapping, random,
    # maximum-length and 32KiB-distance matches; decode with production .NET3.5 code.
    $lua = @'
local encode=assert(loadfile(app.params['root']..'/tools/export_water_effects.lua'))('mask-deflate')
local pack16=assert(loadfile(app.params['root']..'/tools/export_water_effects.lua'))('mask-pack16')
local function write(name,bytes)
 local text={}
 for i,byte in ipairs(bytes) do text[i]=string.char(byte) end
 local file=assert(io.open(app.params['output']..'/'..name,'wb'))
 file:write(table.concat(text));file:close()
end
local function writeText(name,text)
 local file=assert(io.open(app.params['output']..'/'..name,'w'))
 file:write(text);file:close()
end
local function fixture(name,bytes)
 local compressed=encode(bytes)
 local repeated=encode(bytes)
 assert(#compressed==#repeated,'Non-deterministic length')
 for i,byte in ipairs(compressed) do assert(byte==repeated[i],'Non-deterministic data') end
 write(name..'.raw',bytes);write(name..'.deflate',compressed);writeText(name..'.packed',pack16(compressed))
end
local state=123456789
local function random() state=(state*1664525+1013904223) & 0xffffffff;return state >> 24 end
for _,count in ipairs({0,1,2,3,7,258,259,32768,32769,65536}) do
 local bytes={}
 for i=1,count do bytes[i]=random() end
 fixture('random-'..count,bytes)
end
for _,period in ipairs({1,2,256}) do
 local bytes={}
 for i=1,70000 do bytes[i]=(i-1)%period end
 fixture('period-'..period,bytes)
end
local bytes={}
for i=1,32768 do bytes[i]=random() end
for i=1,32768 do bytes[i+32768]=bytes[i] end
fixture('distance-32768',bytes)
print('ENCODED 14 deterministic fixtures')
'@
    $source = @'
using System;
using System.IO;
public static class MaskDeflateTests
{
    public static void Main(string[] args)
    {
        int count = 0, fixedBlocks = 0, dynamicBlocks = 0, packedFixtures = 0;
        foreach (string path in Directory.GetFiles(args[0], "*.raw"))
        {
            byte[] expected = File.ReadAllBytes(path);
            byte[] encoded = File.ReadAllBytes(Path.ChangeExtension(path, ".deflate"));
            int kind = (encoded[0] >> 1) & 3;
            if (kind == 1) fixedBlocks++; else if (kind == 2) dynamicBlocks++;
            else throw new Exception("Unexpected block kind: " + path);
            byte[][] decoded = AquabroMod.PixelMask.DecodeFrames(1, expected.Length, Convert.ToBase64String(encoded));
            if (decoded.Length != 1 || decoded[0].Length != expected.Length) throw new Exception("Length: " + path);
            for (int i = 0; i < expected.Length; i++)
                if (decoded[0][i] != expected[i]) throw new Exception("Byte mismatch: " + path + ":" + i);
            string packed = Unpack(File.ReadAllText(Path.ChangeExtension(path, ".packed")));
            byte[][] decodedPacked = AquabroMod.PixelMask.DecodeFrames(1, expected.Length, encoded.Length, packed);
            if (decodedPacked.Length != 1 || decodedPacked[0].Length != expected.Length) throw new Exception("Packed length: " + path);
            for (int i = 0; i < expected.Length; i++)
                if (decodedPacked[0][i] != expected[i]) throw new Exception("Packed byte mismatch: " + path + ":" + i);
            Console.WriteLine(Path.GetFileName(path) + " bytes=" + expected.Length + " compressed=" + encoded.Length + " packedChars=" + packed.Length + " block=" + kind);
            packedFixtures++;
            count++;
        }
        if (count != 14 || packedFixtures != 14 || fixedBlocks == 0 || dynamicBlocks == 0) throw new Exception("Missing codec coverage");
        Console.WriteLine("PASS UTF16-pack+DEFLATE .NET3.5; fixtures=" + count + " fixed=" + fixedBlocks + " dynamic=" + dynamicBlocks);
    }

    static string Unpack(string value)
    {
        if (value.Length % 6 != 0) throw new Exception("Invalid packed fixture length");
        char[] result = new char[value.Length / 6];
        for (int i = 0; i < result.Length; i++)
        {
            int offset = i * 6;
            if (value[offset] != '\\' || value[offset + 1] != 'u') throw new Exception("Invalid packed fixture escape");
            result[i] = (char)Convert.ToInt32(value.Substring(offset + 2, 4), 16);
        }
        return new string(result);
    }
}
'@
    $script = Join-Path $temporary 'fixtures.lua'
    $test = Join-Path $temporary 'MaskDeflateTests.cs'
    $executable = Join-Path $temporary 'MaskDeflateTests.exe'
    [IO.File]::WriteAllText($script, $lua)
    [IO.File]::WriteAllText($test, $source)
    & $settings.AsepritePath -b --script-param "root=$project" --script-param "output=$temporary" --script $script | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Mask encoder fixtures failed' }
    & (Join-Path $env:WINDIR 'Microsoft.NET/Framework/v3.5/csc.exe') /nologo /target:exe /optimize+ /debug- "/out:$executable" $test (Join-Path $project 'src/PixelMask.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Mask codec regression compilation failed' }
    & $executable $temporary
    if ($LASTEXITCODE -ne 0) { throw 'Mask codec regression failed' }
}
finally {
    $resolved = [IO.Path]::GetFullPath($temporary)
    $parent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if (-not $resolved.StartsWith($parent, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolved) -notlike 'Aquabro-mask-codec-*') { throw 'Unexpected codec test directory' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}

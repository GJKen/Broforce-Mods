param()
$ErrorActionPreference = 'Stop'
$source = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path (Split-Path -Parent $PSScriptRoot) 'src\HarmonyDiagnostics.WorldState.cs')
$start = $source.IndexOf('private static byte[] DecompressWorldPacket(')
if ($start -lt 0) { throw 'Missing production decompressor.' }
$brace = $source.IndexOf('{', $start)
$depth = 1
$end = $brace + 1
while ($depth -gt 0 -and $end -lt $source.Length) {
    if ($source[$end] -eq '{') { $depth++ }
    if ($source[$end] -eq '}') { $depth-- }
    $end++
}
$declaration = $source.Substring($start, $end - $start)
$fixture = @'
using System;
using System.IO;
using System.IO.Compression;
public static class CompressionFixture {
__DECLARATION__
 public static void Run() {
  // Captured from a real Bro Hard effect-removal RPC that failed on Unity Mono.
  var packet=Convert.FromBase64String("Y2dgYGAGYjYgZmQQcstJLM5wSsxLd60oyMkvzszPwyL0H6QBDSTkLXWGsQE=");
  var expected=Convert.FromBase64String("BwAAAAMAAAAGAAAAAQASRmxhc2hCYW5nRXhwbG9zaW9uEkZsYXNoQmFuZ0V4cGxvc2lvbv8DAAAAAAAAAAAAAAAAAAAAAAAAYG6lQwAAAAAAAAAA");
  if(Convert.ToBase64String(DecompressWorldPacket(packet))!=Convert.ToBase64String(expected))throw new Exception("captured effect payload was truncated");
  // Exercise both sides of the block boundary and a multi-block batch.
  foreach(var length in new[]{1,4095,4096,4097,12539}) {
   var original=new byte[length];new Random(length).NextBytes(original);
   byte[] compressed;
   using(var output=new MemoryStream()) {
    using(var zip=new DeflateStream(output,CompressionMode.Compress,true))zip.Write(original,0,original.Length);
    compressed=output.ToArray();
   }
   if(Convert.ToBase64String(DecompressWorldPacket(compressed))!=Convert.ToBase64String(original))throw new Exception("batch size "+length+" was truncated");
  }
  Console.WriteLine("PASS: 6 world compression packet and block-boundary checks");
 }
}
'@
Add-Type -TypeDefinition ($fixture.Replace('__DECLARATION__', $declaration))
[CompressionFixture]::Run()

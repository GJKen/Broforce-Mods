param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$projectileSource = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $projectRoot 'src\HarmonyDiagnostics.WorldProjectiles.cs')

function Read-Declaration([string]$source, [string]$signature) {
    $start = $source.IndexOf($signature)
    if ($start -lt 0) { throw "Missing production declaration: $signature" }
    $brace = $source.IndexOf('{', $start)
    $depth = 1
    $end = $brace + 1
    while ($depth -gt 0 -and $end -lt $source.Length) {
        if ($source[$end] -eq '{') { $depth++ }
        if ($source[$end] -eq '}') { $depth-- }
        $end++
    }
    return $source.Substring($start, $end - $start)
}

$declarations = @(
    Read-Declaration $projectileSource 'private static bool ApplyWorldSpearTermination('
    Read-Declaration $projectileSource 'private static bool TryGetWorldSpearMotion('
    Read-Declaration $projectileSource 'private static bool TryGetWorldSpearTerminalMotion('
) -join [Environment]::NewLine

$fixture = @'
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine {
 public struct Vector2 { public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;} public static Vector2 zero { get { return new Vector2(0,0); } } public static Vector2 Lerp(Vector2 a,Vector2 b,float t){return new Vector2(a.x+(b.x-a.x)*t,a.y+(b.y-a.y)*t);} public static Vector2 operator +(Vector2 a,Vector2 b){return new Vector2(a.x+b.x,a.y+b.y);} public static Vector2 operator *(Vector2 a,float s){return new Vector2(a.x*s,a.y*s);} }
 public struct Vector3 { public float x,y,z; public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;} }
 public static class Mathf { public static float Clamp(float v,float min,float max){return Math.Max(min,Math.Min(max,v));} public static float Clamp01(float v){return Clamp(v,0,1);} public static float LerpAngle(float a,float b,float t){return a+(b-a)*t;} public static float Max(float a,float b){return Math.Max(a,b);} public static float Min(float a,float b){return Math.Min(a,b);} }
 public class Transform { public Vector3 eulerAngles; }
 public static class Time { public static float unscaledTime=50f; }
}

namespace CustomMapMultiplayer {
 internal struct NID { public int Value; public NID(int value){Value=value;} }
 internal struct PID { public int AsInt; public PID(int value){AsInt=value;} }
 internal struct WorldProjectileSample { public float Time,LocalTime,X,Y,VX,VY,Rotation; }
 internal sealed class WorldSnapshot { public NID Id; public string Type; public int Revision,Owner; public float X,Y,VX,VY,Rotation,SentAt; }
 internal sealed class WorldEntry { public NID Id; public WorldSnapshot Last; public List<WorldProjectileSample> SpearMotion; public List<WorldProjectileSample> ProjectileMotion; public bool SpearEnding; }
 internal class Projectile { public float X,Y; public Transform transform=new Transform(); }
 internal sealed class PredabroSpear : Projectile { public NID Nid; public PID Owner; public int SetPositionCalls; public void SetXY(float x,float y){X=x;Y=y;} public void SetPosition(){SetPositionCalls++;} }

 internal static class Fixture {
  private static int checks;
  private static void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
  __PRODUCTION_DECLARATIONS__
  private static void Main(){
   var spear=new PredabroSpear { Nid=new NID(7),Owner=new PID(2),X=90,Y=91 };
   var entry=new WorldEntry { Id=spear.Nid, Last=new WorldSnapshot { Revision=20,X=1,Y=2 },
    SpearMotion=new List<WorldProjectileSample> { new WorldProjectileSample { Time=1,LocalTime=1,X=1,Y=2,VX=400,VY=0 } },
    ProjectileMotion=new List<WorldProjectileSample> { new WorldProjectileSample { Time=1,X=1,Y=2 } } };
   Vector2 position; float rotation;
   var motion=new List<WorldProjectileSample> {
    new WorldProjectileSample { Time=1,LocalTime=1,X=0,Y=0,VX=400,Rotation=0 },
    new WorldProjectileSample { Time=1.016667f,LocalTime=1.016667f,X=6.667f,Y=0,VX=400,Rotation=0 }
   };
   Check(TryGetWorldSpearMotion(motion,1.008333f,1f/60f,0.3f,false,out position,out rotation) &&
    Math.Abs(position.x-3.3335f)<0.01f,"spear presentation interpolates between receiver-clock samples");
   Check(TryGetWorldSpearMotion(motion,1.2f,1f/60f,0.3f,false,out position,out rotation) &&
    Math.Abs(position.x-13.3337f)<0.01f,"late spear packets extrapolate by no more than one tick");
   Check(TryGetWorldSpearMotion(motion,1.2f,1f/60f,0.004f,false,out position,out rotation) &&
    Math.Abs(position.x-8.267f)<0.02f,"near-expiry spear extrapolation cannot exceed remaining travel time");
   Check(TryGetWorldSpearMotion(motion,1.2f,1f/60f,-0.01f,false,out position,out rotation) &&
    Math.Abs(position.x-6.667f)<0.01f,"expired spear motion stops extrapolating");
   Check(ApplyWorldSpearTermination(entry,spear,12.5f,-4f,21),"newer owner termination is accepted");
   Check(entry.SpearEnding && entry.SpearMotion==null && entry.ProjectileMotion==null,"termination clears both prediction histories");
   Check(entry.Last.Revision==21 && entry.Last.X==12.5f && entry.Last.Y==-4f && entry.Last.VX==0f && entry.Last.VY==0f,"termination records a stationary authoritative endpoint");
   Check(spear.X==12.5f && spear.Y==-4f && spear.SetPositionCalls==1,"replica transform is snapped to the terminal coordinate");
   Check(TryGetWorldSpearTerminalMotion(spear,entry,out position,out rotation) && position.x==12.5f && position.y==-4f,"terminal presentation bypasses velocity extrapolation");
   Check(!ApplyWorldSpearTermination(entry,spear,80,90,20) && spear.X==12.5f && spear.Y==-4f,"an older terminal event cannot move the spear back");
   Console.WriteLine("PASS: "+checks+" world spear motion and termination checks");
  }
 }
}
'@

$fixture = $fixture.Replace('__PRODUCTION_DECLARATIONS__', $declarations)
$temporaryPath = Join-Path $PSScriptRoot ('world-spear-termination-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporaryPath | Out-Null
try {
    $fixturePath = Join-Path $temporaryPath 'Fixture.cs'
    $outputPath = Join-Path $temporaryPath 'Fixture.exe'
    [IO.File]::WriteAllText($fixturePath, $fixture, (New-Object Text.UTF8Encoding($false)))
    $compiler = Join-Path $env:windir 'Microsoft.NET\Framework64\v3.5\csc.exe'
    & $compiler /nologo /nowarn:0649 /target:exe "/out:$outputPath" $fixturePath
    if ($LASTEXITCODE -ne 0) { throw 'World spear termination fixture compilation failed.' }
    & $outputPath
    if ($LASTEXITCODE -ne 0) { throw 'World spear termination regression failed.' }
}
finally {
    Remove-Item -LiteralPath $temporaryPath -Recurse -Force
}

param()
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
[xml]$props = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $projectRoot 'LocalBroforcePath.props')
$harmonyPath = Join-Path $props.Project.PropertyGroup.UnityModManagerPath '0Harmony.dll'
$fixture = @'
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
public class BroforceObject { public bool Replica; }
public class Unit:BroforceObject { }
public class Product { public int Value; }
public static class MapController {
 public static int Spawns;
 [MethodImpl(MethodImplOptions.NoInlining)] public static Product SpawnProduct(int value) { Spawns++;return new Product { Value=value }; }
}
namespace Networking { public static class Networking { } }
public static class PickupableController {
 public static int Money;
 [MethodImpl(MethodImplOptions.NoInlining)] public static void CreateMoneySpray(int count) { Money+=count; }
}
public class Vehicle:Unit {
 public int Cleanup;
 [MethodImpl(MethodImplOptions.NoInlining)] public void Death() { Product unread=MapController.SpawnProduct(1); MapController.SpawnProduct(2); Cleanup++; }
}
public class RewardBox:BroforceObject {
 public int Visuals,Released;
 [MethodImpl(MethodImplOptions.NoInlining)] public void MakeEffects() { PickupableController.CreateMoneySpray(5);MapController.SpawnProduct(3);Visuals++;Released++; }
}
public class Consumer:Unit {
 public int Value;
 [MethodImpl(MethodImplOptions.NoInlining)] public void Death() { MapController.SpawnProduct(4);Value=MapController.SpawnProduct(9).Value; }
}
namespace CustomMapMultiplayer {
 internal static partial class HarmonyDiagnostics {
  private static int _worldApplying;
  private static bool IsWorldReplica(BroforceObject obj) { return obj.Replica; }
  private const BindingFlags WorldFields=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
  private static Harmony _harmony=new Harmony("cmm.replay-generation.regression");
  private static class DiagnosticLog { public static void Info(string text) { } }
  private static void PatchWorldMethod(MethodBase method,string prefix,string postfix) { _harmony.Patch(method,new HarmonyMethod(typeof(HarmonyDiagnostics).GetMethod(prefix,BindingFlags.NonPublic|BindingFlags.Static)),null,null,null); }
  private static int Checks;
  private static void Check(bool ok,string text) { Checks++;if(!ok)throw new Exception(text); }
  public static void Main() {
   Check(PatchProcessor.GetOriginalInstructions(typeof(Vehicle).GetMethod("Death"),null).Exists(c=>c.opcode.Name.StartsWith("stloc")),"fixture retains shipped APC unread-local IL shape");
   PatchWorldReplayGeneration();
   var vehicle=new Vehicle();var box=new RewardBox();
   vehicle.Death();box.MakeEffects();Check(MapController.Spawns==3 && PickupableController.Money==5,"authority retains native factories");
   _worldApplying=1;vehicle.Death();box.MakeEffects();vehicle.Death();box.MakeEffects();
   Check(MapController.Spawns==3 && PickupableController.Money==5,"repeated lifecycle presentation generates no entity or money");
   Check(vehicle.Cleanup==3 && box.Visuals==3 && box.Released==3,"presentation cleanup and release continuation still execute");
   var consumer=new Consumer();consumer.Death();
   Check(consumer.Value==9 && MapController.Spawns==4,"consumed factory return keeps its call contract; no null placeholder");
   _worldApplying=0;vehicle.Death();PickupableController.CreateMoneySpray(2);
   Check(MapController.Spawns==6 && PickupableController.Money==7,"leaving replay restores authority generation");
   box.Replica=true;box.MakeEffects();vehicle.Replica=true;vehicle.Death();
   Check(MapController.Spawns==6 && PickupableController.Money==7,"native replica lifecycle outside state application generates no rewards");
   Check(box.Visuals==4 && box.Released==4 && vehicle.Cleanup==5,"native replica lifecycle retains visual cleanup and existing-item release");
   box.Replica=false;box.MakeEffects();
   Check(MapController.Spawns==7 && PickupableController.Money==12,"replica lifecycle scope is restored for subsequent authority calls");
   Console.WriteLine("PASS: "+Checks+" replay-generation Harmony/IL regression checks");
  }
 }
}
'@
$temporaryPath = Join-Path $PSScriptRoot ('world-replay-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporaryPath | Out-Null
try {
    $fixturePath = Join-Path $temporaryPath 'Fixture.cs'
    $outputPath = Join-Path $temporaryPath 'Fixture.exe'
    [IO.File]::WriteAllText($fixturePath, $fixture, (New-Object Text.UTF8Encoding($false)))
    Copy-Item -LiteralPath $harmonyPath -Destination (Join-Path $temporaryPath '0Harmony.dll')
    $compiler = Join-Path $env:windir 'Microsoft.NET\Framework64\v3.5\csc.exe'
    & $compiler /nologo /optimize- /target:exe "/reference:$harmonyPath" "/out:$outputPath" $fixturePath (Join-Path $projectRoot 'src\HarmonyDiagnostics.WorldReplayGeneration.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Replay-generation fixture compilation failed.' }
    & $outputPath
    if ($LASTEXITCODE -ne 0) { throw 'Replay-generation regression failed.' }
}
finally {
    Remove-Item -LiteralPath $temporaryPath -Recurse -Force
}

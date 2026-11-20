param()
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$projectileSource = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $projectRoot 'src\HarmonyDiagnostics.WorldProjectiles.cs')
$grenadeSource = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $projectRoot 'src\HarmonyDiagnostics.WorldGrenades.cs')
$worldSource = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $projectRoot 'src\HarmonyDiagnostics.WorldState.cs')

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
    Read-Declaration $worldSource 'private static bool AcceptWorldRpc('
    Read-Declaration $worldSource 'private static bool IsWorldRpcOwner('
    Read-Declaration $worldSource 'private static bool IsWorldRpcObject('
    Read-Declaration $worldSource 'private static void WriteWorldId('
    Read-Declaration $worldSource 'private static NID ReadWorldId('
    Read-Declaration $projectileSource 'private sealed class WorldSpawn'
    Read-Declaration $projectileSource 'private static void SendWorldSpawn('
    Read-Declaration $projectileSource 'private static WorldSpawn ReadWorldSpawn('
    Read-Declaration $projectileSource 'private static void SendWorldProjectileSpawn('
    Read-Declaration $projectileSource 'private static void ReceiveWorldProjectileRPC('
    Read-Declaration $grenadeSource 'private static void SendWorldGrenadeSpawn('
    Read-Declaration $grenadeSource 'private static void ReceiveWorldGrenadeSpawnRPC('
) -join [Environment]::NewLine

$fixture = @'
using System;
using System.IO;
using System.Collections.Generic;
using Networking;
public struct SID {
 public byte AsByte; public SID(byte b){AsByte=b;}
 public static bool operator ==(SID a,SID b){return a.AsByte==b.AsByte;}
 public static bool operator !=(SID a,SID b){return !(a==b);}
 public override bool Equals(object o){return o is SID&&this==(SID)o;} public override int GetHashCode(){return AsByte;}
}
public struct PID {
 public int AsInt; public PID(int n){AsInt=n;}
 public static PID TargetServer=new PID(0),ServerID=new PID(6),TargetOthers=new PID(-1);
 public static bool operator ==(PID a,PID b){return a.AsInt==b.AsInt;}
 public static bool operator !=(PID a,PID b){return !(a==b);}
 public override bool Equals(object o){return o is PID&&this==(PID)o;} public override int GetHashCode(){return AsInt;}
}
public struct NID {
 public SID Sid; public PID OwnerID; public uint GameObjectID; public ushort ComponentID;
 public NID(SID s,PID p,uint g,ushort c){Sid=s;OwnerID=p;GameObjectID=g;ComponentID=c;}
}
public static class Connect { public static SID SessionID=new SID(5); }
public class MessageInfo { public PID Sender=new PID(2); }
public class RpcObject { public MessageInfo messageInfo=new MessageInfo(); }
public static class RPCController { public static RpcObject CurrentRPC=new RpcObject(); }
namespace Networking {
 public delegate void RpcSignature<T1,T2>(T1 a,T2 b);
 public static class Networking {
  public static byte[] Packet; public static string Method; public static long Epoch; public static PID Target; public static bool Immediate;
  public static void RPC<T1,T2>(PID target,RpcSignature<T1,T2> method,T1 a,T2 b,bool immediate){Packet=(byte[])(object)b;Method=method.Method.Name;Epoch=Convert.ToInt64(a);Target=target;Immediate=immediate;}
 }
}
public static class Fixture {
 private static long _worldEpoch=123;
 private static bool Online=true,CreateAllowed=true;
 private static bool IsWorldSession(){return Online;}
 private static WorldSpawn Received;
 private static readonly Dictionary<NID,WorldSpawn> WorldPendingProjectiles=new Dictionary<NID,WorldSpawn>();
 private static readonly Dictionary<NID,WorldSpawn> WorldPendingGrenades=new Dictionary<NID,WorldSpawn>();
 private static bool TryCreateWorldProjectile(WorldSpawn s){Received=s;return CreateAllowed;}
 private static bool TryCreateWorldGrenade(WorldSpawn s){Received=s;return CreateAllowed;}
__DECLARATIONS__
 private static int checks;
 private static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
 private static string Hex(byte[] bytes){return BitConverter.ToString(bytes).Replace("-","");}
 public static void Main(){
  var s=new WorldSpawn {Id=new NID(new SID(5),new PID(2),0x12345678,0x1234),Actor=new NID(new SID(5),new PID(2),0x87654321,0x5678),Prefab="P",Type="T",Owner=2,Player=1,Seed=-23,X=1.25f,Y=-2.5f,VX=300.5f,VY=-42.25f,Offset=7.5f,Life=3.25f};
  // Fixed protocol-24 packets from the original two serializers, including distinct final floats.
  SendWorldProjectileSpawn(s,PID.TargetOthers);var projectile=Networking.Networking.Packet;
  Check(Hex(projectile)=="05027856341234120502214365877856015001540201000000E9FFFFFF0000A03F000020C000409643000029C20000F040","projectile packet retains the original byte layout");
  Check(Networking.Networking.Method=="ReceiveWorldProjectileRPC"&&Networking.Networking.Target==PID.TargetOthers&&Networking.Networking.Epoch==123&&Networking.Networking.Immediate,"projectile keeps its RPC endpoint and immediate delivery");
  ReceiveWorldProjectileRPC(123,projectile);
  Check(Received.Id.GameObjectID==0x12345678&&Received.Actor.ComponentID==0x5678&&Received.Prefab=="P"&&Received.Type=="T"&&Received.Owner==2&&Received.Player==1&&Received.Seed==-23,"projectile identity and factory parameters survive decoding");
  Check(Received.X==1.25f&&Received.Y==-2.5f&&Received.VX==300.5f&&Received.VY==-42.25f&&Received.Offset==7.5f&&Received.Life==0,"projectile offset is independent of grenade lifetime");
  SendWorldGrenadeSpawn(s,PID.ServerID);var grenade=Networking.Networking.Packet;
  Check(Hex(grenade)=="05027856341234120502214365877856015001540201000000E9FFFFFF0000A03F000020C000409643000029C200005040","grenade packet retains the original byte layout");
  Check(Networking.Networking.Method=="ReceiveWorldGrenadeSpawnRPC"&&Networking.Networking.Target==PID.ServerID&&Networking.Networking.Epoch==123&&Networking.Networking.Immediate,"grenade keeps its RPC endpoint and immediate delivery");
  ReceiveWorldGrenadeSpawnRPC(123,grenade);
  Check(Received.Life==3.25f&&Received.Offset==0&&Received.Seed==-23&&Received.Owner==2,"grenade lifetime is independent of projectile offset");
  Received=null;ReceiveWorldProjectileRPC(122,projectile);ReceiveWorldGrenadeSpawnRPC(122,grenade);Check(Received==null,"old epoch cannot create either object");
  Online=false;ReceiveWorldProjectileRPC(123,projectile);ReceiveWorldGrenadeSpawnRPC(123,grenade);Check(Received==null,"offline receives cannot create objects");Online=true;
  RPCController.CurrentRPC.messageInfo.Sender=new PID(3);ReceiveWorldProjectileRPC(123,projectile);ReceiveWorldGrenadeSpawnRPC(123,grenade);Check(Received==null,"foreign owner cannot create objects");
  RPCController.CurrentRPC.messageInfo.Sender=new PID(2);Connect.SessionID=new SID(4);ReceiveWorldProjectileRPC(123,projectile);ReceiveWorldGrenadeSpawnRPC(123,grenade);Check(Received==null,"foreign session cannot create objects");Connect.SessionID=new SID(5);
  CreateAllowed=false;ReceiveWorldProjectileRPC(123,projectile);ReceiveWorldGrenadeSpawnRPC(123,grenade);Check(WorldPendingProjectiles.Count==1&&WorldPendingGrenades.Count==1,"unready objects retain separate pending queues");
  CreateAllowed=true;s.Owner=PID.TargetServer.AsInt;RPCController.CurrentRPC.messageInfo.Sender=PID.ServerID;SendWorldGrenadeSpawn(s,PID.TargetOthers);Received=null;ReceiveWorldGrenadeSpawnRPC(123,Networking.Networking.Packet);Check(Received!=null&&Received.Owner==PID.TargetServer.AsInt,"server-owned object resolves to the real server sender");
  // Rejected RPC contexts must return before touching even an invalid payload.
  Received=null;var savedRpc=RPCController.CurrentRPC;RPCController.CurrentRPC=null;
  ReceiveWorldProjectileRPC(123,null);ReceiveWorldGrenadeSpawnRPC(123,null);
  Check(Received==null,"missing RPC context is rejected before payload decoding");RPCController.CurrentRPC=savedRpc;
  _worldEpoch=0;ReceiveWorldProjectileRPC(0,null);ReceiveWorldGrenadeSpawnRPC(0,null);
  Check(Received==null,"inactive epoch rejects even a matching zero epoch");_worldEpoch=123;
  ReceiveWorldProjectileRPC(122,null);ReceiveWorldGrenadeSpawnRPC(124,null);
  Check(Received==null,"old and future epochs are rejected before payload decoding");
  SendWorldProjectileSpawn(s,PID.TargetOthers);projectile=Networking.Networking.Packet;
  RPCController.CurrentRPC.messageInfo.Sender=PID.TargetServer;
  ReceiveWorldProjectileRPC(123,projectile);ReceiveWorldGrenadeSpawnRPC(123,Networking.Networking.Packet);
  Check(Received==null,"server-owned messages require the actual server, not the target-server sentinel");
  RPCController.CurrentRPC.messageInfo.Sender=PID.ServerID;
  ReceiveWorldProjectileRPC(123,projectile);
  Check(Received!=null&&Received.Owner==PID.TargetServer.AsInt,"projectile accepts actual server for server-owned objects");
  Console.WriteLine("PASS: "+checks+" world spawn protocol and routing checks");
 }
}
'@
$fixture = $fixture.Replace('__DECLARATIONS__', $declarations)
$temporaryPath = Join-Path $PSScriptRoot ('world-spawns-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporaryPath | Out-Null
try {
    $fixturePath = Join-Path $temporaryPath 'Fixture.cs'
    $outputPath = Join-Path $temporaryPath 'Fixture.exe'
    [IO.File]::WriteAllText($fixturePath, $fixture, (New-Object Text.UTF8Encoding($false)))
    $compiler = Join-Path $env:windir 'Microsoft.NET\Framework64\v3.5\csc.exe'
    & $compiler /nologo /optimize+ /target:exe "/out:$outputPath" $fixturePath
    if ($LASTEXITCODE -ne 0) { throw 'World spawn fixture compilation failed.' }
    & $outputPath
    if ($LASTEXITCODE -ne 0) { throw 'World spawn regression failed.' }
}
finally {
    Remove-Item -LiteralPath $temporaryPath -Recurse -Force
}

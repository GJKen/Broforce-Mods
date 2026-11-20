using System;
using System.Reflection;
using HarmonyLib;
using Networking;
using UnityEngine;

namespace CustomMapMultiplayer
{
    internal static partial class HarmonyDiagnostics
    {
        private static BroforceObject _worldDirectSource;
        private static int _worldReceivingHit;
        private static BroforceObject _worldReceivingTarget;
        private static Mine _worldCountdownMine;

        private static bool WorldCheckForMinePrefix(Block __instance)
        {
            if (_worldEpoch == 0 || !IsWorldSession() || __instance.Nid == NID.NoID) return true;
            var mine = __instance.mine;
            if (mine != null && !mine.exploded && !mine.detonated)
                global::Networking.Networking.RPC<long, NID>(PID.TargetAll,
                    new RpcSignature<long, NID>(ReceiveWorldMinePrimeRPC), _worldEpoch, __instance.Nid, false);
            return false;
        }

        [AllowedRPC]
        private static void ReceiveWorldMinePrimeRPC(long epoch, NID blockId)
        {
            if (_worldEpoch == 0 || epoch != _worldEpoch || !IsWorldSession() ||
                blockId.Sid != Connect.SessionID || RPCController.CurrentRPC == null) return;
            // Mine.Start allocates from the global deterministic sequence, which can
            // differ between peers. The natural block already has a shared identity.
            var block = Registry.GetObject(blockId) as Block;
            if (block != null && block.mine != null) block.mine.Detonate(false);
        }

        private static void WorldMineExplosionPrefix(Mine __instance, int playerNum, out Mine __state)
        {
            __state = _worldCountdownMine;
            if (_worldApplying == 0 && _worldEpoch != 0 && IsWorldSession() && playerNum == -10 && !__instance.exploded)
                _worldCountdownMine = __instance;
        }

        private static void WorldMineExplosionFinalizer(Mine __state) { _worldCountdownMine = __state; }

        private static void PreserveWorldMineCountdown(ref object targetObject, ref MethodInfo methodInfo, ref object[] parameters)
        {
            var block = targetObject as Block;
            if (_worldCountdownMine == null || block == null || block.mine != _worldCountdownMine ||
                methodInfo.Name != "ActuallyCollapse" || !block.IsMine || block.Nid == NID.NoID) return;
            // The mine has already exploded locally. Its own collapse must not turn
            // the replica's pending player-damaging countdown into Explode(5).
            parameters = new object[] { _worldEpoch, block.Nid, parameters[0], parameters[1], parameters[2] };
            targetObject = null;
            methodInfo = typeof(HarmonyDiagnostics).GetMethod("ReceiveWorldMineCollapseRPC", BindingFlags.Static | BindingFlags.NonPublic);
        }

        [AllowedRPC]
        private static void ReceiveWorldMineCollapseRPC(long epoch, NID blockId, float vx, float vy, bool collapseAround)
        {
            if (_worldEpoch == 0 || epoch != _worldEpoch || !IsWorldSession() || blockId.Sid != Connect.SessionID ||
                RPCController.CurrentRPC == null) return;
            var block = Registry.GetObject(blockId) as Block;
            if (block == null || block.IsMine || RPCController.CurrentRPC.messageInfo.Sender !=
                (block.Owner == PID.TargetServer ? PID.ServerID : block.Owner)) return;
            var mine = block.mine;
            if (mine != null && !mine.exploded) mine.Detonate(false);
            block.mine = null;
            try { block.ActuallyCollapse(vx, vy, collapseAround); }
            finally { block.mine = mine; }
        }

        private static byte WorldDirectMutationKind(MethodBase method)
        {
            var p = method.GetParameters();
            if (method.Name == "DamageOptional" && typeof(Doodad).IsAssignableFrom(method.DeclaringType) &&
                p.Length == 2 && p[0].ParameterType == typeof(DamageObject) && p[1].ParameterType == typeof(bool).MakeByRefType()) return 3;
            if (method.Name == "BurnInternal" && typeof(Unit).IsAssignableFrom(method.DeclaringType) &&
                p.Length == 2 && p[0].ParameterType == typeof(int) && p[1].ParameterType == typeof(int)) return 4;
            if (method.Name == "SetAlight_Local" && p.Length == 0 &&
                (typeof(Block).IsAssignableFrom(method.DeclaringType) || typeof(DoodadDestroyable).IsAssignableFrom(method.DeclaringType))) return 5;
            if (method.Name == "Damage" && typeof(Projectile).IsAssignableFrom(method.DeclaringType) && p.Length == 6 &&
                p[0].ParameterType == typeof(int) && p[1].ParameterType == typeof(DamageType) &&
                p[2].ParameterType == typeof(float) && p[3].ParameterType == typeof(float) &&
                p[4].ParameterType == typeof(float) && p[5].ParameterType == typeof(int)) return 6;
            if (method.Name == "Damage" && typeof(Unit).IsAssignableFrom(method.DeclaringType) && p.Length == 8 &&
                p[0].ParameterType == typeof(int) && p[1].ParameterType == typeof(DamageType) &&
                p[2].ParameterType == typeof(float) && p[3].ParameterType == typeof(float) &&
                p[4].ParameterType == typeof(int) && p[5].ParameterType == typeof(MonoBehaviour) &&
                p[6].ParameterType == typeof(float) && p[7].ParameterType == typeof(float)) return 7;
            if ((method.Name == "GibNow" || method.Name == "Gib") && typeof(Unit).IsAssignableFrom(method.DeclaringType) &&
                p.Length == 3 && p[0].ParameterType == typeof(DamageType) &&
                p[1].ParameterType == typeof(float) && p[2].ParameterType == typeof(float))
                return method.Name == "GibNow" ? (byte)8 : (byte)9;
            return 0;
        }

        private static BroforceObject WorldDirectActor(MonoBehaviour actor)
        {
            // Native collapse runs the attached mine on each peer. Keep its own
            // source boundary rather than forwarding another hit from this request.
            if (actor is Mine) return (Mine)actor;
            if (_worldDamageSource != null) return _worldDamageSource;
            // Collateral invoked while settling a request is initiated by this target's owner.
            // Keep the original damageSender separately; it may belong to another peer.
            if (_worldReceivingTarget != null) return _worldReceivingTarget;
            var obj = actor as BroforceObject;
            if (obj != null) return obj;
            return _worldDirectSource != null ? _worldDirectSource : _worldStatusSource;
        }

        private static void WorldAreaDamagePrefix(int playerNum, out BroforceObject __state)
        { BeginWorldAreaDamage(null, playerNum, out __state); }

        private static void WorldAreaDamageSenderPrefix(MonoBehaviour sender, int playerNum, out BroforceObject __state)
        { BeginWorldAreaDamage(sender, playerNum, out __state); }

        private static void WorldAreaDamageFiredPrefix(MonoBehaviour firedBy, int playerNum, out BroforceObject __state)
        { BeginWorldAreaDamage(firedBy, playerNum, out __state); }

        private static void WorldAreaDamageProjectilePrefix(int newPlayerNum, out BroforceObject __state)
        { BeginWorldAreaDamage(null, newPlayerNum, out __state); }

        private static void BeginWorldAreaDamage(MonoBehaviour actor, int player, out BroforceObject previous)
        {
            previous = _worldDirectSource;
            // Explicit actor/context wins over playerNum (which can change after reflection).
            if (actor != null) _worldDirectSource = actor as BroforceObject;
            else if (_worldDirectSource == null && player >= 0 && player < HeroController.players.Length && HeroController.players[player] != null)
                _worldDirectSource = HeroController.players[player].character;
        }

        private static void WorldAreaDamageFinalizer(BroforceObject __state) { _worldDirectSource = __state; }

        // Harmony's __args write-back for ref/out bool can overwrite adjacent caller
        // locals on the shipped Mono runtime. Read the hit without exposing that ref.
        private static bool WorldDirectOptionalMutationPrefix(BroforceObject __instance, MethodBase __originalMethod, DamageObject __0)
        { return WorldDirectMutationPrefix(__instance, __originalMethod, new object[] { __0, false }); }

        private static bool WorldDirectMutationPrefix(BroforceObject __instance, MethodBase __originalMethod, object[] __args)
        {
            if (_worldApplying != 0 || _worldReceivingHit != 0 && __instance.IsMine ||
                _worldEpoch == 0 || !IsWorldSession() || !IsWorldObject(__instance))
                return WorldMutationPrefix(__instance, __originalMethod);
            var kind = WorldDirectMutationKind(__originalMethod);
            // Native RPCs and presentation replay retain their existing ownership boundary.
            if (kind >= 7 && RPCController.CurrentRPC != null && _worldReceivingHit == 0)
                return WorldMutationPrefix(__instance, __originalMethod);
            var hit = kind == 3 ? (DamageObject)__args[0] : null;
            var actor = kind == 7 ? __args[5] as MonoBehaviour : hit == null ? null : hit.damageSender;
            if (kind == 7 && actor != null && Registry.GetNID(actor) == NID.NoID)
                return WorldMutationPrefix(__instance, __originalMethod);
            var source = WorldDirectActor(actor);
            if (source == null || source.Nid == NID.NoID ||
                !(source is Unit && ((Unit)source).IsHero) && !IsWorldObject(source))
                return WorldMutationPrefix(__instance, __originalMethod);
            // A replicated hero's animation is presentation of the owner's attack.
            // The owner's request, not that second simulation, settles these routes.
            if (!source.IsMine) return false;
            if (__instance.IsMine) return WorldMutationPrefix(__instance, __originalMethod);
            if (kind >= 7)
            {
                var gib = kind >= 8;
                SendWorldProjectileContactHit(source, __instance, gib ? source : actor,
                    gib ? 0 : (int)__args[0], (DamageType)__args[gib ? 0 : 1],
                    (float)__args[gib ? 1 : 2], (float)__args[gib ? 2 : 3],
                    gib ? 0 : (int)__args[4], false,
                    gib ? __instance.X : (float)__args[6], gib ? __instance.Y : (float)__args[7],
                    kind, NID.NoID, (DamageType)__args[gib ? 0 : 1]);
                return false;
            }
            var damage = kind == 3 ? hit.damage : kind == 5 ? 0 : (int)__args[0];
            var type = kind == 3 ? hit.damageType : kind == 6 ? (DamageType)__args[1] : DamageType.Fire;
            var vx = kind == 3 ? hit.xForce : kind == 6 ? (float)__args[2] : 0f;
            var vy = kind == 3 ? hit.yForce : kind == 6 ? (float)__args[3] : 0f;
            var direction = kind == 4 ? (int)__args[1] : kind == 6 ? (int)__args[5] : 0;
            // kind 6 carries the original damageDelay in x; it is not a hit position.
            var x = kind == 3 ? hit.x : kind == 6 ? (float)__args[4] : __instance.X;
            var y = kind == 3 ? hit.y : __instance.Y;
            SendWorldProjectileContactHit(source, __instance, hit == null ? source : hit.damageSender,
                damage, type, vx, vy, direction, false, x, y, kind, NID.NoID, type);
            return false;
        }

        private static bool IsWorldRemoteHeroAttack(MonoBehaviour actor, BroforceObject target)
        {
            var hero = actor as Unit;
            return _worldReceivingHit == 0 && _worldDamageSource == null && _worldEpoch != 0 && IsWorldSession() && IsWorldObject(target) &&
                hero != null && hero.IsHero && !hero.IsMine;
        }

        private static void ApplyWorldDirectMutation(BroforceObject target, MonoBehaviour actor, byte kind,
            int damage, DamageType type, float vx, float vy, int direction, float x, float y)
        {
            if (kind == 3 && target is Doodad)
            {
                var show = false;
                ((Doodad)target).DamageOptional(new DamageObject(damage, type, vx, vy, x, y, actor), ref show);
            }
            else if (kind == 4 && target is Unit) ((Unit)target).BurnInternal(damage, direction);
            else if (kind == 5 && target is Block) ((Block)target).SetAlight_Local();
            else if (kind == 5 && target is DoodadDestroyable) ((DoodadDestroyable)target).SetAlight_Local();
            else if (kind == 6 && target is Projectile) ((Projectile)target).Damage(damage, type, vx, vy, x, direction);
            else if (kind == 7 && target is Unit) ((Unit)target).Damage(damage, type, vx, vy, direction, actor, x, y);
            else if (kind == 8 && target is Unit) ((Unit)target).GibNow(type, vx, vy);
            else if (kind == 9 && target is Unit)
                AccessTools.Method(target.GetType(), "Gib", new[] { typeof(DamageType), typeof(float), typeof(float) }, null)
                    .Invoke(target, new object[] { type, vx, vy });
        }
    }
}

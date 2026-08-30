using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using Networking;
using UnityEngine;

namespace CustomMapMultiplayer
{
    // 跨官方/Workshop 地图和 Steam/FRP 会话的普通 Mook 死亡与尸体终态收敛。
    internal static partial class HarmonyDiagnostics
    {
        private const float EntityFinalStateStableDelaySeconds = 0.35f;
        private const float EntityFinalStateTerminalTimeoutSeconds = 5f;
        private const float EntityFinalStateStableVelocity = 1f;
        private const float EntityFinalStateMissingObjectTimeoutSeconds = 5f;
        private const float EntityFinalStateDisableGraceSeconds = 2f;
        private const float EntityFinalStateTerminalRetentionSeconds = 15f;
        private const float EntityFinalStatePruneIntervalSeconds = 5f;

        private static readonly Dictionary<NID, EntityFinalState> EntityFinalStates =
            new Dictionary<NID, EntityFinalState>();
        private static readonly Dictionary<NID, PendingEntityDeath> PendingEntityDeaths =
            new Dictionary<NID, PendingEntityDeath>();
        private static readonly Dictionary<NID, PendingEntityTerminal> PendingEntityTerminals =
            new Dictionary<NID, PendingEntityTerminal>();
        private static readonly HashSet<NID> EntityFinalStateSubmissionCandidates =
            new HashSet<NID>();
        private static readonly List<NID> EntityFinalStateCandidatesToRemove =
            new List<NID>();
        private static readonly List<NID> PendingEntityDeathsToRemove =
            new List<NID>();
        private static readonly List<NID> PendingEntityTerminalsToRemove =
            new List<NID>();
        private static readonly List<NID> EntityFinalStatesToRemove =
            new List<NID>();
        private static readonly Dictionary<int, EntityFinalStateMookQualification>
            EntityFinalStateMookQualifications =
                new Dictionary<int, EntityFinalStateMookQualification>();
        private static readonly List<int> EntityFinalStateMookQualificationsToRemove =
            new List<int>();
        private static readonly FieldInfo MookHasDiedField =
            typeof(Mook).GetField(
                "hasDied",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static bool _applyingEntityFinalState;
        private static int _entityFinalStateWarningCount;
        private static float _nextEntityFinalStatePruneAt = float.NegativeInfinity;

        private sealed class EntityFinalState
        {
            internal Mook Mook;
            internal int Sequence;
            internal float DeathAt;
            internal float StableAt;
            internal bool DeathSent;
            internal bool DeathApplied;
            internal bool TerminalSent;
            internal bool TerminalApplied;
            internal float TerminalCompletedAt;
        }

        private sealed class PendingEntityDeath
        {
            internal int Sequence;
            internal float XImpulse;
            internal float YImpulse;
            internal float X;
            internal float Y;
            internal int Health;
            internal int Damage;
            internal DamageType DamageType;
            internal float ExpiresAt;
        }

        private sealed class PendingEntityTerminal
        {
            internal int Sequence;
            internal float X;
            internal float Y;
            internal int Health;
            internal float ExpiresAt;
        }

        private sealed class EntityFinalStateMookQualification
        {
            internal Mook Mook;
            internal bool HasPolymorphicAi;
            internal bool IsBossType;
        }

        private static void PatchEntityFinalStateSynchronization()
        {
            try
            {
                var mookDeath = typeof(Mook).GetMethod(
                    "Death",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    new[] { typeof(float), typeof(float), typeof(DamageObject) },
                    null);
                var deathPostfix = typeof(HarmonyDiagnostics).GetMethod(
                    "EntityMookDeathPostfix",
                    BindingFlags.NonPublic | BindingFlags.Static);
                if (mookDeath == null || deathPostfix == null)
                {
                    DiagnosticLog.Warning("Entity final-state Mook.Death hook could not be resolved.");
                }
                else
                {
                    _harmony.Patch(mookDeath, null, new HarmonyMethod(deathPostfix), null, null);
                }

                var deathRpc = typeof(Unit).GetMethod(
                    "DeathRPC",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    new[] { typeof(float), typeof(float), typeof(float), typeof(float) },
                    null);
                var deathRpcPrefix = typeof(HarmonyDiagnostics).GetMethod(
                    "EntityDeathRpcPrefix",
                    BindingFlags.NonPublic | BindingFlags.Static);
                if (deathRpc == null || deathRpcPrefix == null)
                {
                    DiagnosticLog.Warning("Entity final-state Unit.DeathRPC hook could not be resolved.");
                }
                else
                {
                    _harmony.Patch(deathRpc, new HarmonyMethod(deathRpcPrefix), null, null, null);
                }

                var disable = typeof(DisableWhenOffCamera).GetMethod(
                    "Disable",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    Type.EmptyTypes,
                    null);
                var disablePrefix = typeof(HarmonyDiagnostics).GetMethod(
                    "EntityDisableWhenOffCameraPrefix",
                    BindingFlags.NonPublic | BindingFlags.Static);
                if (disable == null || disablePrefix == null)
                {
                    DiagnosticLog.Warning("Entity final-state DisableWhenOffCamera hook could not be resolved.");
                }
                else
                {
                    _harmony.Patch(disable, new HarmonyMethod(disablePrefix), null, null, null);
                }

                DiagnosticLog.Info(
                    "Entity final-state synchronization enabled for networked ordinary Mooks.");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning("Entity final-state synchronization patch failed: " + exception);
            }
        }

        private static bool IsEntityFinalStateSession()
        {
            return _networkSessionActive && IsOnline() && PID.MyIdHasBeenSet && PID.ServerHasBeenSet;
        }

        private static bool IsEntityFinalStateMook(Mook mook)
        {
            if (!IsEntityFinalStateSession() || mook == null || mook.IsHero || mook.destroyed ||
                mook.mookType == MookType.Vehicle || mook.Nid == NID.NoID)
            {
                return false;
            }

            var instanceId = mook.GetInstanceID();
            EntityFinalStateMookQualification qualification;
            if (!EntityFinalStateMookQualifications.TryGetValue(instanceId, out qualification) ||
                !object.ReferenceEquals(qualification.Mook, mook))
            {
                var typeName = mook.GetType().Name;
                qualification = new EntityFinalStateMookQualification
                {
                    Mook = mook,
                    HasPolymorphicAi = mook.GetComponent<PolymorphicAI>() != null,
                    IsBossType = typeName.IndexOf("Boss", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 typeName.IndexOf("Miniboss", StringComparison.OrdinalIgnoreCase) >= 0
                };
                EntityFinalStateMookQualifications[instanceId] = qualification;
            }

            return qualification.HasPolymorphicAi && !qualification.IsBossType;
        }

        private static bool IsMookDeathComplete(Mook mook)
        {
            if (mook == null)
            {
                return false;
            }

            if (MookHasDiedField == null)
            {
                return mook.actionState == ActionState.Dead && !mook.IsAlive();
            }

            try
            {
                return Convert.ToBoolean(MookHasDiedField.GetValue(mook));
            }
            catch
            {
                return mook.actionState == ActionState.Dead && !mook.IsAlive();
            }
        }

        private static void EntityMookDeathPostfix(
            Mook __instance,
            float xI,
            float yI,
            DamageObject damage)
        {
            try
            {
                if (!IsEntityFinalStateMook(__instance) || !__instance.IsMine || _applyingEntityFinalState)
                {
                    return;
                }

                var nid = __instance.Nid;
                EntityFinalState state;
                if (!EntityFinalStates.TryGetValue(nid, out state))
                {
                    state = new EntityFinalState();
                    EntityFinalStates[nid] = state;
                }

                if (state.DeathSent)
                {
                    state.Mook = __instance;
                    return;
                }

                state.Mook = __instance;
                state.Sequence = state.Sequence < 1 ? 1 : state.Sequence + 1;
                state.DeathAt = Time.unscaledTime;
                state.StableAt = 0f;
                state.DeathSent = true;
                state.DeathApplied = true;
                EntityFinalStateSubmissionCandidates.Add(nid);

                var damageType = damage == null ? DamageType.Normal : damage.damageType;
                var damageAmount = damage == null
                    ? (__instance.health < 0 ? -__instance.health : __instance.health)
                    : damage.damage;
                global::Networking.Networking.RPC<NID, int, float, float, float, float, int, int, DamageType>(
                    PID.TargetOthers,
                    new RpcSignature<NID, int, float, float, float, float, int, int, DamageType>(
                        ApplyEntityDeathRPC),
                    nid,
                    state.Sequence,
                    xI,
                    yI,
                    __instance.X,
                    __instance.Y,
                    __instance.health,
                    damageAmount,
                    damageType,
                    false);

                DiagnosticLog.InfoFileOnly(
                    "ENTITY_FINAL death-owner; nid=" + nid +
                    "; sequence=" + state.Sequence +
                    "; type=" + __instance.GetType().Name +
                    "; health=" + __instance.health +
                    "; position=" + __instance.X + "," + __instance.Y + ".");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning("ENTITY_FINAL death-owner failed: " + exception);
            }
        }

        [AllowedRPC]
        private static void ApplyEntityDeathRPC(
            NID nid,
            int sequence,
            float xI,
            float yI,
            float x,
            float y,
            int health,
            int damage,
            DamageType damageType)
        {
            if (!IsEntityFinalStateSession() || nid == NID.NoID || sequence <= 0)
            {
                return;
            }

            Mook mook = null;
            try
            {
                mook = Registry.GetObject(nid) as Mook;
            }
            catch
            {
            }

            if (mook == null)
            {
                PendingEntityDeath existing;
                if (!PendingEntityDeaths.TryGetValue(nid, out existing) || sequence >= existing.Sequence)
                {
                    PendingEntityDeaths[nid] = new PendingEntityDeath
                    {
                        Sequence = sequence,
                        XImpulse = xI,
                        YImpulse = yI,
                        X = x,
                        Y = y,
                        Health = health,
                        Damage = damage,
                        DamageType = damageType,
                        ExpiresAt = Time.unscaledTime + EntityFinalStateMissingObjectTimeoutSeconds
                    };
                }
                return;
            }

            ApplyEntityDeathToMook(nid, mook, sequence, xI, yI, x, y, health, damage, damageType);
        }

        private static void ApplyEntityDeathToMook(
            NID nid,
            Mook mook,
            int sequence,
            float xI,
            float yI,
            float x,
            float y,
            int health,
            int damage,
            DamageType damageType)
        {
            if (!IsEntityFinalStateMook(mook))
            {
                return;
            }

            EntityFinalState state;
            if (!EntityFinalStates.TryGetValue(nid, out state))
            {
                state = new EntityFinalState();
                EntityFinalStates[nid] = state;
            }

            if (sequence < state.Sequence || (sequence == state.Sequence && state.DeathApplied))
            {
                return;
            }

            state.Mook = mook;
            state.Sequence = sequence;
            state.DeathAt = Time.unscaledTime;
            state.StableAt = 0f;
            state.DeathSent = true;

            SetEntityMookPosition(mook, x, y);
            var alreadyComplete = mook.actionState == ActionState.Dead && IsMookDeathComplete(mook);
            if (!alreadyComplete)
            {
                var previousApplying = _applyingEntityFinalState;
                _applyingEntityFinalState = true;
                try
                {
                    // Prevent the nested native Mook.Death from sending a second death RPC.
                    mook.deathNotificationSent = true;
                    mook.Death(
                        xI,
                        yI,
                        new DamageObject(damage, damageType, xI, yI, x, y, null));
                }
                catch (Exception exception)
                {
                    DiagnosticLog.Warning(
                        "ENTITY_FINAL death-apply exception; nid=" + nid + "; error=" + exception);
                }
                finally
                {
                    _applyingEntityFinalState = previousApplying;
                }
            }

            SetEntityMookPosition(mook, x, y);
            mook.health = health;
            mook.actionState = ActionState.Dead;
            state.DeathApplied = true;

            DiagnosticLog.InfoFileOnly(
                "ENTITY_FINAL death-apply; nid=" + nid +
                "; sequence=" + sequence +
                "; health=" + mook.health +
                "; position=" + mook.X + "," + mook.Y +
                "; complete=" + IsMookDeathComplete(mook) + ".");
        }

        private static bool EntityDeathRpcPrefix(
            Unit __instance,
            float xI,
            float yI,
            float _x,
            float _y)
        {
            var mook = __instance as Mook;
            if (!IsEntityFinalStateMook(mook) || mook.IsMine)
            {
                return true;
            }

            // Native DeathRPC passes a null DamageObject into Mook.Death. Apply the
            // same chain with a non-null context so a lost custom packet cannot leave
            // the remote Mook alive or throw midway through TestVanDammeAnim.Death.
            if (mook.actionState == ActionState.Dead && IsMookDeathComplete(mook))
            {
                DiagnosticLog.Trace(
                    "ENTITY_FINAL native DeathRPC duplicate; nid=" + mook.Nid + ".");
                return false;
            }

            var previousApplying = _applyingEntityFinalState;
            _applyingEntityFinalState = true;
            try
            {
                SetEntityMookPosition(mook, _x, _y);
                mook.deathNotificationSent = true;
                mook.Death(
                    xI,
                    yI,
                    new DamageObject(0, DamageType.Normal, xI, yI, _x, _y, null));
                SetEntityMookPosition(mook, _x, _y);
                mook.health = mook.health > 0 ? 0 : mook.health;
                mook.actionState = ActionState.Dead;

                EntityFinalState state;
                if (!EntityFinalStates.TryGetValue(mook.Nid, out state))
                {
                    state = new EntityFinalState();
                    EntityFinalStates[mook.Nid] = state;
                }

                state.Mook = mook;
                state.DeathAt = Time.unscaledTime;
                state.DeathApplied = true;
                state.DeathSent = false;
                DiagnosticLog.InfoFileOnly(
                    "ENTITY_FINAL native-death-apply; nid=" + mook.Nid +
                    "; health=" + mook.health +
                    "; position=" + mook.X + "," + mook.Y + ".");
            }
            catch (Exception exception)
            {
                mook.health = mook.health > 0 ? 0 : mook.health;
                mook.actionState = ActionState.Dead;
                DiagnosticLog.Warning(
                    "ENTITY_FINAL native-death-apply failed; nid=" + mook.Nid +
                    "; error=" + exception + ".");
            }
            finally
            {
                _applyingEntityFinalState = previousApplying;
            }

            return false;
        }

        private static bool EntityDisableWhenOffCameraPrefix(DisableWhenOffCamera __instance)
        {
            if (!IsEntityFinalStateSession() || __instance == null)
            {
                return true;
            }

            var mook = __instance.GetComponent<Mook>();
            if (!IsEntityFinalStateMook(mook) || mook.actionState != ActionState.Dead)
            {
                return true;
            }

            if (mook.IsMine)
            {
                TrySubmitEntityTerminalBeforeDisable(mook);
                return true;
            }

            EntityFinalState state;
            if (!EntityFinalStates.TryGetValue(mook.Nid, out state) || state.TerminalApplied)
            {
                return true;
            }

            return Time.unscaledTime - state.DeathAt >= EntityFinalStateDisableGraceSeconds;
        }

        [AllowedRPC]
        private static void ApplyEntityCorpseTerminalRPC(
            NID nid,
            int sequence,
            float x,
            float y,
            int health)
        {
            if (!IsEntityFinalStateSession() || nid == NID.NoID || sequence <= 0)
            {
                return;
            }

            Mook mook = null;
            try
            {
                mook = Registry.GetObject(nid) as Mook;
            }
            catch
            {
            }

            if (mook == null || mook.actionState != ActionState.Dead)
            {
                PendingEntityTerminal existing;
                if (!PendingEntityTerminals.TryGetValue(nid, out existing) || sequence >= existing.Sequence)
                {
                    PendingEntityTerminals[nid] = new PendingEntityTerminal
                    {
                        Sequence = sequence,
                        X = x,
                        Y = y,
                        Health = health,
                        ExpiresAt = Time.unscaledTime + EntityFinalStateMissingObjectTimeoutSeconds
                    };
                }
                return;
            }

            ApplyEntityCorpseTerminalToMook(nid, mook, sequence, x, y, health);
        }

        private static void ApplyEntityCorpseTerminalToMook(
            NID nid,
            Mook mook,
            int sequence,
            float x,
            float y,
            int health)
        {
            if (!IsEntityFinalStateMook(mook) || mook.actionState != ActionState.Dead)
            {
                return;
            }

            EntityFinalState state;
            if (!EntityFinalStates.TryGetValue(nid, out state))
            {
                state = new EntityFinalState();
                EntityFinalStates[nid] = state;
            }

            if (sequence < state.Sequence || (sequence == state.Sequence && state.TerminalApplied))
            {
                return;
            }

            state.Mook = mook;
            state.Sequence = sequence;
            state.TerminalApplied = true;
            state.TerminalCompletedAt = Time.unscaledTime;
            SetEntityMookPosition(mook, x, y);
            mook.health = health;
            mook.xI = 0f;
            mook.yI = 0f;

            DiagnosticLog.InfoFileOnly(
                "ENTITY_FINAL corpse-terminal-apply; nid=" + nid +
                "; sequence=" + sequence +
                "; health=" + mook.health +
                "; position=" + mook.X + "," + mook.Y + ".");
        }

        private static void TrySubmitEntityFinalStates()
        {
            if (!IsEntityFinalStateSession() || EntityFinalStateSubmissionCandidates.Count == 0)
            {
                return;
            }

            var now = Time.unscaledTime;
            PerformanceTelemetry.AddItems(
                PerformanceMetric.EntitySubmit,
                EntityFinalStateSubmissionCandidates.Count);
            EntityFinalStateCandidatesToRemove.Clear();
            foreach (var nid in EntityFinalStateSubmissionCandidates)
            {
                EntityFinalState state;
                if (!EntityFinalStates.TryGetValue(nid, out state))
                {
                    EntityFinalStateCandidatesToRemove.Add(nid);
                    continue;
                }

                var mook = state.Mook;
                if (state.TerminalSent || !state.DeathSent || mook == null || !mook.IsMine ||
                    !IsEntityFinalStateMook(mook) || mook.actionState != ActionState.Dead)
                {
                    if (state.TerminalSent || mook == null)
                    {
                        EntityFinalStateCandidatesToRemove.Add(nid);
                    }
                    continue;
                }

                var elapsed = now - state.DeathAt;
                if (elapsed < EntityFinalStateStableDelaySeconds)
                {
                    continue;
                }

                var stable = mook.IsOnGround() &&
                    Mathf.Abs(mook.xI) <= EntityFinalStateStableVelocity &&
                    Mathf.Abs(mook.yI) <= EntityFinalStateStableVelocity;
                if (stable)
                {
                    if (state.StableAt <= 0f)
                    {
                        state.StableAt = now;
                    }
                }
                else
                {
                    state.StableAt = 0f;
                }

                if (elapsed < EntityFinalStateTerminalTimeoutSeconds &&
                    (state.StableAt <= 0f ||
                     now - state.StableAt < EntityFinalStateStableDelaySeconds))
                {
                    continue;
                }

                SubmitEntityCorpseTerminal(nid, state, mook);
                PerformanceTelemetry.Hit(PerformanceMetric.EntitySubmit);
                EntityFinalStateCandidatesToRemove.Add(nid);
            }

            for (var index = 0; index < EntityFinalStateCandidatesToRemove.Count; index++)
            {
                EntityFinalStateSubmissionCandidates.Remove(
                    EntityFinalStateCandidatesToRemove[index]);
            }
        }

        private static void SubmitEntityCorpseTerminal(
            NID nid,
            EntityFinalState state,
            Mook mook)
        {
            state.TerminalSent = true;
            state.TerminalApplied = true;
            state.TerminalCompletedAt = Time.unscaledTime;
            SetEntityMookPosition(mook, mook.X, mook.Y);
            mook.xI = 0f;
            mook.yI = 0f;

            global::Networking.Networking.RPC<NID, int, float, float, int>(
                PID.TargetOthers,
                new RpcSignature<NID, int, float, float, int>(ApplyEntityCorpseTerminalRPC),
                nid,
                state.Sequence,
                mook.X,
                mook.Y,
                mook.health,
                false);

            DiagnosticLog.InfoFileOnly(
                "ENTITY_FINAL corpse-owner-terminal; nid=" + nid +
                "; sequence=" + state.Sequence +
                "; health=" + mook.health +
                "; position=" + mook.X + "," + mook.Y + ".");
        }

        private static void SetEntityMookPosition(Mook mook, float x, float y)
        {
            if (mook == null)
            {
                return;
            }

            mook.SetXY(x, y);
            var position = mook.transform.position;
            mook.transform.position = new Vector3(x, y, position.z);
        }

        private static void TryApplyPendingEntityFinalStates()
        {
            if (!IsEntityFinalStateSession() ||
                (PendingEntityDeaths.Count == 0 && PendingEntityTerminals.Count == 0))
            {
                return;
            }

            var now = Time.unscaledTime;
            PerformanceTelemetry.AddItems(
                PerformanceMetric.EntityPending,
                PendingEntityDeaths.Count + PendingEntityTerminals.Count);
            PendingEntityDeathsToRemove.Clear();
            foreach (var pair in PendingEntityDeaths)
            {
                var pending = pair.Value;
                if (pending.ExpiresAt < now)
                {
                    PendingEntityDeathsToRemove.Add(pair.Key);
                    LogEntityFinalStateWarning(
                        "ENTITY_FINAL death-miss timeout; nid=" + pair.Key + ".");
                    continue;
                }

                Mook mook = null;
                try
                {
                    mook = Registry.GetObject(pair.Key) as Mook;
                }
                catch
                {
                }

                if (mook == null || mook.actionState != ActionState.Dead)
                {
                    continue;
                }

                PendingEntityDeathsToRemove.Add(pair.Key);
                ApplyEntityDeathToMook(
                    pair.Key,
                    mook,
                    pending.Sequence,
                    pending.XImpulse,
                    pending.YImpulse,
                    pending.X,
                    pending.Y,
                    pending.Health,
                    pending.Damage,
                    pending.DamageType);
            }

            for (var index = 0; index < PendingEntityDeathsToRemove.Count; index++)
            {
                PendingEntityDeaths.Remove(PendingEntityDeathsToRemove[index]);
            }

            PendingEntityTerminalsToRemove.Clear();
            foreach (var pair in PendingEntityTerminals)
            {
                var pending = pair.Value;
                if (pending.ExpiresAt < now)
                {
                    PendingEntityTerminalsToRemove.Add(pair.Key);
                    LogEntityFinalStateWarning(
                        "ENTITY_FINAL corpse-terminal-miss timeout; nid=" + pair.Key + ".");
                    continue;
                }

                Mook mook = null;
                try
                {
                    mook = Registry.GetObject(pair.Key) as Mook;
                }
                catch
                {
                }

                if (mook == null)
                {
                    continue;
                }

                PendingEntityTerminalsToRemove.Add(pair.Key);
                ApplyEntityCorpseTerminalToMook(
                    pair.Key,
                    mook,
                    pending.Sequence,
                    pending.X,
                    pending.Y,
                    pending.Health);
            }

            for (var index = 0; index < PendingEntityTerminalsToRemove.Count; index++)
            {
                PendingEntityTerminals.Remove(PendingEntityTerminalsToRemove[index]);
            }
        }

        private static void TryPruneEntityFinalStates()
        {
            if (!IsEntityFinalStateSession())
            {
                return;
            }

            var now = Time.unscaledTime;
            if (now < _nextEntityFinalStatePruneAt)
            {
                return;
            }

            _nextEntityFinalStatePruneAt =
                now + EntityFinalStatePruneIntervalSeconds;
            if (EntityFinalStates.Count == 0)
            {
                return;
            }

            EntityFinalStatesToRemove.Clear();
            PerformanceTelemetry.AddItems(
                PerformanceMetric.EntityPrune,
                EntityFinalStates.Count);
            foreach (var pair in EntityFinalStates)
            {
                var state = pair.Value;
                if ((!state.TerminalSent && !state.TerminalApplied) ||
                    EntityFinalStateSubmissionCandidates.Contains(pair.Key) ||
                    PendingEntityDeaths.ContainsKey(pair.Key) ||
                    PendingEntityTerminals.ContainsKey(pair.Key) ||
                    state.TerminalCompletedAt <= 0f ||
                    now - state.TerminalCompletedAt < EntityFinalStateTerminalRetentionSeconds)
                {
                    continue;
                }

                EntityFinalStatesToRemove.Add(pair.Key);
            }

            EntityFinalStateMookQualificationsToRemove.Clear();
            foreach (var pair in EntityFinalStateMookQualifications)
            {
                var mook = pair.Value.Mook;
                if (mook == null || mook.destroyed)
                {
                    EntityFinalStateMookQualificationsToRemove.Add(pair.Key);
                }
            }

            for (var index = 0; index < EntityFinalStatesToRemove.Count; index++)
            {
                EntityFinalStates.Remove(EntityFinalStatesToRemove[index]);
            }

            for (var index = 0; index < EntityFinalStateMookQualificationsToRemove.Count; index++)
            {
                EntityFinalStateMookQualifications.Remove(
                    EntityFinalStateMookQualificationsToRemove[index]);
            }
        }

        private static void TrySubmitEntityTerminalBeforeDisable(Mook mook)
        {
            if (!IsEntityFinalStateMook(mook) || !mook.IsMine || mook.actionState != ActionState.Dead)
            {
                return;
            }

            EntityFinalState state;
            if (EntityFinalStates.TryGetValue(mook.Nid, out state) && !state.TerminalSent)
            {
                SubmitEntityCorpseTerminal(mook.Nid, state, mook);
                EntityFinalStateSubmissionCandidates.Remove(mook.Nid);
            }
        }

        private static void LogEntityFinalStateWarning(string message)
        {
            if (_entityFinalStateWarningCount++ < 20)
            {
                DiagnosticLog.Warning(message);
            }
        }

        private static void ClearEntityFinalStateSynchronizationState()
        {
            EntityFinalStates.Clear();
            PendingEntityDeaths.Clear();
            PendingEntityTerminals.Clear();
            EntityFinalStateSubmissionCandidates.Clear();
            EntityFinalStateCandidatesToRemove.Clear();
            PendingEntityDeathsToRemove.Clear();
            PendingEntityTerminalsToRemove.Clear();
            EntityFinalStatesToRemove.Clear();
            EntityFinalStateMookQualifications.Clear();
            EntityFinalStateMookQualificationsToRemove.Clear();
            _applyingEntityFinalState = false;
            _entityFinalStateWarningCount = 0;
            _nextEntityFinalStatePruneAt = float.NegativeInfinity;
        }
    }
}

using System.Collections.Generic;
using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace EnemyAIEnhancement
{
    internal static partial class Controller
    {
        private static readonly FieldInfo dogHurt = AccessTools.Field(typeof(MookDog), "damagedDisableAttackTime");
        private static readonly FieldInfo dogScared = AccessTools.Field(typeof(Mook), "scaredTime");
        private static readonly FieldInfo dogHowl = AccessTools.Field(typeof(MookDog), "isHowling");
        private static readonly FieldInfo dogFetch = AccessTools.Field(typeof(DogAI), "fetchObject");
        private static readonly FieldInfo dogCorpse = AccessTools.Field(typeof(DogAI), "attractedToCorpse");
        private static readonly FieldInfo dogStanding = AccessTools.Field(typeof(TestVanDammeAnim), "standingHeadHeight");
        private static readonly FieldInfo hellGrowth = AccessTools.Field(typeof(HellDog), "growingFrames");
        private static readonly FieldInfo hellSpitting = AccessTools.Field(typeof(HellDog), "spittingBabies");
        private static readonly Dictionary<PolymorphicAI, ActionObject> dogCooldowns = new Dictionary<PolymorphicAI, ActionObject>();

        private static bool DogSpecialPhase(DogAI ai, MookDog dog)
        {
            return (bool)faceFirst.GetValue(ai) || ai.mentalState != MentalState.Alerted ||
                dog.IsIncapacitated() || dog.blindTime > 0f || dog.burnTime > 0f || Mathf.Abs(dog.xIBlast) > 20f ||
                (float)dogHurt.GetValue(dog) > 0f || (float)dogScared.GetValue(dog) > 0f || (bool)dogHowl.GetValue(dog) ||
                (bool)faceUsing.GetValue(dog) || (Transform)dogFetch.GetValue(ai) != null || (bool)dogCorpse.GetValue(ai) ||
                HasAction(ai, EnemyActionType.UseSpecial) || HasAction(ai, EnemyActionType.UseSpecial2) ||
                (dog.GetType() == typeof(HellDog) && ((int)hellGrowth.GetValue(dog) > 0 || (bool)hellSpitting.GetValue(dog)));
        }

        private static bool DogCommitted(PolymorphicAI ai)
        {
            ActionObject cooldown;
            if (dogCooldowns.TryGetValue(ai, out cooldown))
            {
                if (ai.actionQueue.Contains(cooldown)) return true;
                dogCooldowns.Remove(ai);
            }
            for (int i = 0; i < ai.actionQueue.Count; i++)
                if (ai.actionQueue[i].type == EnemyActionType.Fire)
                {
                    for (int j = i + 1; j < ai.actionQueue.Count; j++)
                        if (ai.actionQueue[j].type == EnemyActionType.Wait) dogCooldowns[ai] = ai.actionQueue[j];
                    return true;
                }
            return (float)faceHoldFire.GetValue(ai) > 0f;
        }

        private static float DogHeight(MookDog dog)
        { return Mathf.Max(dog.height, Mathf.Max(dog.headHeight, (float)dogStanding.GetValue(dog))); }

        private static bool DogRoute(MookDog dog, float x)
        {
            float width = dog.HalfWidth, height = DogHeight(dog);
            if (Mathf.Min(x, dog.X) - width < 0f || Mathf.Max(x, dog.X) + width >= Map.Width * 16f ||
                dog.Y < 0f || dog.Y + height >= Map.Height * 16f) return false;
            float feet = (float)feetWidth.GetValue(dog);
            for (int c = Map.GetCollumn(Mathf.Min(x, dog.X) - feet); c <= Map.GetCollumn(Mathf.Max(x, dog.X) + feet); c++)
                if (!Map.IsBlockSolid(c, dog.row - 1)) return false;
            Vector3 center = new Vector3(dog.X, dog.Y + 1f + height * 0.5f, 0f);
            Vector3 extents = new Vector3(width, height * 0.5f, 1f);
            Vector3 delta = new Vector3(x - dog.X, 0f, 0f);
            int layers = Map.groundLayer | Map.fragileLayer;
            RaycastHit floor;
            return Physics.Raycast(new Vector3(x, dog.Y + 4f, 0f), Vector3.down, out floor, 8f, layers) &&
                Mathf.Abs(floor.point.y - dog.Y) < 2f && !Physics.CheckBox(center, extents, Quaternion.identity, layers) &&
                !Physics.CheckBox(center + delta, extents, Quaternion.identity, layers) &&
                (delta.sqrMagnitude < 0.01f || !Physics.BoxCast(center, extents, delta.normalized, Quaternion.identity, delta.magnitude, layers));
        }

        private static bool DogTick(DogAI ai, MookDog dog, ref int seenPlayer, bool force)
        {
            Memory memory;
            memories.TryGetValue(ai, out memory);
            if (memory != null && memory.DogFaction != dog.playerNum) { memories.Remove(ai); return false; }
            if (DogSpecialPhase(ai, dog)) return false;
            // Hold and delay can outlive Fire and the attack jump. Never replan them in midair.
            if (DogCommitted(ai)) return true;
            if (!dog.IsOnGround() || (dog.actionState != ActionState.Idle && dog.actionState != ActionState.Running)) return false;
            if (seenPlayer == -1) { memories.Remove(ai); return false; }
            PathAgent agent = ai.GetComponent<PathAgent>();
            if ((memory == null || memory.PathGoal == null) && (HasAction(ai, EnemyActionType.FollowPath) || (agent != null && agent.CurrentPath != null)) ||
                HasAction(ai, EnemyActionType.Jump) || (ai.actionQueue.Count > 0 &&
                (ai.actionQueue[0].type == EnemyActionType.Wait || memory == null))) return false;
            float now = Time.time;
            if (!force && memory != null && now < memory.NextSense) return false;
            int target = seenPlayer;
            bool visible = HeroController.CanSeePlayer(dog.X, dog.Y, 0, ai.sightRangeX, ai.sightRangeY, ref target) &&
                target >= 0 && target < 4 && HeroController.PlayerIsAlive(target) && GameModeController.DoesPlayerNumDamage(dog.playerNum, target);
            Vector2 hero = visible ? (Vector2)HeroController.GetPlayerPosition(target) : Vector2.zero;
            int npc = int.MinValue; float npcX = 0f, npcY = 0f;
            if (Map.CanSeeNPCEnemyUnit(dog.X, dog.Y, ai.FacingDirection, ai.sightRangeX, ai.sightRangeY, dog.playerNum, ref npc, ref npcX, ref npcY) &&
                (!visible || Vector2.Distance(new Vector2(dog.X, dog.Y), new Vector2(npcX, npcY)) < Vector2.Distance(new Vector2(dog.X, dog.Y), hero)))
            { memories.Remove(ai); return false; }
            if (memory == null && !visible) return false;
            memory = GetMemory(ai, dog); memory.NextSense = now + SenseInterval;
            if (Plugin.Settings.AvoidGrenades && TryRetreat(ai, dog, memory)) return true;
            if (visible)
            {
                seenPlayer = target; SampleVisible(memory, target, hero, now);
                if (Mathf.Abs(hero.y - dog.Y) > 16f || Mathf.Abs(hero.x - dog.X) > 18f)
                    return Plugin.Settings.PursueTargets && InterceptPursue(ai, dog, memory, true);
                int direction = hero.x < dog.X ? -1 : 1;
                float jump = ai.jumpWhileAttacking ? (dog.useAttackJumpForceForAttack ? dog.attackJumpForce : dog.jumpForce) : 0f;
                float run = Mathf.Max(dog.speed, dog.useNewFrames ? dog.awareMegaRunSpeed : dog.awareRunSpeed);
                float travel = Mathf.Max(24f, run * (ai.attackTime + Mathf.Max(ai.attackHoldFireDuration + ai.attackHoldFireDelay, 2f * jump / 726f)));
                float rise = jump * jump / 1452f, height = DogHeight(dog);
                if (dog.Y + height + rise >= Map.Height * 16f || !DogRoute(dog, dog.X + direction * travel) ||
                    Physics.CheckBox(new Vector3(dog.X + direction * travel * 0.5f, dog.Y + 1f + (height + rise) * 0.5f, 0f),
                    new Vector3(travel * 0.5f + dog.HalfWidth, (height + rise) * 0.5f, 1f), Quaternion.identity, Map.groundLayer | Map.fragileLayer)) return false;
                Begin(ai, memory, Plan.Attack, 0, direction);
                ai.AddAction(EnemyActionType.FacePoint, new GridPoint(dog.collumn + direction, dog.row), 0f);
                ai.AddAction(EnemyActionType.Fire, ai.attackTime);
                ai.AddAction(EnemyActionType.Wait, Mathf.Max(ai.delayBetweenFiring, ai.attackHoldFireDuration + ai.attackHoldFireDelay));
                dogCooldowns[ai] = ai.actionQueue[ai.actionQueue.Count - 1];
                return true;
            }
            if (Plugin.Settings.RememberTargets && memory.Player >= 0 && HeroController.PlayerIsAlive(memory.Player) && now - memory.SeenAt <= MemorySeconds)
            {
                if (Plugin.Settings.PursueTargets && InterceptPursue(ai, dog, memory, false)) return true;
                Search(ai, dog, memory, force); return true;
            }
            memory.Player = -1; memory.Plan = Plan.None; seenPlayer = -10;
            ai.SetMentalState(MentalState.Suspicious); ai.ClearActionQueue(true); ai.AddAction(EnemyActionType.Wait, 0.3f);
            return true;
        }
    }

    [HarmonyPatch(typeof(DogAI), "DoAlertedThink")]
    internal static class PlanDogCombat
    {
        private static bool Prefix(DogAI __instance, Mook ___mook, ref int ___seenEnemyNum)
        { return ___mook == null || Controller.Kind(__instance, ___mook) != EnemyKind.Dog || !Controller.Tick(__instance, ___mook, ref ___seenEnemyNum, true); }
    }
}

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;
using Rogueforce;
using BroMakerLib;
using BroMakerLib.CustomObjects.Bros;
using BroMakerLib.Loggers;
using UnityEngine;

[assembly: AssemblyTitle("Aquabro")]
[assembly: AssemblyDescription("Aquabro custom character for Broforce")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("Aquabro")]
[assembly: ComVisible(false)]
[assembly: Guid("6a10943c-2f74-4135-9634-87f8396c714b")]
[assembly: AssemblyVersion("0.1.0.0")]
[assembly: AssemblyFileVersion("0.1.0.0")]

namespace AquabroMod
{
    public class AquabroWaterImpact : MonoBehaviour { }

    public class AquabroTridentFlight : MonoBehaviour
    {
        public bool hasPrevious;
        public float previousX, previousY;
    }

    public class AquabroWaterEffect : MonoBehaviour
    {
        private SpriteSM waterSprite;
        private int size, frames;
        private bool charged;
        private float elapsed;
        private Aquabro impactOwner;
        private float impactXI;
        private readonly HashSet<object> affectedUnits = new HashSet<object>();
        // 普通水花沿用原稿节奏；水球爆裂17–22帧以每帧100ms放慢播放，共600ms。
        private static readonly float[] ChargedFrameDurations = {
            .1f, .1f, .1f, .1f, .1f, .1f
        };

        private static int GetAnimationFrame(float time, bool charged)
        {
            int count = charged ? ChargedFrameDurations.Length : 12;
            float end = 0f;
            for (int f = 0; f < count; f++)
            {
                end += charged ? ChargedFrameDurations[f] : (f < 4 ? .035f : .05f);
                if (time < end) return f;
            }
            return -1;
        }

        public static AquabroWaterEffect Play(string path, string texture, float x, float y, int size, int frames,
            bool charged)
        {
            GameObject obj = new GameObject("Aquabro Water Impact");
            obj.transform.position = new Vector3(x, y, -12f);
            SpriteSM sprite = obj.AddComponent<SpriteSM>();
            sprite.meshRender.material = ResourcesController.GetMaterial(path, texture);
            sprite.SetPixelDimensions(size, size);
            sprite.SetSize(charged ? 72f : size, charged ? 72f : size);
            sprite.SetLowerLeftPixel(0, size);
            sprite.RecalcTexture(); sprite.CalcUVs(); sprite.UpdateUVs();
            AquabroWaterEffect effect = obj.AddComponent<AquabroWaterEffect>();
            effect.waterSprite = sprite; effect.size = size; effect.frames = frames;
            effect.charged = charged;
            return effect;
        }

        public void BeginBlast(Aquabro owner, float shotXI, int directDamage, object directTarget)
        {
            impactOwner = owner;
            impactXI = shotXI;
            Aquabro.ApplyWaterImpactUnits(owner, true, transform.position.x, transform.position.y,
                shotXI, directDamage, directTarget, affectedUnits, 0);
        }

        private void Update()
        {
            // 暂停期间既不推进动画，也不结算后来进入的敌人。
            float delta = Time.deltaTime;
            if (delta <= 0f) return;
            elapsed += delta;
            int frame = GetAnimationFrame(elapsed, charged);
            if (frame < 0) { Destroy(gameObject); return; }
            waterSprite.SetLowerLeftPixel(Mathf.Clamp(frame, 0, frames - 1) * size, size);
            if (charged && impactOwner != null)
                Aquabro.ApplyWaterImpactUnits(impactOwner, true, transform.position.x, transform.position.y,
                    impactXI, 0, null, affectedUnits, frame);
        }
    }

    [HeroPreset("Aquabro", HeroType.Rambro)]
    public partial class Aquabro : CustomHero
    {
        private const float FullChargeDuration = 0.5f;
        // 26px 保留为场景破坏与震屏范围；单位改按72px水花当前可见像素。
        private const float WaterBlastRange = 26f;
        private const float WaterBlastDamageRatio = 0.7f;
        private const float ChargedWaterSize = 72f;
        private const float ThrowCooldown = 0.2f;
        private const float ThrowAnimationDuration = 0.182f;
        private const float RunningChargeLoopDuration = 0.64f;
        private const float RunningChargeFrameDuration = 0.04f;
        // v7 起手两帧各 50ms；水流视觉循环共 533ms，收招为 66/50/66ms。
        private static readonly float[] ChargeLoopDurations = {
            0.034f, 0.033f, 0.033f, 0.033f,
            0.034f, 0.033f, 0.033f, 0.033f,
            0.034f, 0.033f, 0.033f, 0.033f,
            0.034f, 0.033f, 0.033f, 0.033f
        };
        private const float LadderFrameRate = 0.1f;
        private const float MeleeSpeedMultiplier = 1.4f;
        // The v2 knife reaches its furthest point on source frame 4. Trigger
        // the inherited hit test when that frame starts so its nominal 22px
        // forward edge matches the weapon tip at about 21.5px.
        private const float MeleeHitTime = 0.16f;
        private const float MeleeDuration = 0.25f;

        private static Projectile normalTridentPrefab;
        private static Projectile chargedTridentPrefab;
        private static SoundHolder tridentSoundHolder;
        private static bool tridentVisualPatchInstalled;

        private float chargeTime;
        private float throwDelay;
        private float throwCooldown;
        private bool tridentPoseActive;
        private bool tridentGunExpanded;
        private Vector2 normalGunPixelDimensions;
        private Vector2 normalGunSize;
        private float tridentGunAnchorShift;
        private float meleeElapsed;
        private bool meleeAttackEmitted;
        private bool specialResolved;
        private bool animationTimeStepReady;
        private int ladderAnimationPhase;

        protected override void Awake()
        {
            if (!tridentVisualPatchInstalled)
            {
                Harmony visualHarmony = new Harmony("gz.aquabro.trident-visual");
                visualHarmony.Patch(
                    typeof(Projectile).GetMethod("Fire"),
                    null, new HarmonyMethod(typeof(Aquabro).GetMethod("ApplyTridentVisual",
                        BindingFlags.NonPublic | BindingFlags.Static)), null, null);
                visualHarmony.Patch(typeof(BrocheteMachete).GetMethod("RunTrail",
                    BindingFlags.NonPublic | BindingFlags.Instance),
                    new HarmonyMethod(typeof(Aquabro).GetMethod("AllowOriginalSpearTrail",
                        BindingFlags.NonPublic | BindingFlags.Static)), null, null, null);
                PatchWaterMethod(visualHarmony, "HitUnits", "WaterHitUnits");
                PatchWaterMethod(visualHarmony, "Bounce", "WaterHitGround");
                PatchWaterMethod(visualHarmony, "MakeEffects", "AllowOriginalHitEffects");
                WaterProjectileBarrier.Install(visualHarmony);
                tridentVisualPatchInstalled = true;
            }
            base.Awake();
            // BroMaker has now assigned Aquabro's materials. Helicopter transport
            // can disable this component before Start, so refresh before it renders.
            RefreshSpriteTextures();
        }

        private static void ApplyTridentVisual(Projectile __instance, MonoBehaviour FiredBy)
        {
            Aquabro owner = FiredBy as Aquabro;
            if (owner == null || !(__instance is PredabroSpear)) return;

            // 保留原版网络 prefab，仅在 Fire 初始化完成后换本次投掷的外观。
            // 同一补丁也处理联机接收端，不修改 Predabro 或共享 prefab。
            SpriteSM spearSprite = __instance.GetComponent<SpriteSM>();
            if (spearSprite == null) return;
            // 原版长矛还有前景武器子层，不能让它叠在自定义完整武器上。
            foreach (Renderer childRenderer in __instance.GetComponentsInChildren<Renderer>(true))
            {
                if (childRenderer.gameObject != __instance.gameObject)
                    childRenderer.enabled = false;
            }
            spearSprite.meshRender.material = ResourcesController.GetMaterial(owner.Info.path, "trident.png");
            spearSprite.SetPixelDimensions(32, 32);
            spearSprite.SetSize(32f, 32f);
            spearSprite.SetLowerLeftPixel(0f, 32f);
            spearSprite.RecalcTexture();
            spearSprite.CalcUVs();
            spearSprite.UpdateUVs();
        }

        private static bool AllowOriginalSpearTrail(BrocheteMachete __instance)
        {
            // 蓄力长矛的残影使用独立原版武器 prefab；海王暂不生成该残影。
            return !(__instance is PredabroSpear) || !(__instance.firedBy is Aquabro);
        }

        private static void PatchWaterMethod(Harmony harmony, string method, string prefix)
        {
            harmony.Patch(typeof(PredabroSpear).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance),
                new HarmonyMethod(typeof(Aquabro).GetMethod(prefix, BindingFlags.NonPublic | BindingFlags.Static)), null, null, null);
        }

        private static bool WaterHitUnits(PredabroSpear __instance)
        {
            Aquabro owner = __instance.firedBy as Aquabro;
            if (owner == null) return true;
            AquabroTridentFlight flight = __instance.GetComponent<AquabroTridentFlight>();
            if (flight == null) flight = __instance.gameObject.AddComponent<AquabroTridentFlight>();
            float direction = Mathf.Sign(__instance.xI);
            // 32px武器沿用原版锚点，非透明戟尖在弹体前方约24px。
            float tipX = __instance.X + direction * 24f, tipY = __instance.Y;
            float fromX = GetTridentSweepStartX(owner.X, __instance.X, direction, flight.hasPrevious, flight.previousX);
            float fromY = flight.hasPrevious ? flight.previousY : tipY;
            float hitX, hitY;
            Unit target = FindTridentContact(owner, __instance.playerNum, fromX, fromY, tipX, tipY, out hitX, out hitY);
            flight.hasPrevious = true; flight.previousX = tipX; flight.previousY = tipY;
            if (target != null) ResolveWaterImpact(__instance, owner, hitX, hitY, target);
            else
            {
                RaycastHit wall;
                if (FindTridentTerrainContact(fromX, fromY, tipX, tipY, out wall))
                    WaterHitGround(__instance, wall);
            }
            return false;
        }

        private static float GetTridentSweepStartX(float ownerX, float shotX, float direction, bool hasPrevious, float previousX)
        {
            if (hasPrevious) return previousX;
            // 首次出生在角色前14px，补齐身体到出生点的盲区；不向角色身后扩大。
            float offset = (shotX - ownerX) * direction;
            return offset >= 0f && offset <= 40f ? ownerX : shotX;
        }

        internal static Unit FindTridentContact(MonoBehaviour owner, int playerNum, float fromX, float fromY,
            float toX, float toY, out float hitX, out float hitY)
        {
            Unit closest = null;
            float nearest = float.PositiveInfinity;
            hitX = toX; hitY = toY;
            RaycastHit wall;
            float length = new Vector3(toX - fromX, toY - fromY, 0f).magnitude;
            if (length > .00001f && FindTridentTerrainContact(fromX, fromY, toX, toY, out wall))
                nearest = wall.distance / length;
            if (Map.units == null) return null;
            foreach (Unit target in Map.units)
            {
                if (target == null || target == owner || target.health <= 0 || target.invulnerable ||
                    !GameModeController.DoesPlayerNumDamage(playerNum, target.playerNum)) continue;
                TestVanDammeAnim character = target as TestVanDammeAnim;
                float top = character != null ? target.Y + character.headHeight : target.Y + target.height * 2f + 3f;
                float halfWidth = target.width * .5f;
                // 戟尖上下三像素的实体区域；头部采用角色当前headHeight，兼容重装兵和蹲姿。
                float minX = target.X - halfWidth, maxX = target.X + halfWidth;
                float minY = target.Y - 3f, maxY = top + 3f;
                float enter = 0f, leave = 1f;
                if (!ClipTridentAxis(fromX, toX - fromX, minX, maxX, ref enter, ref leave) ||
                    !ClipTridentAxis(fromY, toY - fromY, minY, maxY, ref enter, ref leave) || enter >= nearest) continue;
                nearest = enter; closest = target;
                hitX = fromX + (toX - fromX) * enter;
                hitY = Mathf.Clamp(fromY + (toY - fromY) * enter, target.Y, top);
            }
            return closest;
        }

        private static bool FindTridentTerrainContact(float fromX, float fromY, float toX, float toY,
            out RaycastHit nearest)
        { return FindTridentTerrainContactExcept(fromX, fromY, toX, toY, null, out nearest); }

        private static bool FindTridentTerrainContactExcept(float fromX, float fromY, float toX, float toY,
            Collider receiver, out RaycastHit nearest)
        {
            nearest = new RaycastHit();
            Vector3 path = new Vector3(toX - fromX, toY - fromY, 0f);
            float length = path.magnitude;
            if (length <= .00001f) return false;
            float distance = float.PositiveInfinity;
            bool found = false;
            foreach (RaycastHit hit in Physics.RaycastAll(new Vector3(fromX, fromY, 0f), path.normalized,
                length, Map.groundLayer | Map.groundAndDamageableObjects))
            {
                Collider contact = hit.collider;
                if (contact == null || !contact.enabled || !contact.gameObject.activeInHierarchy || contact.isTrigger ||
                    contact.GetComponentInParent<Unit>() != null) continue;
                DamageRelay relay = contact.GetComponent<DamageRelay>();
                if (relay != null && relay.unit != null) continue;
                if (contact == receiver) continue;
                CollisionDamageTarget obstacle = CollisionDamageTarget.Find(contact);
                CollisionDamageTarget target = CollisionDamageTarget.Find(receiver);
                if (obstacle != null && target != null && obstacle.Key == target.Key) continue;
                if (hit.distance >= distance) continue;
                distance = hit.distance;
                nearest = hit;
                found = true;
            }
            return found;
        }

        private static bool ClipTridentAxis(float origin, float delta, float min, float max, ref float enter, ref float leave)
        {
            if (Mathf.Abs(delta) < .00001f) return origin >= min && origin <= max;
            float a = (min - origin) / delta, b = (max - origin) / delta;
            if (a > b) { float swap = a; a = b; b = swap; }
            enter = Mathf.Max(enter, a); leave = Mathf.Min(leave, b);
            return enter <= leave;
        }

        private static bool WaterHitGround(PredabroSpear __instance, RaycastHit raycastHit)
        {
            Aquabro owner = __instance.firedBy as Aquabro;
            if (owner == null) return true;
            if (__instance.GetComponent<AquabroWaterImpact>() != null) return false;
            if (CollisionDamageTarget.Find(raycastHit.collider) != null)
            {
                ResolveWaterImpact(__instance, owner, raycastHit.point.x + raycastHit.normal.x * .05f,
                    raycastHit.point.y + raycastHit.normal.y * .05f, raycastHit.collider);
                return false;
            }
            if (owner.IsMine && Mathf.Abs(__instance.xI) <= 500f)
                MapController.DamageGround(__instance, __instance.damage, DamageType.Crush, 16f,
                    raycastHit.point.x + raycastHit.normal.x * 3f,
                    raycastHit.point.y + raycastHit.normal.y * 3f,
                    CollisionDamageTarget.IgnoreForGround(raycastHit.point.x + raycastHit.normal.x * 3f,
                        raycastHit.point.y + raycastHit.normal.y * 3f, 16f), false);
            // Start the splash just outside the struck face so its visibility
            // rays cannot begin inside the wall and miss that same obstacle.
            ResolveWaterHit(__instance, owner, raycastHit.point.x + raycastHit.normal.x * .05f,
                raycastHit.point.y + raycastHit.normal.y * .05f);
            return false;
        }

        private static bool AllowOriginalHitEffects(PredabroSpear __instance, bool particles, float x, float y)
        {
            Aquabro owner = __instance.firedBy as Aquabro;
            if (owner == null) return true;
            if (!particles) ResolveWaterHit(__instance, owner, x, y);
            return false;
        }

        private static void ResolveWaterHit(PredabroSpear shot, Aquabro owner, float x, float y)
        {
            // CheckSpawnPoint can invoke MakeEffects before HitUnits, with the
            // spawn point already inside a nearby wall. Keep that splash on the
            // first visible face instead of starting its rays inside the wall.
            AquabroTridentFlight flight = shot.GetComponent<AquabroTridentFlight>();
            object directContact = null;
            if (flight == null || !flight.hasPrevious)
            {
                float fromX = GetTridentSweepStartX(owner.X, shot.X, Mathf.Sign(shot.xI), false, 0f);
                RaycastHit wall;
                if (FindTridentTerrainContact(fromX, shot.Y, x, y, out wall))
                {
                    x = wall.point.x + wall.normal.x * .05f;
                    y = wall.point.y + wall.normal.y * .05f;
                    if (CollisionDamageTarget.Find(wall.collider) != null) directContact = wall.collider;
                }
            }
            ResolveWaterImpact(shot, owner, x, y, directContact);
        }

        private static void ResolveWaterImpact(PredabroSpear shot, Aquabro owner, float x, float y, object directTarget)
        {
            if (shot.GetComponent<AquabroWaterImpact>() != null) return;
            shot.gameObject.AddComponent<AquabroWaterImpact>();
            bool charged = Mathf.Abs(shot.xI) > 500f;
            float radius = charged ? WaterBlastRange : 12f;
            float damageRadius = charged ? radius * WaterBlastDamageRatio : radius;
            int blastDamage = charged ? 14 : 8;
            AquabroWaterEffect effect = AquabroWaterEffect.Play(owner.Info.path, charged ? "water-rocket-explosion.png" : "water-hit-small.png",
                x, y, charged ? 64 : 48, charged ? 6 : 12, charged);
            if (owner.IsMine)
            {
                // 与 Rocket.MakeEffects 相同的范围破土、场景物件和弹丸破坏调用。
                if (charged)
                    MapController.DamageGround(shot, ValueOrchestrator.GetModifiedDamage(blastDamage, shot.playerNum),
                        DamageType.Explosion, damageRadius, x, y, CollisionDamageTarget.IgnoreForGround(x, y, damageRadius), false);
                DamageWaterDoodads(shot, charged ? blastDamage : shot.damageInternal,
                    charged ? DamageType.Explosion : shot.damageType, x, y,
                    charged ? 0f : shot.xI, charged ? 0f : shot.yI,
                    charged ? damageRadius * 0.6f : 8f);
                if (charged)
                {
                    // 原版 HitProjectiles 还会额外扩张弹体尺寸；改为严格圆形中心判定。
                    foreach (Projectile targetProjectile in new List<Projectile>(Map.damageableProjectiles))
                    {
                        if (targetProjectile == null || !GameModeController.DoesPlayerNumDamage(shot.playerNum, targetProjectile.playerNum)) continue;
                        float px = targetProjectile.X - x, py = targetProjectile.Y - y;
                        if (px * px + py * py <= damageRadius * damageRadius &&
                            WaterSceneContactVisible(x, y, targetProjectile.X, targetProjectile.Y, targetProjectile.gameObject))
                            Map.DamageProjectile(targetProjectile, blastDamage, DamageType.Explosion, 0f, 0f, 0.25f, shot.playerNum);
                    }
                    // 树木接口使用矩形判定，缩为圆的内接正方形。
                    float boxRange = radius * 0.70710678f;
                    Map.ShakeTrees(x, y, boxRange, boxRange, 60f);
                }
                if (charged) effect.BeginBlast(owner, shot.xI, shot.damageInternal, directTarget);
                else ApplyWaterImpactUnits(owner, false, x, y, shot.xI, shot.damageInternal,
                    directTarget, new HashSet<object>(), 0);
            }
            if (charged && owner.IsMine)
            {
                // 原版位置震屏可传播数百像素；这里仅在本地角色位于水花范围内时震屏。
                float viewerDistance = Vector2.Distance(new Vector2(owner.X, owner.Y + 6f), new Vector2(x, y));
                if (viewerDistance < radius) SortOfFollow.Shake(0.2f * (1f - viewerDistance / radius));
            }
            UnityEngine.Object.Destroy(shot.gameObject);
        }

        private static bool WaterSceneContactVisible(float x, float y, float hitX, float hitY, GameObject receiver)
        {
            Vector3 path = new Vector3(hitX - x, hitY - y, 0f);
            float length = path.magnitude;
            if (length <= .00001f) return true;
            foreach (RaycastHit hit in Physics.RaycastAll(new Vector3(x, y, 0f), path.normalized,
                Mathf.Max(0f, length - .01f), Map.groundLayer | Map.groundAndDamageableObjects))
            {
                Collider contact = hit.collider;
                if (contact == null || !contact.enabled || !contact.gameObject.activeInHierarchy || contact.isTrigger ||
                    contact.GetComponentInParent<Unit>() != null) continue;
                DamageRelay relay = contact.GetComponent<DamageRelay>();
                if (relay != null && relay.unit != null) continue;
                if (receiver != null && (contact.gameObject == receiver || contact.transform.IsChildOf(receiver.transform))) continue;
                return false;
            }
            return true;
        }

        private static void DamageWaterDoodads(PredabroSpear shot, int damage, DamageType type,
            float x, float y, float forceX, float forceY, float range)
        {
            // Preserve Map.DamageDoodads eligibility and native reactions, with
            // a visibility check before the damage can reach scenery behind a wall.
            for (int i = Map.destroyableDoodads.Count - 1; i >= 0; i--)
            {
                Doodad target = Map.destroyableDoodads[i];
                if (target == null || (shot.playerNum < 0 ? !target.CanBeDamagedByMooks : target.immuneToHeroDamage) ||
                    !target.IsPointInRange(x, y, range) ||
                    !WaterSceneContactVisible(x, y, target.centerX, target.centerY, target.gameObject)) continue;
                bool hit = false;
                target.DamageOptional(new DamageObject(damage, type, forceX, forceY, x, y, shot), ref hit);
            }
        }

        internal static void ApplyWaterImpactUnits(Aquabro owner, bool charged, float x, float y,
            float shotXI, int directDamage, object directTarget, HashSet<object> affected, int frame)
        {
            if (owner == null || !owner.IsMine) return;
            HashSet<object> partTargets = ApplyWaterImpactContacts(owner, charged, x, y, shotXI, directDamage, directTarget as Collider, affected, frame);
            float radius = charged ? WaterBlastRange : 12f;
            float damageRadius = charged ? ChargedWaterSize * .5f * WaterBlastDamageRatio : radius;
            int blastDamage = charged ? 14 : 8;
            // 原版初筛也只看脚底+6，会漏掉仅头部/身体边缘碰水的大型目标。
            List<Unit> targets = charged
                ? (Map.units == null ? new List<Unit>() : new List<Unit>(Map.units))
                : Map.GetUnitsInRange(radius, radius, x, y, false);
            Unit directUnit = directTarget as Unit;
            if (directUnit != null && !targets.Contains(directUnit)) targets.Add(directUnit);
            foreach (Unit target in targets)
            {
                if (target == null || target.health <= 0 || target.invulnerable ||
                    !GameModeController.DoesPlayerNumDamage(owner.playerNum, target.playerNum) ||
                    affected.Contains(target) || partTargets.Contains(target)) continue;
                float dx = target.X - x, dy = target.Y + 6f - y;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float contactDistance = distance;
                bool contact = charged && FindChargedWaterContact(target, frame, x, y, out contactDistance);
                if (charged ? (!contact && target != directTarget) : (distance > radius && target != directTarget)) continue;
                if (!charged || !contact)
                {
                    RaycastHit wall;
                    if (FindTridentTerrainContact(x, y, target.X, target.Y + 6f, out wall)) continue;
                }
                // 透明处或墙后不登记；剩余动画中接触可见水体仍有一次机会。
                affected.Add(target);
                int direction = dx == 0f ? (int)Mathf.Sign(shotXI) : (int)Mathf.Sign(dx);
                int damage = contactDistance <= damageRadius ? blastDamage : 0;
                // 戟尖直接碰撞独立于爆心距离；不能把确实命中的头部当成零伤害外圈。
                if (target == directTarget)
                    damage = System.Math.Max(directDamage, blastDamage);
                float forceX = distance < 0.001f ? direction * 100f : dx / distance * 400f;
                float forceY = (distance < 0.001f ? 0f : dy / distance * 400f) + 260f;
                // 保证向上震飞，并越过普通士兵的上抛/翻滚阈值；不改 canTumble 等原版规则。
                if (charged) forceY = Mathf.Max(forceY, Mathf.Max(260f, Mathf.Abs(forceX) + 40f));
                else { forceX = direction * 110f; forceY = 90f; }
                Map.KnockAndDamageUnit(owner, target, damage > 0 ? ValueOrchestrator.GetModifiedDamage(damage, owner.playerNum) : 0,
                    charged ? DamageType.Explosion : DamageType.Melee, forceX, forceY, direction, true, x, y, true);
            }
        }

        private static HashSet<object> ApplyWaterImpactContacts(Aquabro owner, bool charged, float x, float y,
            float shotXI, int directDamage, Collider direct, HashSet<object> affected, int frame)
        {
            List<Collider> contacts = new List<Collider>();
            HashSet<object> partTargets = new HashSet<object>();
            // Direct contact has priority over the splash and connected parts.
            if (direct != null) contacts.Add(direct);
            contacts.AddRange(Physics.OverlapSphere(new Vector3(x, y, 0f), charged ? 52f : 12f,
                Map.groundLayer | Map.groundAndDamageableObjects));
            foreach (Collider contact in contacts)
            {
                DamageType type = charged ? DamageType.Explosion : DamageType.Crush;
                CollisionDamageTarget unitRoute = charged ? null : CollisionDamageTarget.Find(contact, DamageType.Melee);
                CollisionDamageTarget boss;
                if (unitRoute != null && unitRoute.UnitTarget != null) { boss = unitRoute; type = DamageType.Melee; }
                else boss = CollisionDamageTarget.Find(contact, type);
                if (boss != null && boss.UnitTarget != null) partTargets.Add(boss.UnitTarget);
                if (boss == null || object.ReferenceEquals(boss.UnitTarget, owner) ||
                    (boss.UnitTarget != null && boss.UnitTarget.health <= 0) ||
                    !boss.CanDamage(owner.playerNum, type) || CollisionDamageTarget.AlreadyHit(boss.Key, affected)) continue;
                Bounds bounds = contact.bounds;
                float hitX = Mathf.Clamp(x, bounds.min.x, bounds.max.x);
                float hitY = Mathf.Clamp(y, bounds.min.y, bounds.max.y);
                float dx = hitX - x, dy = hitY - y;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                bool directHit = contact == direct;
                if (!directHit)
                {
                    if (charged)
                    {
                        if (!FindChargedWaterContactRect(bounds.min.x, bounds.min.y, bounds.max.x, bounds.max.y,
                            frame, x, y, contact, out distance)) continue;
                    }
                    else
                    {
                        RaycastHit wall;
                        if (distance > 12f || FindTridentTerrainContactExcept(x, y, hitX, hitY, contact, out wall)) continue;
                    }
                }
                int damage = charged ? (distance <= ChargedWaterSize * .5f * WaterBlastDamageRatio ? 14 : 0) : 8;
                if (directHit) damage = System.Math.Max(directDamage, charged ? 14 : 8);
                // Boss parts do not receive the infantry's outer-ring launch.
                // A harmless outer-ring touch must not consume later inner contact.
                if (damage <= 0 && boss.UnitTarget == null) continue;
                CollisionDamageTarget.Register(boss.Key, affected);
                float forceX = 0f, forceY = 0f;
                if (boss.UnitTarget != null)
                {
                    // A unit reached through a relay keeps the same infantry
                    // launch as the body pass; scene receivers receive no launch.
                    float ux = boss.UnitTarget.X - x, uy = boss.UnitTarget.Y + 6f - y;
                    float length = Mathf.Sqrt(ux * ux + uy * uy);
                    float direction = ux == 0f ? Mathf.Sign(shotXI) : Mathf.Sign(ux);
                    forceX = charged ? (length < .001f ? direction * 100f : ux / length * 400f) : direction * 110f;
                    forceY = charged ? Mathf.Max((length < .001f ? 0f : uy / length * 400f) + 260f,
                        Mathf.Max(260f, Mathf.Abs(forceX) + 40f)) : 90f;
                }
                MapController.Damage_Networked(owner, contact.gameObject,
                    damage > 0 ? ValueOrchestrator.GetModifiedDamage(damage, owner.playerNum) : 0, type, forceX, forceY, hitX, hitY);
            }
            return partTargets;
        }

        private static bool FindChargedWaterContact(Unit target, int frame, float x, float y, out float distance)
        {
            TestVanDammeAnim character = target as TestVanDammeAnim;
            float left = target.X - target.width * .5f, right = target.X + target.width * .5f;
            float bottom = target.Y;
            float top = character != null ? target.Y + character.headHeight : target.Y + target.height * 2f + 3f;
            return FindChargedWaterContactRect(left, bottom, right, top, frame, x, y, null, out distance);
        }

        private static bool FindChargedWaterContactRect(float left, float bottom, float right, float top,
            int frame, float x, float y, Collider receiver, out float distance)
        {
            distance = float.PositiveInfinity;
            const float pixel = ChargedWaterSize / 64f;
            float half = ChargedWaterSize * .5f;
            if (right < x - half || left > x + half || top < y - half || bottom > y + half) return false;
            int minX = System.Math.Max(0, (int)System.Math.Floor((left - x + half) / pixel) - 1);
            int maxX = System.Math.Min(63, (int)System.Math.Floor((right - x + half) / pixel));
            int minY = System.Math.Max(0, (int)System.Math.Floor((y + half - top) / pixel) - 1);
            int maxY = System.Math.Min(63, (int)System.Math.Floor((y + half - bottom) / pixel));
            for (int py = minY; py <= maxY; py++)
            {
                float overlapBottom = Mathf.Max(bottom, y + half - (py + 1) * pixel);
                float overlapTop = Mathf.Min(top, y + half - py * pixel);
                if (overlapBottom > overlapTop) continue;
                for (int px = minX; px <= maxX; px++)
                {
                    if (!ChargedWaterPixelMask.IsVisible(frame, px, py)) continue;
                    float overlapLeft = Mathf.Max(left, x - half + px * pixel);
                    float overlapRight = Mathf.Min(right, x - half + (px + 1) * pixel);
                    if (overlapLeft > overlapRight) continue;
                    float contactX = Mathf.Clamp(x, overlapLeft, overlapRight);
                    float contactY = Mathf.Clamp(y, overlapBottom, overlapTop);
                    float dx = contactX - x, dy = contactY - y;
                    float candidate = Mathf.Sqrt(dx * dx + dy * dy);
                    if (candidate >= distance) continue;
                    RaycastHit wall;
                    if (FindTridentTerrainContactExcept(x, y, contactX, contactY, receiver, out wall)) continue;
                    distance = candidate;
                }
            }
            return !float.IsPositiveInfinity(distance);
        }

        protected override void Start()
        {
            base.Start();
            runningFrameRate = 0.08f;
            meleeType = MeleeType.Custom;
            currentMeleeType = MeleeType.Custom;
            useNewKnifeClimbingFrames = true;
            // 保留基类旧式爬梯状态；外观统一由正式竖梯源稿的腿部与上半身提供。
            useNewLadderClimbingFrames = false;
            useLadderClimbingTransition = false;
            RefreshSpriteTextures();
            RefreshSkillHud();
            try
            {
                SetupTridentPrefabs();
            }
            catch (Exception ex)
            {
                BMLogger.ExceptionLog(ex, true);
            }
        }

        private void RefreshSpriteTextures()
        {
            // BroMaker replaces the renderer materials after the base hero is
            // created, but SpriteSM can still cache the original Rambro texture.
            // Refresh after Awake's material swap, then again when Start runs.
            if (sprite != null)
            {
                sprite.RecalcTexture();
                sprite.CalcUVs();
                sprite.UpdateUVs();
            }

            if (gunSprite != null)
            {
                gunSprite.RecalcTexture();
                gunSprite.CalcUVs();
                gunSprite.UpdateUVs();
            }
        }

        private void SetupTridentPrefabs()
        {
            if (normalTridentPrefab != null && chargedTridentPrefab != null)
                return;

            Predabro predabro = HeroController.GetHeroPrefab(HeroType.Predabro) as Predabro;
            if (predabro == null)
                throw new InvalidOperationException("Could not find the Predabro prefab.");

            // 网络生成只接受已登记的原版 prefab；克隆后停用的对象既不在
            // PrefabList 中，也不会由 Projectile.Fire 自动激活。
            // 直接复用 Predabro，保留普通/蓄力长矛的伤害、贯穿、破土和特效。
            normalTridentPrefab = predabro.projectile;
            chargedTridentPrefab = predabro.chargedSpearProectilePrefab;
            tridentSoundHolder = predabro.soundHolder;
            if (normalTridentPrefab == null || chargedTridentPrefab == null)
                throw new InvalidOperationException("Could not find the Predabro spear prefabs.");
        }

        protected override void UseFire()
        {
            // Release is handled in RunFiring so holding fire can build charge.
        }

        protected override void RunFiring()
        {
            if (health <= 0)
            {
                chargeTime = 0f;
                throwDelay = 0f;
                throwCooldown = 0f;
                return;
            }

            if (throwDelay > 0f)
                throwDelay = Mathf.Max(0f, throwDelay - t);
            if (throwCooldown > 0f)
                throwCooldown = Mathf.Max(0f, throwCooldown - t);

            // 小刀键动作独占身体和武器姿态，持火不覆盖前刺。
            if (doingMelee)
            {
                chargeTime = 0f;
                throwDelay = 0f;
                throwCooldown = 0f;
                // Advance the custom knife timeline independently of global
                // time so only this animation runs at the requested speed.
                meleeElapsed += Mathf.Max(t, 0f) * MeleeSpeedMultiplier;
                if (!meleeAttackEmitted && meleeElapsed + 0.00001f >= MeleeHitTime)
                {
                    // Custom melee bypasses AnimateMeleeCommon's held-key throw.
                    // Hand off before the stab, including frames that skip past it.
                    if (highFive && nearbyMook != null && nearbyMook.CanBeThrown())
                    {
                        CancelMelee();
                        ThrowBackMook(nearbyMook);
                        nearbyMook = null;
                        return;
                    }
                    meleeAttackEmitted = true;
                    PerformKnifeMeleeAttack(true, true);
                }
                if (meleeElapsed + 0.00001f >= MeleeDuration)
                {
                    frame = 0;
                    counter = 0f;
                    CancelMelee();
                    ChangeFrame();
                }
                return;
            }

            if (IsIncapacitated() || isOnHelicopter || throwingHeldObject ||
                impaledByTransform != null || strungUpBy != null ||
                inseminatorUnit != null || hasBeenCoverInAcid ||
                usingSpecial || usingPockettedSpecial || usingSpecial2 ||
                usingSpecial3 || usingSpecial4 ||
                currentGesture != GestureElement.Gestures.None)
            {
                chargeTime = 0f;
                throwDelay = 0f;
                throwCooldown = 0f;
                return;
            }

            // 与 Predabro 一样保留基类的开火计时、翻滚取消和头像反馈。
            base.RunFiring();

            if (throwCooldown <= 0f && fire)
            {
                // 投掷后等待200ms，间隔结束才开始下一次蓄力。
                throwDelay = 0f;
                ActivateGun();
                chargeTime += t;
                // 普通、跑步、跳跃与梯上姿态在 Update 最后应用对应的新攻击动作。
                if (!CanShowTridentPose())
                {
                    // 完整地形姿势自带手臂和武器，不再叠加旧蓄力贴图。
                    DeactivateGun();
                }
            }

            if (throwCooldown <= 0f && wasFire && !fire && chargeTime > 0f)
            {
                FireTrident();
                fireDelay = 0.4f;
                throwDelay = ThrowAnimationDuration;
                throwCooldown = ThrowCooldown;
                chargeTime = 0f;
                if (!CanShowTridentPose())
                {
                    SetGunPosition(0f, 0f);
                    SetGunSprite(0, 0);
                }
            }
        }

        private bool CanShowTridentPose()
        {
            return health > 0 && !IsIncapacitated() && !doingMelee &&
                !isOnHelicopter && !throwingHeldObject && rollingFrames <= 0 &&
                impaledByTransform == null && strungUpBy == null &&
                inseminatorUnit == null && !hasBeenCoverInAcid &&
                !usingSpecial && !usingPockettedSpecial && !usingSpecial2 &&
                !usingSpecial3 && !usingSpecial4 &&
                currentGesture == GestureElement.Gestures.None &&
                !WallDrag && !wallClimbing &&
                !ledgeGrapple && !chimneyFlip &&
                (actionState == ActionState.Idle || actionState == ActionState.Running ||
                 actionState == ActionState.Jumping || actionState == ActionState.ClimbingLadder);
        }

        private static int GetChargeAnimationFrame(float elapsed)
        {
            if (elapsed < 0.05f) return 1;
            if (elapsed < FullChargeDuration) return 2;

            // 视觉循环在源稿两帧起手后开始；满蓄力独立按 0.5 秒判定。
            float loopTime = (elapsed - FullChargeDuration) % 0.533f;
            for (int i = 0; i < ChargeLoopDurations.Length; i++)
            {
                if (loopTime < ChargeLoopDurations[i]) return 3 + i;
                loopTime -= ChargeLoopDurations[i];
            }
            return 3;
        }

        private static int GetRunningChargeAnimationFrame(float elapsed)
        {
            if (elapsed < 0.05f) return 1;
            if (elapsed < FullChargeDuration) return 2;
            float loopTime = (elapsed - FullChargeDuration) % RunningChargeLoopDuration;
            return 3 + Mathf.Clamp(Mathf.FloorToInt(loopTime / RunningChargeFrameDuration), 0, 15);
        }

        private static int GetJumpAttackContext(float horizontalVelocity, float verticalVelocity)
        {
            // 前进/原地与上升/下降共四种腿型；原跳跃稿同组的三个腿帧逐像素相同。
            return (Mathf.Abs(horizontalVelocity) > 50f ? 0 : 2) +
                (verticalVelocity > 20f ? 0 : 1);
        }

        private bool AnimateTridentAttack()
        {
            if (CanShowTridentPose() && (chargeTime > 0f || throwDelay > 0f))
            {
                // 滑索在游戏中仍是 Jumping，必须先识别附着状态，再选择绳索蓄力稿。
                bool onZipline = attachedToZipline != null;
                bool running = !onZipline && actionState == ActionState.Running && !ducking;
                bool jumping = !onZipline && actionState == ActionState.Jumping;
                bool climbingLadder = !onZipline && actionState == ActionState.ClimbingLadder;
                int attackFrame;
                if (throwDelay > 0f)
                {
                    float elapsed = ThrowAnimationDuration - throwDelay;
                    attackFrame = elapsed < 0.066f ? 19 : (elapsed < 0.116f ? 20 : 21);
                }
                else
                    attackFrame = (onZipline || running || jumping || climbingLadder) ? GetRunningChargeAnimationFrame(chargeTime) :
                        GetChargeAnimationFrame(chargeTime);

                tridentPoseActive = true;
                SetSpriteOffset(0f, 0f);
                // 上半身按攻击计时选帧，滑索腿部跟随原版入绳/跳跃的六个阶段。
                int pose = attackFrame - 1;
                int mode = 0;
                int bob = 0;
                int bodyCell = TridentAtlas.StandingLegs[pose];
                if (onZipline)
                {
                    mode = 4;
                    bodyCell = TridentAtlas.ZiplineLegs[Mathf.Clamp(frame, 0, 5)];
                }
                else if (running)
                {
                    int phase = Mathf.Max(frame, 0) % 8;
                    mode = 1;
                    bodyCell = TridentAtlas.RunningLegs[phase];
                    bob = TridentAtlas.RunningBob[phase];
                }
                else if (jumping || climbingLadder)
                {
                    int phase = jumping ? GetJumpAttackContext(xI, yI) : Mathf.Clamp(ladderAnimationPhase, 0, 3);
                    if (jumping && pose == 20)
                    {
                        // 恢复持戟复用普通跳跃的身体与手臂，二者一起右移1像素。
                        SetMovementFrame(TridentAtlas.JumpingRecovery[phase], 1f);
                        return true;
                    }
                    mode = jumping ? 2 : 3;
                    bodyCell = (jumping ? TridentAtlas.JumpingLegs : TridentAtlas.LadderLegs)[phase];
                    if (climbingLadder) bob = TridentAtlas.LadderBob[phase];
                }
                SetBodyFrame(bodyCell);
                SetTridentUpperFrame(mode * 21 + pose, bob);
                ActivateGun();
                return true;
            }

            if (tridentPoseActive)
            {
                tridentPoseActive = false;
                RestoreTridentGunSize();
                SetSpriteOffset(0f, 0f);
                SetGunPosition(0f, 0f);
                gunFrame = 0;
                SetGunSprite(0, 0);
                if ((actionState != ActionState.Running || ducking) &&
                    actionState != ActionState.Jumping && actionState != ActionState.ClimbingLadder)
                {
                    frame = 0;
                    counter = 0f;
                }
                ChangeFrame();
            }
            return false;
        }

        private void SetTridentUpperFrame(int index, int bob)
        {
            if (gunSprite == null) return;
            if (!tridentGunExpanded)
            {
                normalGunPixelDimensions = gunSprite.pixelDimensions;
                normalGunSize = new Vector2(gunSprite.width, gunSprite.height);
                tridentGunExpanded = true;
                // 35像素上半身占两个32像素图集格，完整保留右侧水流。
                gunSprite.SetPixelDimensions(35, 32);
                gunSprite.SetSize(35f, 32f);
                switch (gunSprite.anchor)
                {
                    case SpriteBase.ANCHOR_METHOD.UPPER_CENTER:
                    case SpriteBase.ANCHOR_METHOD.MIDDLE_CENTER:
                    case SpriteBase.ANCHOR_METHOD.BOTTOM_CENTER:
                        tridentGunAnchorShift = (35f - normalGunSize.x) * 0.5f;
                        break;
                    case SpriteBase.ANCHOR_METHOD.UPPER_RIGHT:
                    case SpriteBase.ANCHOR_METHOD.MIDDLE_RIGHT:
                    case SpriteBase.ANCHOR_METHOD.BOTTOM_RIGHT:
                        tridentGunAnchorShift = 35f - normalGunSize.x;
                        break;
                    default:
                        tridentGunAnchorShift = 0f;
                        break;
                }
            }
            SetGunPosition(TridentAtlas.UpperFrames[index, 1] + tridentGunAnchorShift,
                -TridentAtlas.UpperFrames[index, 2] - bob);
            SetGunAtlasCell(TridentAtlas.UpperFrames[index, 0]);
        }

        private void RestoreTridentGunSize()
        {
            if (!tridentGunExpanded || gunSprite == null) return;
            gunSprite.SetPixelDimensions(normalGunPixelDimensions);
            gunSprite.SetSize(normalGunSize.x, normalGunSize.y);
            tridentGunExpanded = false;
        }

        protected override bool CanStartNewMelee()
        {
            return !ControllingShark && !doingMelee && health > 0 && stunTime <= 0f && frozenTime <= 0f &&
                !isOnHelicopter && impaledByTransform == null && strungUpBy == null &&
                inseminatorUnit == null && !throwingHeldObject && !hasBeenCoverInAcid &&
                !usingSpecial && !usingPockettedSpecial &&
                !usingSpecial2 && !usingSpecial3 && !usingSpecial4;
        }

        protected override void StartMelee()
        {
            StartCustomMelee();
        }

        protected override void StartCustomMelee()
        {
            if (!CanStartNewMelee())
                return;

            currentMeleeType = MeleeType.Custom;
            meleeElapsed = 0f;
            meleeAttackEmitted = false;
            chargeTime = 0f;
            throwDelay = 0f;
            throwCooldown = 0f;
            jumpingMelee = false;
            jumpTime = 0f;
            StartMeleeCommon();
        }

        protected override void AnimateCustomMelee()
        {
            SetSpriteOffset(0f, 0f);
            frameRate = 0.05f;
            int phase = meleeElapsed < 0.04f ? 0 :
                (meleeElapsed < 0.08f ? 1 :
                (meleeElapsed < MeleeHitTime ? 2 : 3));
            bool airAttack = jumpingMelee;
            int sourceFrame = airAttack ? 4 + phase : phase;
            SetBodyFrame(airAttack ? 608 + phase : (ducking ? 177 : 25) + phase);
            // The 39px source canvas is cropped into a 32px gun cell per frame.
            // Restore the crop origin here so the weapon stays at its source
            // position and the thrust tip is not clipped by the atlas cell.
            float[] weaponCropOrigins = { 0f, 0f, 2f, 6f };
            SetGunPosition(weaponCropOrigins[phase], 0f);
            SetGunAtlasCell(airAttack ? 132 + sourceFrame : 128 + sourceFrame);
            ActivateGun();
        }

        protected override void RunCustomMeleeMovement()
        {
            // 沿用旧版近战移动：空中正常下落，地面允许半速移动。
            jumpTime = 0f;
            if (Y > groundHeight + 1f)
            {
                ApplyFallingGravity();
                if (yI < maxFallSpeed) yI = maxFallSpeed;
            }
            else
                xI = (left ? -1f : (right ? 1f : 0f)) * speed * 0.5f;
        }

        protected override void CancelMelee()
        {
            if (doingMelee) jumpTime = 0f;
            meleeElapsed = 0f;
            meleeAttackEmitted = false;
            SetGunPosition(0f, 0f);
            SetGunSprite(0, 0);
            DeactivateGun();
            base.CancelMelee();
        }

        private void SetBodyFrame(int atlasSlot)
        {
            int column = atlasSlot % 32;
            int row = atlasSlot / 32;
            // Body assets are packed from the top of the texture, while
            // SpriteSM's pixel Y coordinate is converted to bottom-origin UVs.
            sprite.SetLowerLeftPixel(column * spritePixelWidth,
                (row + 1) * spritePixelHeight);
        }

        private void SetMovementFrame(int bodySlot, float xOffset)
        {
            SetBodyFrame(bodySlot);
            SetSpriteOffset(xOffset, 0f);
            SyncMovementWeapon(xOffset);
        }

        private void SyncMovementWeapon(float xOffset)
        {
            if (gunSprite == null) return;
            int bodySlot = CurrentBodyFrame();
            for (int i = 0; i < TridentAtlas.MovementFrames.GetLength(0); i++)
            {
                if (TridentAtlas.MovementFrames[i, 0] != bodySlot) continue;
                RestoreTridentGunSize();
                SetGunPosition(TridentAtlas.MovementFrames[i, 2] + xOffset,
                    -TridentAtlas.MovementFrames[i, 3]);
                gunSprite.transform.localScale = Vector3.one;
                SetGunAtlasCell(TridentAtlas.MovementFrames[i, 1]);
                ActivateGun();
                return;
            }
        }

        protected override void SetGunSprite(int spriteFrame, int spriteRow)
        {
            RestoreTridentGunSize();
            if (gunSprite == null) return;
            // 基类的帧号对应已删除的旧武器行；只按当前身体配对正式源稿的武器层。
            int bodyFrame = CurrentBodyFrame();
            if (bodyFrame >= 64 && bodyFrame <= 72)
            {
                SetGunPosition(0f, 0f);
                gunSprite.transform.localScale = Vector3.one;
                SetGunAtlasCell(bodyFrame);
                ActivateGun();
                return;
            }
            // 其余完整身体帧自带手臂和武器；匹配普通移动帧时再显示独立武器。
            DeactivateGun();
            SyncMovementWeapon(0f);
        }

        private int CurrentBodyFrame()
        {
            if (sprite == null || spritePixelWidth <= 0 || spritePixelHeight <= 0)
                return -1;

            Vector2 pixel = sprite.lowerLeftPixel;
            return (Mathf.RoundToInt(pixel.y / spritePixelHeight) - 1) * 32 +
                Mathf.RoundToInt(pixel.x / spritePixelWidth);
        }

        private void SetGunAtlasCell(int cell)
        {
            if (gunSprite == null) return;
            if (cell < 160) RestoreTridentGunSize();
            gunSprite.SetLowerLeftPixel((cell % 32) * gunSpritePixelWidth,
                (cell / 32 + 1) * gunSpritePixelHeight);
        }

        private void SyncCrouchWeapon()
        {
            // 按实际身体帧同步：梯顶切换时可能已显示蹲姿，但状态仍为爬梯、ducking 为 false。
            // 攻击、近战和特殊动作保留各自武器姿态。
            if (health <= 0 || fire || throwDelay > 0f || gunFrame > 0 ||
                doingMelee || rollingFrames > 0 || usingSpecial || usingPockettedSpecial ||
                usingSpecial2 || usingSpecial3 || usingSpecial4 ||
                currentGesture != GestureElement.Gestures.None ||
                (actionState != ActionState.Idle && actionState != ActionState.Running &&
                 actionState != ActionState.ClimbingLadder))
                return;

            int bodyFrame = CurrentBodyFrame();
            if (bodyFrame < 64 || bodyFrame > 72) return;
            // 两层来自同一原稿的同一帧，槽号、画布锚点一致，不做一格回退。
            ActivateGun();
            SetGunPosition(0f, 0f);
            SetGunAtlasCell(bodyFrame);
        }

        protected override void SetDeltaTime()
        {
            base.SetDeltaTime();
            animationTimeStepReady = t > 0f;
        }

        protected override void SetHighFiveBoostDeltaTime()
        {
            base.SetHighFiveBoostDeltaTime();
            animationTimeStepReady = t > 0f;
        }

        protected override void Update()
        {
            // TestMod 冻结时会跳过基类 Update；只有本帧重算过 t 才推进自定义动画。
            // 保留上一帧的 t，让基类 SetDeltaTime 正常记录移动平台所需的 lastT。
            animationTimeStepReady = false;
            base.Update();
            RefreshSkillHud();
            if (!animationTimeStepReady)
                return;
            TickSkillPreview(Mathf.Max(0f, t));
            // 基类换帧、移动和落地可能覆盖姿态，最后统一写入本次攻击帧。
            if (AnimateTridentAttack() || health <= 0)
                return;
            if (doingMelee)
                AnimateCustomMelee();
            SyncCrouchWeapon();
            // 基类移动逻辑可能再次改写武器位置，最后恢复源稿中的手臂对齐。
            if (CanShowTridentPose())
            {
                if (actionState == ActionState.ClimbingLadder)
                    SyncLadderWeapon();
                else
                    SyncMovementWeapon(0f);
            }
        }

        private void SetTraversalFrame(int frame)
        {
            SetBodyFrame(512 + Mathf.Clamp(frame, 0, 85));
        }

        private void RemapWallFrame(bool climbing)
        {
            int nativeFrame = CurrentBodyFrame();
            if (nativeFrame >= 76 && nativeFrame <= 80)
            {
                SetTraversalFrame((climbing ? 28 : 0) + nativeFrame - 76);
                return;
            }

            if (nativeFrame >= 86 && nativeFrame <= 90)
            {
                SetTraversalFrame((climbing ? 38 : 5) + nativeFrame - 86);
                return;
            }

            if (climbing && nativeFrame >= 81 && nativeFrame <= 85)
            {
                SetTraversalFrame(33 + nativeFrame - 81);
                return;
            }

            if (climbing && nativeFrame >= 91 && nativeFrame <= 95)
                SetTraversalFrame(43 + nativeFrame - 91);
        }

        // 保留基类移动与四阶段计时，普通爬绳也使用蓄力/投掷稿中的持戟姿势。
        protected override void AnimateClimbingLadder()
        {
            base.AnimateClimbingLadder();
            // 离开梯顶后仍保留基类选择的地面身体和手臂。
            if (!IsNearbyLadder(transform.localScale.x * 6f, 22f))
            {
                SyncMovementWeapon(0f);
                return;
            }
            // The legacy ladder path advances at the base-game cadence. Aquabro's
            // larger body changes read too quickly at that rate, so slow only the
            // visual ladder-frame interval while preserving ladder movement/state.
            frameRate = LadderFrameRate;
            // 停在绳上保留最后的裤腿阶段，继续移动才推进四帧循环。
            if (up || down || left || right)
                ladderAnimationPhase = Mathf.Max(frame, 0) % 4;

            SyncLadderWeapon();
        }

        private void SyncLadderWeapon()
        {
            if (!CanShowTridentPose()) return;
            int bodyFrame = CurrentBodyFrame();
            if ((bodyFrame >= 64 && bodyFrame <= 72) ||
                !IsNearbyLadder(transform.localScale.x * 6f, 22f))
            {
                SyncMovementWeapon(0f);
                return;
            }

            // 普通爬绳使用投掷稿第5帧“恢复持戟”；蓝色水流由蓄力计时选帧。
            int phase = Mathf.Clamp(ladderAnimationPhase, 0, 3);
            SetSpriteOffset(0f, 0f);
            SetBodyFrame(TridentAtlas.LadderLegs[phase]);
            gunSprite.transform.localScale = Vector3.one;
            SetTridentUpperFrame(3 * 21 + 20, TridentAtlas.LadderBob[phase]);
            ActivateGun();
        }

        protected override void AnimateHanging()
        {
            DeactivateGun();
            base.AnimateHanging();
            int nativeFrame = CurrentBodyFrame();
            if (nativeFrame >= 107 && nativeFrame <= 124)
            {
                SetTraversalFrame(10 + nativeFrame - 107);
            }
            DeactivateGun();
        }

        protected override void AnimateWallDrag()
        {
            DeactivateGun();
            base.AnimateWallDrag();
            RemapWallFrame(false);
            DeactivateGun();
        }

        protected override void AnimateWallAnticipation()
        {
            DeactivateGun();
            base.AnimateWallAnticipation();
            RemapWallFrame(true);
            DeactivateGun();
        }

        protected override void AnimateWallClimb()
        {
            DeactivateGun();
            base.AnimateWallClimb();
            RemapWallFrame(true);
            DeactivateGun();
        }

        protected override void AnimateLedgeGrapple()
        {
            DeactivateGun();
            base.AnimateLedgeGrapple();
            RemapWallFrame(true);
            DeactivateGun();
        }

        protected override void AnimateZipline()
        {
            DeactivateGun();
            base.AnimateZipline();
            int nativeFrame = CurrentBodyFrame();
            if (nativeFrame >= 107 && nativeFrame <= 124)
            {
                SetTraversalFrame(68 + nativeFrame - 107);
            }
            DeactivateGun();
        }

        private void FireTrident()
        {
            bool charged = chargeTime >= FullChargeDuration;
            Projectile prefab = charged ? chargedTridentPrefab : normalTridentPrefab;
            if (prefab == null)
                return;

            float x = X + transform.localScale.x * 14f;
            float y = Y + (ducking ? 8f : 11f);
            float speed = transform.localScale.x * 400f * (charged ? 1.66f : 1f);
            if (IsMine)
            {
                ProjectileController.SpawnProjectileOverNetwork(prefab, this, x, y, speed, 0f, false, playerNum, false, false, 0f);
            }

            PlayAttackSound();
            Map.DisturbWildLife(X, Y, 120f, playerNum);
        }

        protected override void PlayAttackSound(float volume)
        {
            if (tridentSoundHolder == null)
            {
                base.PlayAttackSound(volume);
                return;
            }

            SoundHolder originalSoundHolder = soundHolder;
            try
            {
                soundHolder = tridentSoundHolder;
                base.PlayAttackSound(volume);
            }
            finally
            {
                soundHolder = originalSoundHolder;
            }
        }

        public override void AnimateActualIdleFrames()
        {
            frameRate = 0.0667f;
            SetMovementFrame(0, 0f);
        }

        protected override void AnimateActualNewRunningFrames()
        {
            frameRate = runningFrameRate;
            SetMovementFrame(32 + frame % 8, 0f);
        }

        protected override void AnimateActualJumpingFrames()
        {
            // 兰博在 yI < -55 时把手臂再抬高1像素；身体保留顶点/初降姿势。
            if (yI < -55f)
            {
                int firstFrame = Mathf.Abs(xI) > 50f ? 114 : 117;
                SetMovementFrame(firstFrame + Mathf.Max(frame, 0) % 3, 0f);
                return;
            }
            int context = GetJumpAttackContext(xI, yI);
            int startFrame = context < 2 ? context * 3 : (context + 1) * 3;
            SetMovementFrame(96 + startFrame + Mathf.Max(frame, 0) % 3, 0f);
        }

        protected override void AnimateActualJumpingDuckingFrames()
        {
            // 原版空中蹲姿读取身体格8/9，海王这两个格为空白。
            // 使用海王完整跳跃身体及配套手臂，保持空中按下键时可见。
            AnimateActualJumpingFrames();
        }

        public override void AnimateActualIdleDuckingFrames()
        {
            SetSpriteOffset(0f, 0f);
            SetGunPosition(0f, 0f);
            SetBodyFrame(64);
            SetGunAtlasCell(64);
            ActivateGun();
        }

        protected override void AnimateActualDuckingRunning()
        {
            SetSpriteOffset(0f, 0f);
            SetGunPosition(0f, 0f);
            frameRate = 0.08f;
            int bodyFrame = 64 + 1 + frame % 8;
            SetBodyFrame(bodyFrame);
            SetGunAtlasCell(bodyFrame);
            ActivateGun();
        }

    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using Rogueforce;
using BroMakerLib;

namespace AquabroMod
{
    // Preview selection is per local player and survives respawn.
    public partial class Aquabro
    {
        private static readonly int[] SkillSelections = new int[4];
        private static readonly string[] SkillNames = { "怒海狂潮", "深海猎杀", "深海漩涡", "深海涌泉" };
        internal static readonly string[] SkillAssets = { "tidal-wave", "shark-hunt", "maelstrom", "abyssal-spring" };
        private readonly List<AquabroSkillVisual> skillVisuals = new List<AquabroSkillVisual>();
        private int castingSkill;
        internal int CurrentAcidStage;
        private static readonly System.Random AcidRandom = new System.Random();
        internal static bool RollsCorrosion(int stage, double roll)
        { return stage >= 2 || (stage == 1 && roll < .5); }
        private DamageType SkillDamageType(Unit target, int damage)
        {
            TestVanDammeAnim character = target as TestVanDammeAnim;
            // Native Acid damage coats compatible characters instead of subtracting
            // health. Vehicles, boss parts and acid-resistant creatures keep their
            // normal damage path; never force the coating flag or call Melt directly.
            if (damage <= 0 || CurrentAcidStage <= 0 || character == null || target.health <= 0 ||
                !character.canBeCoveredInAcid || character is Alien ||
                (character is AlienMosquito && !(character is HellLostSoul))) return DamageType.Explosion;
            // Further poison-water hits must not explode the coated enemy's skeleton
            // or knock it out of its native panic/melting sequence.
            return character.HasBeenCoveredInAcid() || RollsCorrosion(CurrentAcidStage, AcidRandom.NextDouble()) ?
                DamageType.Acid : DamageType.Explosion;
        }
        private AquabroSkillVisual activeShark;
        private bool sharkCameraTarget;
        private bool sharkDropInput;
        private int hudSkill = -1;
        private Material skillHudMaterial;
        private List<Material> skillHudMaterials;
        public int SelectedSkill { get { return SkillSelections[Mathf.Clamp(playerNum, 0, 3)]; } }
        private bool ControllingShark { get { return activeShark != null && activeShark.SharkControlled; } }
        private bool SharkSkillActive { get { return ControllingShark || (usingSpecial && castingSkill == 1); } }
        private bool CanStartShark
        {
            get
            {
                if (WallDrag || wallClimbing || ledgeGrapple || attachedToZipline != null) return false;
                if (actionState != ActionState.Idle && actionState != ActionState.Running &&
                    actionState != ActionState.ClimbingLadder) return false;
                // Ladder tops/platforms support a cast too. While climbing, check
                // contact without IsOverLadder's changes to the hero's ladder state.
                return IsGroundBelow() || IsGroundBelowAtXOffset(0f, true) ||
                    (actionState == ActionState.ClimbingLadder && IsNearbyLadder(0f, 6f));
            }
        }

        internal bool CanContinueShark { get { return health > 0 && !IsIncapacitated() && gameObject.activeInHierarchy; } }

        private static float SharkControlDuration()
        {
            // Dredd's control ends with the rocket's serialized lifetime, not a hero timer.
            BroDredd dredd = HeroController.GetHeroPrefab(HeroType.BroDredd) as BroDredd;
            return dredd != null && dredd.specialRocketPrefab != null && dredd.specialRocketPrefab.life > 0f ?
                dredd.specialRocketPrefab.life : 4f;
        }

        protected override void CheckInput()
        {
            bool wasControlling = ControllingShark;
            base.CheckInput();
            if (!wasControlling && !ControllingShark) return;
            if (ControllingShark)
            {
                sharkDropInput = down;
                activeShark.SetSharkInput(left, right);
            }
            // Keep special/wasSpecial intact so holding the launch key cannot trigger a breach.
            left = right = up = down = buttonJump = fire = highFive = buttonGesture = false;
        }

        protected override void CalculateMovement()
        {
            if (!SharkSkillActive || health <= 0 || IsIncapacitated()) { base.CalculateMovement(); return; }
            // Keep the base game's collision movement, but bypass jump/dash input.
            jumpTime = 0f;
            xI = 0f;
            // Skipping base.CalculateMovement also skips its Running -> Idle transition.
            // The stationary hero must not keep a running pose while steering the shark.
            if (actionState == ActionState.Running)
            {
                actionState = ActionState.Idle;
                frame = 0;
            }
        }

        protected override void RunMovement()
        {
            if (!SharkSkillActive || health <= 0 || IsIncapacitated()) { base.RunMovement(); return; }
            bool savedUp = up, savedDown = down, savedJump = buttonJump;
            bool savedWall = canWallClimb, savedLedge = canLedgeGrapple, savedCeiling = canCeilingHang;
            up = down = buttonJump = false;
            canWallClimb = canLedgeGrapple = canCeilingHang = false;
            WallDrag = wallClimbing = wallClimbAnticipation = ledgeGrapple = false;
            try { base.RunMovement(); }
            finally
            {
                up = savedUp; down = savedDown; buttonJump = savedJump;
                canWallClimb = savedWall; canLedgeGrapple = savedLedge; canCeilingHang = savedCeiling;
            }
        }

        internal void ReleaseSharkControl(AquabroSkillVisual effect)
        {
            if (activeShark != effect) return;
            // Reuse Dredd's native input handoff: base.CheckInput consumes this
            // delay after remote control ends, independently of the effect lifetime.
            if (CanContinueShark)
                controllingProjectileDelay = Mathf.Max(controllingProjectileDelay, 0.25f);
            ReleaseSharkCamera();
        }

        private void ReleaseSharkCamera()
        {
            if (sharkCameraTarget) HeroController.RemoveTemporaryPlayerTarget(playerNum);
            sharkCameraTarget = false;
        }

        internal void EndShark(AquabroSkillVisual effect)
        {
            if (activeShark != effect) return;
            activeShark = null;
            // Death/disable can end the effect before a normal breach.
            ReleaseSharkCamera();
        }

        public override void SetGestureAnimation(GestureElement.Gestures gesture)
        {
            // The original input sends this RPC once per rising edge, rather than
            // once per animation loop. Do not hook TriggerFlexEvent (fires twice).
            if (gesture == GestureElement.Gestures.Flex && health > 0 &&
                !usingSpecial && !usingPockettedSpecial && !doingMelee && !IsIncapacitated() && !ControllingShark)
            {
                int slot = Mathf.Clamp(playerNum, 0, 3);
                SkillSelections[slot] = (SkillSelections[slot] + 1) % SkillNames.Length;
                chargeTime = throwDelay = 0f;
            }
            base.SetGestureAnimation(gesture);
            RefreshSkillHud();
        }

        private void RefreshSkillHud()
        {
            if (!IsMine || playerNum < 0 || playerNum >= 4 || HeroController.players == null ||
                playerNum >= HeroController.players.Length) return;
            Player player = HeroController.players[playerNum];
            if (player == null || player.character != this || player.hud == null || player.hud.grenadeIcons == null) return;
            if (hudSkill != SelectedSkill || skillHudMaterial == null)
            {
                Material material = ResourcesController.GetMaterial(Info.path, "special-" + SkillAssets[SelectedSkill] + ".png");
                if (material == null) return;
                skillHudMaterial = material;
                skillHudMaterials = new List<Material> { material };
                hudSkill = SelectedSkill;
            }
            // Keep BroMaker's later HUD refreshes on this player's selected skill.
            CurrentSpecialMaterials = skillHudMaterials;
            foreach (SpriteSM icon in player.hud.grenadeIcons)
            {
                if (icon == null || icon.meshRender.sharedMaterial == skillHudMaterial) continue;
                icon.meshRender.sharedMaterial = skillHudMaterial;
                icon.RecalcTexture();
                icon.SetPixelDimensions(16, 16);
                icon.SetLowerLeftPixel(0, 16);
                icon.CalcUVs();
                icon.UpdateUVs();
            }
            // Match the native HUD's 10px icon spacing while leaving room by the avatar.
            float offset = playerNum % 2 == 0 ? 3f : -3f;
            foreach (SpriteSM icon in player.hud.grenadeIcons)
                if (icon != null) icon.SetOffset(new Vector3(offset, 0f, 0f));
            CurrentSpecialMaterialOffset = new Vector2(3f, 0f);
        }

        protected override void PressSpecial()
        {
            if (ControllingShark)
            {
                if (health > 0 && !IsIncapacitated()) activeShark.BreachShark();
                return;
            }
            if (health <= 0 || IsIncapacitated() || doingMelee || usingSpecial ||
                (SelectedSkill == 1 && activeShark != null)) return;
            if (SelectedSkill == 1 && !CanStartShark) return;
            if (SpecialAmmo <= 0) { HeroController.FlashSpecialAmmo(playerNum); return; }
            castingSkill = SelectedSkill;
            specialResolved = false;
            chargeTime = throwDelay = 0f;
            SetGestureAnimation(GestureElement.Gestures.None);
            RestoreTridentGunSize();
            SetSpriteOffset(0f, 0f);
            SetGunPosition(0f, 0f);
            base.PressSpecial();
            // 起手立即切换完整施法帧，避免下一次换帧前仍显示已清零偏移的独立手臂。
            if (usingSpecial) AnimateSpecial();
        }

        protected override void UseSpecial()
        {
            if (specialResolved || health <= 0) return;
            specialResolved = true;
            // Recheck at release: stepping off an edge during the wind-up spends no ammo.
            if (castingSkill == 1 && !CanStartShark) return;
            if (SpecialAmmo <= 0) { HeroController.FlashSpecialAmmo(playerNum); return; }
            SpecialAmmo--;
            TriggerBroSpecialEvent();
            float facing = transform.localScale.x < 0f ? -1f : 1f;
            switch (castingSkill)
            {
                case 0:
                    AddSkillVisual(0, X, Y, facing, AquabroSkillVisual.TidalLifetime, 0f, 0.85f);
                    break;
                case 1:
                    sharkDropInput = false;
                    float sharkY;
                    if (!FindSharkFloor(X, Y, false, out sharkY)) sharkY = Y;
                    activeShark = new AquabroSkillVisual(this, 1, X, sharkY, facing, SharkControlDuration(), 0f, 1f);
                    skillVisuals.Add(activeShark);
                    usingSpecial = usingPockettedSpecial = false;
                    pressSpecialFacingDirection = 0;
                    // UseSpecial can run before this tick's movement calculation.
                    // Choose idle immediately when ChangeFrame restores the hero pose.
                    if (actionState == ActionState.Running) actionState = ActionState.Idle;
                    frame = 0;
                    xI = 0f;
                    if (IsMine && playerNum >= 0 && playerNum < 4)
                    {
                        HeroController.AddTemporaryPlayerTarget(playerNum, activeShark.CameraTarget);
                        sharkCameraTarget = true;
                    }
                    ActivateGun();
                    ChangeFrame();
                    break;
                case 2:
                    Vector2 center = FindMaelstromDestination(facing);
                    AddSkillVisual(2, center.x, center.y, facing, AquabroSkillVisual.MaelstromLifetime, 0f, 0.85f);
                    break;
                case 3:
                    StartSpringColumns();
                    break;
            }
            PlayAttackSound(0.8f);
        }

        protected override void AnimateSpecial()
        {
            DeactivateGun();
            SetSpriteOffset(0f, 0f);
            frameRate = 0.065f;
            // 狂潮删除了两个重复恢复帧，仍保留每个技能的八格图集区间。
            int lastCastFrame = castingSkill == 0 ? 5 : 7;
            SetBodyFrame(384 + castingSkill * 8 + Mathf.Clamp(frame, 0, lastCastFrame));
            if (frame >= 2) UseSpecial();
            if (frame >= lastCastFrame)
            {
                frame = 0;
                usingSpecial = usingPockettedSpecial = false;
                pressSpecialFacingDirection = 0;
                ActivateGun();
                ChangeFrame();
            }
        }

        private void StartSpringColumns()
        {
            HashSet<object> sharedHits = new HashSet<object>();
            SpringDamageState sharedSpring = new SpringDamageState();
            for (int i = -3; i <= 3; i++)
            {
                Vector2 pos;
                if (!FindSpringColumn(X + i * 30f, out pos)) continue;
                AddSkillVisual(3, pos.x, pos.y, i < 0 ? -1f : 1f, 1.4f,
                    Mathf.Abs(i) * 0.14f, Mathf.Abs(i) == 3 ? 0.9f : 0.65f, sharedHits, sharedSpring);
            }
            SortOfFollow.Shake(0.25f, new Vector3(X, Y, 0f));
        }

        private bool FindSpringColumn(float destinationX, out Vector2 position)
        {
            float x = X, y = Y;
            RaycastHit floor, wall;
            // Preserve the centre's original 12px-up / 55px-down ground search.
            if (RaycastSpringTerrain(new Vector3(x, y + 12f, 0f), Vector3.down, 55f, out floor)) y = floor.point.y;
            float distance = destinationX - x;
            int steps = System.Math.Max(1, (int)System.Math.Ceiling(Mathf.Abs(distance) / 4f));
            float step = distance / steps;
            float direction = step < 0f ? -1f : 1f;
            position = new Vector2(x, y);
            for (int i = 0; i < steps && Mathf.Abs(step) > .00001f; i++)
            {
                float next = x + step;
                // A one-tile lip is traversable terrain, not a wall cancelling
                // all three columns. Follow its top, but stop at taller faces.
                if (RaycastSpringTerrain(new Vector3(x, y + 18f, 0f), new Vector3(direction, 0f, 0f),
                    Mathf.Abs(step), out wall)) return false;
                if (RaycastSpringTerrain(new Vector3(next, y + 18f, 0f), Vector3.down, 61f, out floor))
                {
                    if (floor.point.y > y + 16f) return false;
                    y = floor.point.y;
                }
                x = next;
            }
            position = new Vector2(destinationX, y);
            return true;
        }

        internal bool ValidSkillTarget(Unit target)
        {
            return ValidSkillDamageTarget(target) && target.health > 0;
        }

        private bool ValidSkillDamageTarget(Unit target)
        {
            // Corpses still receive normal damage so the game's gib rules apply.
            return target != null && target != this && target.gameObject.activeInHierarchy &&
                !target.invulnerable && GameModeController.DoesPlayerNumDamage(playerNum, target.playerNum);
        }

        internal bool SkillLineOfSight(float x, float y, Unit target)
        {
            return SkillContactVisible(x, y, target.X, target.Y + 6f, false);
        }

        private static bool SkillContactVisible(float x, float y, float hitX, float hitY, bool ignoreProps)
        { return SkillContactVisible(x, y, hitX, hitY, ignoreProps, null); }

        // Water may overlap a wall visually, but cannot act through that wall.
        // Use the full segment even for contacts closer than eight pixels.
        internal static bool WaterContactVisible(float x, float y, float hitX, float hitY, Collider receiver)
        {
            Vector3 start = new Vector3(x, y, 0f);
            Vector3 diff = new Vector3(hitX - x, hitY - y, 0f);
            if (diff.magnitude <= .00001f) return true;
            foreach (RaycastHit hit in Physics.RaycastAll(start, diff.normalized,
                Mathf.Max(0f, diff.magnitude - .01f), Map.groundLayer))
            {
                Collider contact = hit.collider;
                if (contact == null || !contact.enabled || !contact.gameObject.activeInHierarchy ||
                    contact.isTrigger || contact == receiver ||
                    (receiver != null && contact.gameObject == receiver.gameObject) ||
                    contact.GetComponentInParent<Unit>() != null || contact.GetComponent<DamageRelay>() != null ||
                    contact.GetComponentInParent<FallingBlock>() != null ||
                    contact.GetComponentInParent<Cage>() != null || contact.GetComponent<Cage>() != null ||
                    contact.GetComponent<CageBar>() != null || contact.GetComponent<SawBlade>() != null ||
                    (contact.GetComponentInParent<Doodad>() != null && contact.GetComponentInParent<Elevator>() == null)) continue;
                return false;
            }
            return true;
        }

        private static bool SkillContactVisible(float x, float y, float hitX, float hitY, bool ignoreProps, Collider receiver)
        {
            Vector3 diff = new Vector3(hitX - x, hitY - y, 0f);
            if (diff.magnitude < (receiver == null ? 8f : .00001f)) return true;
            foreach (RaycastHit hit in Physics.RaycastAll(new Vector3(x, y, 0f), diff.normalized,
                Mathf.Max(0f, diff.magnitude - (receiver == null ? 4f : .01f)), Map.groundLayer))
            {
                Collider contact = hit.collider;
                if (contact == null || contact.isTrigger || contact.GetComponentInParent<Unit>() != null ||
                    contact.GetComponent<DamageRelay>() != null) continue;
                if (contact == receiver) continue;
                CollisionDamageTarget obstacle = CollisionDamageTarget.FindSkill(contact);
                CollisionDamageTarget target = CollisionDamageTarget.FindSkill(receiver);
                if (obstacle != null && target != null && obstacle.Key == target.Key) continue;
                if (ignoreProps && (contact.GetComponentInParent<FallingBlock>() != null ||
                    contact.GetComponentInParent<Doodad>() != null)) continue;
                return false;
            }
            return true;
        }

        private static bool TryRegisterSkillHit(object target, HashSet<object> alreadyHit,
            SpringDamageState spring, float castTime, out bool first)
        {
            first = true;
            if (CollisionDamageTarget.AlreadyHit(target, alreadyHit) || (spring != null && !spring.TryHit(target, castTime, out first))) return false;
            CollisionDamageTarget.Register(target, alreadyHit);
            return true;
        }

        private void DamageSkillUnit(Unit target, int damage, float forceX, float forceY,
            int direction, bool knock, float x, float y)
        {
            int modified = ValueOrchestrator.GetModifiedDamage(damage, playerNum);
            DamageType type = SkillDamageType(target, modified);
            bool acid = type == DamageType.Acid;
            // Acid coats the body like a native pool. Do not leave a directional
            // water-impact flag that lets a riot shield reject the zero-force hit.
            if (modified > 0) Map.KnockAndDamageUnit(this, target, modified,
                type, acid ? 0f : forceX, acid ? 0f : forceY, acid ? 0 : direction, !acid && knock, x, y, true);
        }

        // Shared body pass for wave, shark and spring. Relay hits run first;
        // wave terrain handling still runs before this pass and can stop the cast.
        private void SkillDamageUnits(SkillContactMask mask, int damage, float forceX, float forceY,
            bool radial, HashSet<object> alreadyHit, HashSet<object> partTargets, SpringDamageState spring, float castTime)
        {
            bool wave = mask.Kind == 0;
            // Preserve the broad-query limits, including room for the wave's body test.
            float range = wave ? 80f * mask.Scale + 64f :
                mask.Kind == 3 ? 64f * mask.Scale + 5f + 16f : 64f * mask.Scale + 21f;
            foreach (Unit target in Map.GetUnitsInRange(range, range, mask.X, mask.Y + 48f * mask.Scale, true))
            {
                if (!ValidSkillDamageTarget(target) || partTargets.Contains(target) || alreadyHit.Contains(target) ||
                    (wave && CollisionDamageTarget.IsWaveUnit(target))) continue;
                float dy = target.Y + 6f - mask.Y;
                bool contact = wave ? mask.IntersectsBody(target) : mask.Contains(target.X, target.Y + 6f, 5f);
                if (!contact || !SkillLineOfSight(mask.X,
                    mask.Y + Mathf.Clamp(dy, 0f, (wave ? 100f : 112f) * mask.Scale), target)) continue;
                bool first;
                if (!TryRegisterSkillHit(target, alreadyHit, spring, castTime, out first)) continue;
                int direction = radial ? (target.X < mask.X ? -1 : 1) : (mask.Facing < 0f ? -1 : 1);
                DamageSkillUnit(target, damage, first ? (radial ? direction * Mathf.Abs(forceX) : forceX) : 0f,
                    first ? forceY : 0f, direction, first, mask.X, mask.Y + 24f);
            }
        }

        private HashSet<object> SkillDamageRelays(int kind, float x, float y, float facing, float scale, int frame,
            int damage, float forceX, float forceY, bool radial, HashSet<object> alreadyHit)
        {
            return SkillDamageRelays(kind, x, y, facing, scale, frame, damage, forceX, forceY, radial, alreadyHit, null, 0f);
        }

        private HashSet<object> SkillDamageRelays(int kind, float x, float y, float facing, float scale, int frame,
            int damage, float forceX, float forceY, bool radial, HashSet<object> alreadyHit, SpringDamageState spring, float castTime)
        { return SkillDamageRelays(kind, x, y, facing, scale, frame, damage, forceX, forceY, radial, alreadyHit, spring, castTime, null); }

        private HashSet<object> SkillDamageRelays(int kind, float x, float y, float facing, float scale, int frame,
            int damage, float forceX, float forceY, bool radial, HashSet<object> alreadyHit, SpringDamageState spring, float castTime,
            WaveDamageState wave)
        {
            HashSet<object> partTargets = new HashSet<object>();
            SkillContactMask mask = new SkillContactMask(kind, x, y, facing, scale, frame);
            // Discover collision receivers independently of Unit registries or
            // boss class names. Keep the native contact/relay as the damage entry.
            foreach (Collider contact in Physics.OverlapSphere(new Vector3(x, y + 48f * scale, 0f),
                96f * scale + 5f, Map.groundLayer | Map.groundAndDamageableObjects))
            {
                if (contact == null || !contact.enabled || !contact.gameObject.activeInHierarchy || contact.isTrigger) continue;
                CollisionDamageTarget receiver = CollisionDamageTarget.FindSkill(contact);
                if (receiver == null) continue;
                Unit target = receiver.UnitTarget;
                if (target != null) partTargets.Add(target);
                object key = receiver.Key;
                bool periodicWave = wave != null && receiver.IsWaveBoss;
                if (!periodicWave && CollisionDamageTarget.AlreadyHit(key, alreadyHit)) continue;
                // An armoured/closed part must not consume the cast's hit before
                // an exposed weak point is tested later in the same query.
                if ((target != null && !ValidSkillDamageTarget(target)) ||
                    !receiver.CanDamage(playerNum, DamageType.Explosion)) continue;
                Bounds bounds = contact.bounds;
                if (!mask.IntersectsRect(bounds)) continue;
                float hitX = Mathf.Clamp(x, bounds.min.x, bounds.max.x);
                float hitY = Mathf.Clamp(y + 48f * scale, Mathf.Max(y + 1f, bounds.min.y), bounds.max.y);
                if (!SkillContactVisible(x, hitY, hitX, hitY, kind == 2, contact)) continue;
                bool first;
                int hitDamage = damage;
                if (periodicWave)
                {
                    first = false;
                    hitDamage = wave.TakeDamage(key, castTime);
                    if (hitDamage == 0) continue;
                }
                else if (!TryRegisterSkillHit(key, alreadyHit, spring, castTime, out first)) continue;
                float fx = radial ? (hitX < x ? -1f : 1f) * Mathf.Abs(forceX) : forceX;
                int modified = ValueOrchestrator.GetModifiedDamage(hitDamage, playerNum);
                DamageType type = SkillDamageType(target, modified);
                // Preserve native relay immunity/armour for both requested types.
                if (type == DamageType.Acid && !receiver.CanDamage(playerNum, type)) type = DamageType.Explosion;
                bool launch = target != null && first && type != DamageType.Acid;
                if (modified > 0) MapController.Damage_Networked(this, contact.gameObject,
                    modified, type, launch ? fx : 0f, launch ? forceY : 0f, hitX, hitY);
            }
            return partTargets;
        }

        internal void SkillBlast(float x, float y, float radius, int damage, float forceX, float forceY, HashSet<object> alreadyHit, bool radial)
        {
            if (!IsMine) return;
            foreach (Unit target in Map.GetUnitsInRange(radius + 16f, radius + 16f, x, y, true))
            {
                if (!ValidSkillDamageTarget(target)) continue;
                float dx = target.X - x, dy = target.Y + 6f - y;
                if (dx * dx + dy * dy > radius * radius || !SkillLineOfSight(x, y, target) ||
                    (alreadyHit != null && !alreadyHit.Add(target))) continue;
                int direction = dx < 0f ? -1 : 1;
                DamageSkillUnit(target, damage, radial ? direction * Mathf.Abs(forceX) : forceX,
                    forceY, direction, true, x, y);
            }
            Map.HitProjectiles(playerNum, 12, DamageType.Explosion, radius, x, y, forceX, forceY, 0.25f);
        }

        private void AddSkillVisual(int kind, float x, float y, float facing, float lifetime, float delay, float scale)
        {
            AddSkillVisual(kind, x, y, facing, lifetime, delay, scale, null, null);
        }

        private void AddSkillVisual(int kind, float x, float y, float facing, float lifetime, float delay, float scale,
            HashSet<object> sharedHits, SpringDamageState sharedSpring)
        {
            skillVisuals.Add(new AquabroSkillVisual(this, kind, x, y, facing, lifetime, delay, scale, sharedHits, sharedSpring));
        }

        internal void SkillSpringBlast(float x, float y, float facing, float scale, int frame,
            SpringDamageState spring, float castTime)
        {
            if (!IsMine || frame < 0 || frame >= AquabroSkillVisual.SpringDissolveStartFrame) return;
            // The build exports this mask from the very same composite as the PNG.
            HashSet<object> pulseHits = new HashSet<object>();
            HashSet<object> partTargets = SkillDamageRelays(3, x, y, facing, scale, frame,
                4, 70f, 650f, true, pulseHits, spring, castTime);
            SkillDamageUnits(new SkillContactMask(3, x, y, facing, scale, frame),
                4, 70f, 650f, true, pulseHits, partTargets, spring, castTime);
        }

        internal Vector2 FindMaelstromDestination(float facing)
        {
            Vector2 best = new Vector2();
            float bestScore = float.PositiveInfinity;
            foreach (Unit target in Map.GetUnitsInRange(180f, 112f, X, Y + 6f, false))
            {
                // Placement follows any valid hostile unit, including vehicles.
                // Continuous suction below remains limited to infantry.
                // Worm roots can remain underground while their exposed heads move.
                // Use the open collision receivers below for this family, even if registered.
                if (target is AlienMinibossSandWorm || target.playerNum >= 0 || !ValidSkillTarget(target)) continue;
                ConsiderMaelstromDestination(facing, target.X, target.Y, ref best, ref bestScore);
            }
            // Several native worms never register in Map.units. Discover their
            // exposed damage receivers without changing the game's unit registry.
            foreach (Collider contact in Physics.OverlapSphere(new Vector3(X, Y + 6f, 0f),
                216f, Map.groundLayer | Map.groundAndDamageableObjects))
            {
                if (contact == null || !contact.enabled || !contact.gameObject.activeInHierarchy || contact.isTrigger) continue;
                CollisionDamageTarget receiver = CollisionDamageTarget.FindSkill(contact);
                Unit target = receiver == null ? null : receiver.UnitTarget;
                if (!(target is AlienMinibossSandWorm) || target.playerNum >= 0 || !ValidSkillTarget(target) ||
                    !receiver.CanDamage(playerNum, DamageType.Explosion)) continue;
                Bounds bounds = contact.bounds;
                // A partially emerged head can overlap the floor at its lower
                // edge. Start the ground probe at its exposed centre instead.
                ConsiderMaelstromDestination(facing, bounds.center.x, bounds.center.y, ref best, ref bestScore);
            }
            if (bestScore < float.PositiveInfinity) return best;
            // Without a reachable enemy, keep the forward cast and stop at solid
            // terrain. Crates, barrels and decorative objects never shorten it.
            Vector3 origin = new Vector3(X, Y + 14f, 0f);
            Vector3 fallback = new Vector3(X + facing * 70f, Y + 14f, 0f);
            Vector3 forward = fallback - origin;
            RaycastHit hit;
            if (RaycastSharkTerrain(origin, forward.normalized, forward.magnitude, out hit))
                fallback = new Vector3(hit.point.x - forward.normalized.x * 8f, fallback.y, 0f);
            float ground = RaycastSharkTerrain(fallback, Vector3.down, 80f, out hit) ? hit.point.y : Y + 2f;
            return new Vector2(fallback.x, ground);
        }

        private void ConsiderMaelstromDestination(float facing, float targetX, float targetY,
            ref Vector2 best, ref float bestScore)
        {
            float dx = targetX - X, dy = targetY - Y;
            if (Mathf.Abs(dx) > 180f || Mathf.Abs(dy) > 96f || dx * facing < 0f) return;
            float score = dx * dx + dy * dy;
            if (score >= bestScore) return;
            Vector3 origin = new Vector3(X, Y + 14f, 0f);
            Vector3 diff = new Vector3(targetX, targetY + 14f, 0f) - origin;
            RaycastHit wall, floor;
            if (diff.magnitude > 1f && RaycastSharkTerrain(origin, diff.normalized, diff.magnitude, out wall)) return;
            if (!RaycastSharkTerrain(new Vector3(targetX, targetY + 2f, 0f), Vector3.down, 80f, out floor)) return;
            bestScore = score; best = new Vector2(targetX, floor.point.y);
        }

        private bool MaelstromLineOfSight(float x, float y, Unit target)
        {
            Vector3 diff = new Vector3(target.X - x, target.Y + 6f - y, 0f);
            RaycastHit hit;
            return diff.magnitude < 8f || !RaycastSharkTerrain(new Vector3(x, y, 0f),
                diff.normalized, diff.magnitude - 4f, out hit);
        }

        private bool MaelstromTouchesTarget(float x, float y, float facing, float scale, int frame,
            Unit target, HashSet<object> caught)
        {
            if (!ValidSkillDamageTarget(target)) return false;
            float halfWidth = Mathf.Max(1f, target.width * .5f);
            float top = target.Y + Mathf.Max(target.headHeight, target.height);
            if (top <= y + 1f || target.Y > y + 112f * scale + 5f ||
                Mathf.Abs(target.X - x) > 64f * scale + halfWidth + 5f) return false;
            bool contact = new SkillContactMask(2, x, y, facing, scale, frame).IntersectsBody(target);
            // A captured enemy may enter the transparent mouth or a gap between
            // rotating sheets. Keep pulling it while it remains inside the effect.
            if (!contact && !caught.Contains(target)) return false;
            // Test at the enemy's height: a platform below it must not block an
            // exposed upper rim just because the old ray began near the floor.
            return MaelstromLineOfSight(x, target.Y + 6f, target);
        }

        internal void SkillMaelstromPull(float x, float y, float facing, float scale, int frame,
            float delta, bool pulse, HashSet<object> caught)
        { SkillMaelstromPullAtTime(x, y, facing, scale, frame, delta, pulse, caught, 0f); }

        internal static Vector2 MaelstromAttractor(Unit target, float x, float y, float scale, float age)
        {
            // A stable phase per enemy avoids a new random impulse every frame.
            // The targets circulate through the middle and upper water instead
            // of all settling at the same low point under gravity.
            uint seed = unchecked((uint)target.GetHashCode());
            seed ^= seed >> 16;
            seed = unchecked(seed * 0x45d9f3bu);
            seed ^= seed >> 16;
            float phase = (seed & 65535u) * (6.2831853f / 65536f);
            return new Vector2(x + scale * 16f * Mathf.Sin(phase + age * 2.3f),
                y + scale * (62f + 24f * Mathf.Sin(phase * 1.618034f + age * 1.7f)));
        }

        internal void SkillMaelstromPullAtTime(float x, float y, float facing, float scale, int frame,
            float delta, bool pulse, HashSet<object> caught, float age)
        {
            if (!IsMine || delta <= 0f || frame < 0 || frame >= AquabroSkillVisual.MaelstromDissolveStartFrame) return;
            HashSet<object> pulseHits = new HashSet<object>();
            HashSet<object> partTargets = pulse ? SkillDamageRelays(2, x, y, facing, scale, frame,
                AquabroSkillVisual.MaelstromPulseDamage, 0f, 0f, false, pulseHits) : null;
            float range = 64f * scale + 64f;
            foreach (Unit target in Map.GetUnitsInRange(range, range, x, y + 48f * scale, true))
            {
                if (!MaelstromTouchesTarget(x, y, facing, scale, frame, target, caught)) continue;
                caught.Add(target);
                if (pulse && !partTargets.Contains(target))
                    DamageSkillUnit(target, AquabroSkillVisual.MaelstromPulseDamage, 0f, 0f, 0, false, x, target.Y + 6f);
                if (target == null || !(target is Mook) || !target.gameObject.activeInHierarchy ||
                    target.HasBeenCoveredInAcid()) continue;
                Vector2 destination = MaelstromAttractor(target, x, y, scale, age);
                float vx = Mathf.MoveTowards(target.xI, Mathf.Clamp((destination.x - target.X) * 6f, -170f, 170f), delta * 1200f);
                float vy = Mathf.MoveTowards(target.yI, Mathf.Clamp((destination.y - target.Y - 6f) * 7f, -160f, 260f), delta * 2400f);
                ApplyMaelstromKnock(target, vx, vy);
            }
        }

        private static void ApplyMaelstromKnock(Unit target, float vx, float vy)
        {
            // MookBigGuy divides horizontal impulse by 20 and vertical by 5.
            // Compensate only this skill's continuous suction, including subclasses.
            bool heavy = target is MookBigGuy;
            if (heavy && target.health > 0)
            {
                MookBigGuy mook = (MookBigGuy)target;
                // Its Knock override never tumbles or disables AI. Keep a short
                // stun while caught, without shortening an existing longer stun.
                float previousX = target.xI;
                if (!mook.IsStunned) mook.Stun(.15f);
                mook.ClearAllInput();
                // Stun calls Stop(), which clears horizontal speed and sets Idle.
                // Preserve suction momentum and let normal airborne physics run.
                target.xI = previousX;
                mook.actionState = ActionState.Jumping;
            }
            target.Knock(DamageType.Explosion, (vx - target.xI) * (heavy ? 20f : 2f),
                (vy - target.yI) * (heavy ? 5f : 1f), true);
        }

        internal void SkillMaelstromFinish(float x, float y, float facing, float scale, int frame,
            HashSet<object> caught, HashSet<object> alreadyHit)
        {
            if (!IsMine) return;
            HashSet<object> partTargets = SkillDamageRelays(2, x, y, facing, scale, frame,
                AquabroSkillVisual.MaelstromFinishDamage, 220f, 500f, true, alreadyHit);
            float range = 64f * scale + 64f;
            foreach (Unit target in Map.GetUnitsInRange(range, range, x, y + 48f * scale, true))
            {
                if (partTargets.Contains(target) || !MaelstromTouchesTarget(x, y, facing, scale, frame, target, caught) ||
                    !alreadyHit.Add(target)) continue;
                int direction = target.X < x ? -1 : 1;
                DamageSkillUnit(target, AquabroSkillVisual.MaelstromFinishDamage,
                    direction * 220f, 500f, direction, true, x, target.Y + 6f);
            }
        }

        private static bool RaycastSharkTerrain(Vector3 origin, Vector3 direction, float distance, out RaycastHit nearest)
        {
            return RaycastSharkTerrain(origin, direction, distance, Map.groundLayerStrict, out nearest);
        }

        private static bool RaycastSharkTerrain(Vector3 origin, Vector3 direction, float distance, int layers, out RaycastHit nearest)
        { return RaycastSkillTerrain(origin, direction, distance, layers, false, out nearest); }

        private static bool RaycastSpringTerrain(Vector3 origin, Vector3 direction, float distance, out RaycastHit nearest)
        { return RaycastSkillTerrain(origin, direction, distance, Map.groundLayerStrict, true, out nearest); }

        private static bool RaycastSkillTerrain(Vector3 origin, Vector3 direction, float distance, int layers,
            bool ignoreCages, out RaycastHit nearest)
        {
            nearest = new RaycastHit();
            float best = float.PositiveInfinity;
            bool found = false;
            // Crates and barrels also use Ground. Inspect every hit so ignoring
            // a prop still leaves a real wall/floor behind it available to stop us.
            // Elevators inherit Doodad, but their solid colliders are terrain.
            foreach (RaycastHit hit in Physics.RaycastAll(origin, direction, distance, layers))
            {
                Collider contact = hit.collider;
                if (contact == null || contact.isTrigger ||
                    contact.GetComponentInParent<Unit>() != null || contact.GetComponent<DamageRelay>() != null ||
                    contact.GetComponentInParent<FallingBlock>() != null ||
                    (contact.GetComponentInParent<Doodad>() != null && contact.GetComponentInParent<Elevator>() == null)) continue;
                // All columns are planned before the first one can break a cage.
                // A destructible prop must not permanently cancel later columns.
                if (ignoreCages && (contact.GetComponent<Cage>() != null ||
                    contact.GetComponentInParent<Cage>() != null || contact.GetComponent<CageBar>() != null)) continue;
                if (hit.distance >= best) continue;
                best = hit.distance; nearest = hit; found = true;
            }
            return found;
        }

        private bool FindSharkFloor(float x, float y, bool dropDown, out float floorY)
        {
            RaycastHit floor;
            // Standing boss surfaces use LargeObjects, just like the hero's
            // ground mask. Keep horizontal walls and maelstrom queries strict.
            bool found = RaycastSharkTerrain(new Vector3(x, y + 2f, 0f), Vector3.down,
                float.PositiveInfinity, Map.groundLayer, out floor);
            floorY = found ? floor.point.y : y;
            // Ladder tops and bodies support the shark at its current height.
            // Only down input bypasses this support; solid floors still block it.
            if (!dropDown && (!found || floorY < y))
                foreach (Collider ladder in Physics.OverlapSphere(new Vector3(x, y - .1f, 0f), .1f, ladderLayer))
                    if (ladder != null && !ladder.isTrigger) { floorY = y; return true; }
            return found;
        }

        internal Collider FindSharkElevatorSupport(float x, float y)
        {
            RaycastHit floor;
            return RaycastSharkTerrain(new Vector3(x, y + 2f, 0f), Vector3.down, 4f, Map.groundLayer, out floor) &&
                Mathf.Abs(floor.point.y - y) <= 2f && floor.collider.GetComponentInParent<Elevator>() != null ?
                floor.collider : null;
        }

        internal void MoveShark(ref float x, ref float y, float direction, float delta)
        {
            if (delta <= 0f) return;
            // Small terrain-only steps ignore crates/large objects, stop at real
            // walls, and follow descending ground without climbing vertical faces.
            float distance = direction * 90f * delta;
            int steps = System.Math.Max(1, (int)System.Math.Ceiling(Mathf.Abs(distance) / 4f));
            for (int i = 0; i < steps; i++)
            {
                float next = x + distance / steps;
                RaycastHit wall;
                if (direction != 0f && RaycastSharkTerrain(new Vector3(x, y + 4f, 0f), new Vector3(direction, 0f, 0f),
                    Mathf.Abs(next - x) + 4f, out wall))
                    next = direction > 0f ? Mathf.Max(x, Mathf.Min(next, wall.point.x - 4f)) :
                        Mathf.Min(x, Mathf.Max(next, wall.point.x + 4f));
                float floorY;
                if (!FindSharkFloor(next, y, sharkDropInput, out floorY)) break;
                x = next; y = floorY;
            }
        }

        internal bool MoveSharkFlight(ref float x, ref float y, float destinationX, float destinationY,
            out bool ceiling)
        {
            x = MoveSkillHorizontally(x, y, destinationX);
            ceiling = false;
            RaycastHit contact;
            if (destinationY < y && RaycastSharkTerrain(new Vector3(x, y + 2f, 0f), Vector3.down,
                y - destinationY + 2f, Map.groundLayer, out contact))
            {
                y = contact.point.y;
                return true;
            }
            if (destinationY > y && RaycastSharkTerrain(new Vector3(x, y + 56f, 0f), new Vector3(0f, 1f, 0f),
                destinationY - y, out contact))
            {
                y = contact.point.y - 56f; ceiling = true;
            }
            else y = destinationY;
            return false;
        }

        internal float MoveSkillHorizontally(float x, float y, float destination)
        {
            float distance = destination - x;
            int steps = System.Math.Max(1, (int)System.Math.Ceiling(Mathf.Abs(distance) / 4f));
            float step = distance / steps;
            float direction = step < 0f ? -1f : 1f;
            for (int i = 0; i < steps && Mathf.Abs(step) > .00001f; i++)
            {
                float travel = Mathf.Abs(step);
                foreach (float height in new float[] { 12f, 56f })
                {
                    RaycastHit hit;
                    if (RaycastSharkTerrain(new Vector3(x, y + height, 0f), new Vector3(direction, 0f, 0f),
                        travel + 8f, out hit)) travel = Mathf.Min(travel, Mathf.Max(0f, hit.distance - 8f));
                }
                x += direction * travel;
                if (travel < Mathf.Abs(step)) break;
            }
            return x;
        }

        internal void SkillSharkBlast(float x, float y, float facing, float scale, int frame, HashSet<object> alreadyHit)
        {
            if (!IsMine || !AquabroSkillVisual.IsSharkDamageFrame(frame)) return;
            HashSet<object> partTargets = SkillDamageRelays(1, x, y, facing, scale, frame, 28, facing * 180f, 420f, false, alreadyHit);
            SkillDamageUnits(new SkillContactMask(1, x, y, facing, scale, frame),
                28, facing * 180f, 420f, false, alreadyHit, partTargets, null, 0f);
        }

        internal void SkillSharkPanic(float x, float y, float facing)
        {
            if (!IsMine) return;
            foreach (Unit target in Map.GetUnitsInRange(64f, 40f, x, y + 16f, false))
            {
                if (!ValidSkillTarget(target) || target.playerNum >= 0 ||
                    Mathf.Abs(target.X - x) > 64f || target.Y < y - 4f || target.Y > y + 36f ||
                    !SkillLineOfSight(x, y + 8f, target)) continue;
                int direction = target.X == x ? (facing < 0f ? -1 : 1) : (target.X < x ? -1 : 1);
                target.Panic(direction, .6f, true);
            }
        }

        internal void SkillSharkObjects(float x, float y, float facing, float scale, int frame, HashSet<GameObject> alreadyHit)
        {
            if (!IsMine || !AquabroSkillVisual.IsSharkDamageFrame(frame)) return;
            SkillContactMask mask = new SkillContactMask(1, x, y, facing, scale, frame);
            foreach (Collider contact in Physics.OverlapSphere(new Vector3(x, y + 48f * scale, 0f),
                82f * scale + 5f, Map.groundAndDamageableObjects))
            {
                if (contact == null || !contact.enabled || !contact.gameObject.activeInHierarchy || contact.isTrigger) continue;
                DamageRelay relay = contact.GetComponent<DamageRelay>();
                if (contact.GetComponentInParent<Unit>() != null || (relay != null && relay.doodad == null) ||
                    CollisionDamageTarget.FindSkill(contact) != null) continue;
                Bounds bounds = contact.bounds;
                float hitX = Mathf.Clamp(x, bounds.min.x, bounds.max.x);
                float hitY = Mathf.Clamp(y + 48f * scale, Mathf.Max(y + 1f, bounds.min.y), bounds.max.y);
                if (!mask.IntersectsRect(bounds) || !WaterContactVisible(x, hitY, hitX, hitY, contact) ||
                    !alreadyHit.Add(contact.gameObject)) continue;
                MapController.Damage_Networked(this, contact.gameObject, ValueOrchestrator.GetModifiedDamage(28, playerNum),
                    DamageType.Crush, facing * 180f, 420f, bounds.center.x, bounds.center.y);
            }
            for (int i = Map.destroyableDoodads.Count - 1; i >= 0; i--)
            {
                Doodad doodad = Map.destroyableDoodads[i];
                if (doodad == null || !doodad.gameObject.activeInHierarchy ||
                    (playerNum >= 0 ? doodad.immuneToHeroDamage : !doodad.CanBeDamagedByMooks)) continue;
                if (!mask.IntersectsRect(
                    doodad.centerX - doodad.width * .5f, doodad.centerY - doodad.height * .5f,
                    doodad.centerX + doodad.width * .5f, doodad.centerY + doodad.height * .5f) ||
                    !WaterContactVisible(x, doodad.centerY, doodad.centerX, doodad.centerY, null) ||
                    !alreadyHit.Add(doodad.gameObject)) continue;
                bool hit = false;
                doodad.DamageOptional(new DamageObject(ValueOrchestrator.GetModifiedDamage(28, playerNum),
                    DamageType.Crush, facing * 180f, 420f, x, y, this), ref hit);
            }
        }

        internal bool SkillWaveContact(float x, float y, float facing, float scale, int frame,
            HashSet<object> alreadyHit, HashSet<GameObject> hitObjects)
        { return SkillWaveContact(x, y, facing, scale, frame, alreadyHit, hitObjects, null, 0f); }

        internal bool SkillWaveContact(float x, float y, float facing, float scale, int frame,
            HashSet<object> alreadyHit, HashSet<GameObject> hitObjects, WaveDamageState wave, float castTime)
        {
            if (!IsMine || frame < 0 || frame >= AquabroSkillVisual.TidalDissolveStartFrame) return true;
            HashSet<object> partTargets = SkillDamageRelays(0, x, y, facing, scale, frame, 26, facing * 420f, 300f, false,
                alreadyHit, null, castTime, wave);
            SkillContactMask mask = new SkillContactMask(0, x, y, facing, scale, frame);
            bool blocked = SkillWaveLargeUnits(mask, alreadyHit, partTargets, wave, castTime);
            Collider[] contacts = Physics.OverlapSphere(new Vector3(x, y + 48f * scale, 0f),
                96f * scale + 5f, Map.groundLayer | Map.groundAndDamageableObjects);
            Array.Sort(contacts, delegate(Collider a, Collider b)
            { return (a.bounds.center.x * facing).CompareTo(b.bounds.center.x * facing); });
            foreach (Collider contact in contacts)
            {
                if (contact == null || !contact.enabled || !contact.gameObject.activeInHierarchy || contact.isTrigger) continue;
                CollisionDamageTarget boss = CollisionDamageTarget.FindSkill(contact);
                if (boss != null && boss.StopsWave && mask.IntersectsRect(contact.bounds))
                {
                    Bounds b = contact.bounds;
                    float hitX = Mathf.Clamp(x, b.min.x, b.max.x);
                    float hitY = Mathf.Clamp(y + 48f * scale, Mathf.Max(y + 1f, b.min.y), b.max.y);
                    if (SkillContactVisible(x, hitY, hitX, hitY, false, contact)) blocked = true;
                }
                // Units use the body-mask damage pass below, including large
                // enemies whose colliders may also be in the terrain query.
                DamageRelay relay = contact.GetComponent<DamageRelay>();
                if (contact.GetComponentInParent<Unit>() != null || (relay != null && relay.doodad == null) ||
                    CollisionDamageTarget.FindSkill(contact) != null) continue;
                Bounds bounds = contact.bounds;
                if (!mask.IntersectsRect(bounds)) continue;
                float terrainX = Mathf.Clamp(x, bounds.min.x, bounds.max.x);
                float terrainY = Mathf.Clamp(y + 48f * scale, Mathf.Max(y + 1f, bounds.min.y), bounds.max.y);
                if (!WaterContactVisible(x, terrainY, terrainX, terrainY, contact)) continue;
                if (wave == null ? hitObjects.Add(contact.gameObject) : wave.TryHit(contact.gameObject, castTime))
                    MapController.Damage_Networked(this, contact.gameObject,
                        ValueOrchestrator.GetModifiedDamage(24, playerNum), DamageType.Crush,
                        facing * 420f, 80f, bounds.center.x, bounds.center.y);
                // A surviving wall stops the wave. Floor contact alone does not.
                if (contact != null && contact.enabled && contact.gameObject.activeInHierarchy &&
                    bounds.max.y > y + 8f) blocked = true;
            }
            // Doodads use the same five-pixel boundary, without a separate area blast.
            for (int i=Map.destroyableDoodads.Count-1;i>=0;i--)
            {
                Doodad doodad=Map.destroyableDoodads[i];
                if (doodad==null || !doodad.gameObject.activeInHierarchy ||
                    (playerNum>=0 ? doodad.immuneToHeroDamage : !doodad.CanBeDamagedByMooks)) continue;
                if (!mask.IntersectsRect(
                    doodad.centerX-doodad.width*.5f,doodad.centerY-doodad.height*.5f,
                    doodad.centerX+doodad.width*.5f,doodad.centerY+doodad.height*.5f)) continue;
                if (!WaterContactVisible(x, doodad.centerY, doodad.centerX, doodad.centerY, null)) continue;
                bool hit=false;
                if (hitObjects.Add(doodad.gameObject))
                    doodad.DamageOptional(new DamageObject(ValueOrchestrator.GetModifiedDamage(24,playerNum),
                        DamageType.Crush,facing*420f,80f,x,y,this),ref hit);
                if (hit && doodad.isImpenetrable) blocked = true;
            }
            SkillDamageUnits(mask, 26, facing * 420f, 300f, false, alreadyHit, partTargets, null, 0f);
            return !blocked;
        }

        // Vehicles can have only Platform-layer child colliders (truck roofs),
        // or no active damage collider at all. Use their native Unit body too.
        private bool SkillWaveLargeUnits(SkillContactMask mask, HashSet<object> hits,
            HashSet<object> partTargets, WaveDamageState wave, float castTime)
        {
            bool blocked = false;
            float range = 80f * mask.Scale + 64f;
            foreach (Unit target in Map.GetUnitsInRange(range, range, mask.X, mask.Y + 48f * mask.Scale, true))
            {
                if (!CollisionDamageTarget.IsWaveUnit(target) || target == this || target.health <= 0 ||
                    !target.gameObject.activeInHierarchy || !GameModeController.DoesPlayerNumDamage(playerNum, target.playerNum) ||
                    !mask.IntersectsBody(target) || !SkillLineOfSight(mask.X,
                        mask.Y + Mathf.Clamp(target.Y + 6f - mask.Y, 0f, 100f * mask.Scale), target)) continue;
                // Keep closed relay gates authoritative. Relay/body hits share
                // the same target key and the same cast-wide damage budget.
                if (!partTargets.Contains(target) && ValidSkillDamageTarget(target))
                {
                    int damage = wave == null ? (hits.Add(target) ? 26 : 0) : wave.TakeDamage(target, castTime);
                    if (damage > 0) DamageSkillUnit(target, damage, 0f, 0f, 0, false, mask.X, target.Y + 6f);
                }
                if (target.health > 0) blocked = true;
            }
            return blocked;
        }

        private void TickSkillPreview(float delta)
        {
            if (health <= 0) { ClearSkillPreview(); return; }
            if (delta <= 0f) return;
            // Absorb from every spring column before any column deals damage.
            // In particular, a pool on the left must affect right-side hits
            // in this same update, independent of the reverse damage order.
            foreach (AquabroSkillVisual effect in skillVisuals) effect.PrepareSpringWater(delta);
            for (int i = skillVisuals.Count - 1; i >= 0; i--)
                if (!skillVisuals[i].Tick(delta)) { skillVisuals[i].Dispose(); skillVisuals.RemoveAt(i); }
        }

        private void ClearSkillPreview()
        {
            foreach (AquabroSkillVisual effect in skillVisuals) effect.Dispose();
            skillVisuals.Clear();
            CurrentAcidStage = 0;
        }

        private void OnDisable() { ClearSkillPreview(); }
    }

    // Per-target actual-time cooldown, shared by linked boss parts and substeps.
    internal sealed class WaveDamageState
    {
        private readonly Dictionary<object, float> nextHit = new Dictionary<object, float>();
        private readonly Dictionary<object, int> spent = new Dictionary<object, int>();
        internal int TakeDamage(object target, float time)
        {
            object[] keys = CollisionDamageTarget.DamageKeys(target);
            int used = 0;
            foreach (object key in keys)
            {
                int previous;
                spent.TryGetValue(key, out previous);
                used = System.Math.Max(used, previous);
            }
            int damage = System.Math.Min(2, 26 - used);
            if (damage <= 0 || !TryHit(target, time)) return 0;
            foreach (object key in keys) spent[key] = used + damage;
            return damage;
        }
        internal bool TryHit(object target, float time)
        {
            object[] keys = CollisionDamageTarget.DamageKeys(target);
            foreach (object key in keys)
            {
                float next;
                if (nextHit.TryGetValue(key, out next) && time + .00001f < next) return false;
            }
            foreach (object key in keys) nextHit[key] = time + .12f;
            return true;
        }
    }

    // Shared by all seven columns of one cast, including all boss parts.
    internal sealed class SpringDamageState
    {
        internal readonly WaterAcidState Acid = new WaterAcidState();
        private readonly Dictionary<object, int> hits = new Dictionary<object, int>();
        private readonly Dictionary<object, float> nextHit = new Dictionary<object, float>();
        internal bool TryHit(object target, float castTime, out bool first)
        {
            object[] keys = CollisionDamageTarget.DamageKeys(target);
            int count = 0;
            float available = 0f;
            foreach (object key in keys)
            {
                int previous; float next;
                hits.TryGetValue(key, out previous); nextHit.TryGetValue(key, out next);
                count = System.Math.Max(count, previous); available = Mathf.Max(available, next);
            }
            first = count == 0;
            if (count >= 7 || (!first && castTime + .00001f < available)) return false;
            // Actual hit time prevents adjacent columns or low FPS from
            // compressing pulses into the original vehicle 0.096s cooldown.
            foreach (object key in keys) { hits[key] = count + 1; nextHit[key] = castTime + .12f; }
            return true;
        }
    }

    internal sealed class SharkSplashVisual
    {
        private readonly Aquabro owner;
        private readonly float x, y, facing, scale;
        private readonly int first, end;
        private readonly GameObject[] objects = new GameObject[2];
        private readonly SpriteSM[] sprites = new SpriteSM[2];
        internal SharkSplashVisual(Aquabro owner, float x, float y, float facing, float scale, int first, int end)
        {
            this.owner = owner; this.x = x; this.y = y; this.facing = facing; this.scale = scale;
            this.first = first; this.end = end;
            for (int i = 0; i < 2; i++)
            {
                string file = "skill-shark-hunt-water-" + (i == 0 ? "back" : "front") + ".png";
                objects[i] = new GameObject("Aquabro Shark Splash " + (i == 0 ? "Back" : "Front"));
                objects[i].transform.parent = owner.transform;
                SpriteSM sprite = sprites[i] = objects[i].AddComponent<SpriteSM>();
                sprite.meshRender.material = ResourcesController.GetMaterial(owner.Info.path, file);
                sprite.SetPixelDimensions(128, 128); sprite.SetSize(128f * scale, 128f * scale);
                sprite.SetLowerLeftPixel(first * 128, 128); sprite.RecalcTexture(); sprite.CalcUVs(); sprite.UpdateUVs();
            }
            Update(0f);
        }
        internal void Update(float age)
        {
            int frame = first + Mathf.FloorToInt(age / .05f);
            for (int i = 0; i < 2; i++)
            {
                objects[i].SetActive(frame < end);
                if (frame >= end) continue;
                // Reset world position and facing even if the parent hero walks or turns.
                objects[i].transform.position = new Vector3(x, y + 48f * scale, i == 0 ? 1.05f : .95f);
                objects[i].transform.localScale = new Vector3(facing / (owner.transform.localScale.x < 0f ? -1f : 1f), 1f, 1f);
                sprites[i].SetLowerLeftPixel(frame * 128, 128);
            }
        }
        internal void Dispose()
        {
            for (int i = 0; i < 2; i++)
            {
                UnityEngine.Object.Destroy(objects[i]);
            }
        }
    }

    // Advanced by the hero's actual time step: pause and TestMod frame stepping
    // freeze both damage and animation. Objects are children for scene cleanup.
    internal sealed class AquabroSkillVisual
    {
        internal const int SpringDissolveStartFrame = 18;
        internal const int MaelstromDissolveStartFrame = 24;
        internal const float MaelstromActiveDuration = 3.6f;
        internal const float MaelstromLifetime = 4.2f;
        internal const float MaelstromPulseInterval = .4f;
        internal const int MaelstromPulseDamage = 3;
        internal const int MaelstromFinishDamage = 9;
        internal const int TidalDissolveStartFrame = 6;
        internal const float TidalTravelDuration = 1.5f;
        internal const float TidalLifetime = 2f;
        internal const float SharkBreachDistance = 64f;
        internal const float SharkLaunchSpeed = 240f;
        internal const float SharkGravity = 800f;
        internal const float SharkFlightLimit = 4f;
        internal const int SharkFlightFirstFrame = 28;
        internal const int SharkFlightFrameCount = 31;
        internal const int SharkBodyFrameCount = SharkFlightFirstFrame + SharkFlightFrameCount;
        internal static bool IsSharkDamageFrame(int frame)
        { return frame >= 6 && (frame < 27 || (frame >= SharkFlightFirstFrame && frame < SharkBodyFrameCount)); }
        private const float TidalSpeed = 160f;
        // Enemies render near z=0 and terrain near z=5; keep all four
        // skill sprites behind enemies while remaining in front of terrain.
        private const float VisualDepth = 1f;
        private readonly Aquabro owner;
        private readonly int kind;
        private float facing;
        private readonly float lifetime, scale;
        private readonly GameObject obj;
        private readonly SpriteSM sprite;
        private readonly HashSet<object> hitUnits;
        private readonly SpringDamageState springDamage;
        private readonly float castDelay;
        private readonly float originX;
        private float maelstromLeftX, maelstromRightX;
        private float maelstromRouteStartX, maelstromRouteStartTime;
        private int maelstromRouteLeg;
        private readonly HashSet<object> maelstromCaught = new HashSet<object>();
        private readonly HashSet<GameObject> hitObjects = new HashSet<GameObject>();
        private readonly WaterProjectileBarrier waterBarrier;
        private SharkSplashVisual sharkLaunchWater;
        private float x, y, elapsed, pulseTime;
        private bool resolved;
        private bool tidalBlocked;
        private readonly WaveDamageState waveDamage = new WaveDamageState();
        private static readonly int[] Counts = {16,28,32,28};
        private float sharkBreachElapsed = -1f, sharkInput, sharkVelocityX, sharkVelocityY;
        private float sharkLandingElapsed = -1f;
        private Collider sharkElevatorSupport;
        private Vector3 sharkElevatorPosition;
        internal bool SharkControlled { get { return kind == 1 && sharkBreachElapsed < 0f; } }
        internal Transform CameraTarget { get { return obj.transform; } }
        internal void SetSharkInput(bool left, bool right)
        { sharkInput = (right ? 1f : 0f) - (left ? 1f : 0f); }
        internal void BreachShark()
        {
            if (!SharkControlled) return;
            sharkBreachElapsed = 0f;
            sharkInput = 0f;
            sharkVelocityX = facing * SharkBreachDistance / (2f * SharkLaunchSpeed / SharkGravity);
            sharkVelocityY = SharkLaunchSpeed;
            sharkLaunchWater = new SharkSplashVisual(owner, x, y, facing, scale, 6, 19);
            sprite.meshRender.material = ResourcesController.GetMaterial(owner.Info.path, "skill-shark-hunt-body.png");
            sprite.RecalcTexture(); sprite.CalcUVs(); sprite.UpdateUVs();
            owner.ReleaseSharkControl(this);
        }

        public AquabroSkillVisual(Aquabro owner, int kind, float x, float y, float facing, float lifetime, float delay, float scale)
            : this(owner, kind, x, y, facing, lifetime, delay, scale, null) {}

        public AquabroSkillVisual(Aquabro owner, int kind, float x, float y, float facing, float lifetime, float delay, float scale, HashSet<object> sharedHits)
            : this(owner, kind, x, y, facing, lifetime, delay, scale, sharedHits, null) {}

        public AquabroSkillVisual(Aquabro owner, int kind, float x, float y, float facing, float lifetime, float delay,
            float scale, HashSet<object> sharedHits, SpringDamageState sharedSpring)
        {
            hitUnits = sharedHits ?? new HashSet<object>();
            springDamage = sharedSpring ?? new SpringDamageState();
            castDelay = delay;
            this.owner = owner; this.kind = kind; this.x = x; this.y = y;
            originX = x;
            maelstromRouteStartX = x;
            if (kind == 1) RememberSharkElevator();
            if (kind == 2)
            {
                // Schedule only the distance terrain actually permits. A clipped
                // virtual path would leave the vortex waiting at the wall.
                maelstromLeftX = owner.MoveSkillHorizontally(x, y, x - 48f);
                maelstromRightX = owner.MoveSkillHorizontally(x, y, x + 48f);
            }
            this.facing = facing; this.lifetime = lifetime; this.scale = scale; elapsed = -delay;
            obj = new GameObject("Aquabro Skill " + Aquabro.SkillAssets[kind]);
            obj.transform.parent = owner.transform;
            sprite = obj.AddComponent<SpriteSM>();
            sprite.meshRender.material = ResourcesController.GetMaterial(owner.Info.path, "skill-" + Aquabro.SkillAssets[kind] + ".png");
            sprite.SetPixelDimensions(128,128);
            sprite.SetSize(128f * scale, 128f * scale);
            sprite.SetLowerLeftPixel(0,128);
            sprite.RecalcTexture(); sprite.CalcUVs(); sprite.UpdateUVs();
            obj.transform.position = new Vector3(x, y + 48f * scale, VisualDepth);
            if (kind != 1) waterBarrier=new WaterProjectileBarrier(owner,kind,x,y,facing,scale,
                kind == 3 ? springDamage.Acid : null);
            obj.SetActive(false);
        }

        private void RememberSharkElevator()
        {
            sharkElevatorSupport = owner.FindSharkElevatorSupport(x, y);
            if (sharkElevatorSupport != null)
                sharkElevatorPosition = sharkElevatorSupport.transform.position;
        }

        private float MaelstromDestination(float time, out int leg)
        {
            leg = maelstromRouteLeg;
            float target = leg == 1 ? maelstromLeftX : maelstromRightX;
            float first = Mathf.Abs(target - maelstromRouteStartX);
            float span = maelstromRightX - maelstromLeftX;
            float travel = Mathf.Clamp((time - maelstromRouteStartTime) /
                (MaelstromActiveDuration - maelstromRouteStartTime), 0f, 1f) * (first + (2 - leg) * span);
            if (travel < first)
                return maelstromRouteStartX + (target < maelstromRouteStartX ? -travel : travel);
            travel -= first;
            for (leg++; leg < 3; leg++)
            {
                if (travel < span)
                    return leg == 1 ? maelstromRightX - travel : maelstromLeftX + travel;
                travel -= span;
            }
            leg = 2;
            return maelstromRightX;
        }

        private void ExpandMaelstromRoute(float previousElapsed)
        {
            if (resolved || previousElapsed >= MaelstromActiveDuration) return;
            float left = Mathf.Min(maelstromLeftX, owner.MoveSkillHorizontally(originX, y, originX - 48f));
            float right = Mathf.Max(maelstromRightX, owner.MoveSkillHorizontally(originX, y, originX + 48f));
            if (left >= maelstromLeftX - .00001f && right <= maelstromRightX + .00001f) return;
            int leg;
            MaelstromDestination(previousElapsed, out leg);
            if (maelstromRightX - maelstromLeftX <= .00001f) leg = 0;
            // Rebase only the unfinished legs at the actual current position.
            // Mapping the original elapsed fraction onto a wider route would jump.
            maelstromRouteStartX = x;
            maelstromRouteStartTime = Mathf.Max(0f, previousElapsed);
            maelstromRouteLeg = leg;
            maelstromLeftX = left;
            maelstromRightX = right;
        }

        public bool Tick(float delta)
        {
            int previousStage = owner.CurrentAcidStage;
            owner.CurrentAcidStage = waterBarrier == null ? 0 : waterBarrier.AcidStage;
            try { return TickCore(delta); }
            finally { owner.CurrentAcidStage = previousStage; }
        }

        private float acidColorLevel;
        private Texture2D acidTexture;
        private Material acidMaterial;
        private Color32[] acidFramePixels;
        private Color32[][] acidPalettePixels;
        private static readonly Dictionary<string, Color32[][]> AcidPixels = new Dictionary<string, Color32[][]>();
        internal static float AdvanceAcidColor(float current, int target, float delta)
        { return delta <= 0f ? current : Mathf.Min(target, current + delta / .3f); }

        private void UpdateAcidColor(int frame, float delta)
        {
            if (waterBarrier == null || waterBarrier.AcidStage == 0) return;
            // All columns use the same cast clock, including delayed columns.
            // Advancing several columns must not speed up the shared gradient.
            acidColorLevel = kind == 3 ? springDamage.Acid.AdvanceColor(elapsed + castDelay, delta) :
                AdvanceAcidColor(acidColorLevel, waterBarrier.AcidStage, delta);
            if (acidTexture == null)
            {
                string key = owner.Info.path + "/" + Aquabro.SkillAssets[kind];
                if (!AcidPixels.TryGetValue(key, out acidPalettePixels))
                {
                    acidPalettePixels = new Color32[3][];
                    for (int stage = 0; stage <= 2; stage++)
                    {
                        string file = "skill-" + Aquabro.SkillAssets[kind] + (stage == 0 ? "" : "-acid-" + stage) + ".png";
                        // BroMaker's cached render texture has invalid CPU pixel data in this
                        // runtime even when GetPixels32 does not throw. Decode the original PNG.
                        Texture2D source = new Texture2D(2,2,TextureFormat.ARGB32,false);
                        try
                        {
                            if (!source.LoadImage(System.IO.File.ReadAllBytes(System.IO.Path.Combine(owner.Info.path, file))) ||
                                source.width != Counts[kind]*128 || source.height != 128)
                                throw new InvalidOperationException("Invalid acid source atlas: " + file);
                            acidPalettePixels[stage] = source.GetPixels32();
                        }
                        finally { UnityEngine.Object.Destroy(source); }
                    }
                    AcidPixels.Add(key, acidPalettePixels);
                }
                // Blend only the visible 128px frame, not the entire animation atlas.
                // The material and texture belong to this cast; cached source assets stay untouched.
                acidTexture = new Texture2D(128,128,TextureFormat.ARGB32,false);
                acidTexture.filterMode = FilterMode.Point;
                acidTexture.wrapMode = TextureWrapMode.Clamp;
                acidFramePixels = new Color32[128*128];
                acidMaterial = new Material(sprite.meshRender.sharedMaterial);
                acidMaterial.mainTexture = acidTexture;
                sprite.meshRender.material = acidMaterial;
                sprite.RecalcTexture(); sprite.CalcUVs(); sprite.UpdateUVs();
            }
            int from = System.Math.Min(1, (int)acidColorLevel), to = from + 1;
            float mix = acidColorLevel - from;
            int width = Counts[kind] * 128;
            for (int py = 0; py < 128; py++) for (int px = 0; px < 128; px++)
            {
                int sourceIndex = py * width + frame * 128 + px;
                Color32 a = acidPalettePixels[from][sourceIndex], b = acidPalettePixels[to][sourceIndex];
                acidFramePixels[py*128+px] = new Color32(
                    (byte)(a.r+(b.r-a.r)*mix), (byte)(a.g+(b.g-a.g)*mix),
                    (byte)(a.b+(b.b-a.b)*mix), a.a);
            }
            acidTexture.SetPixels32(acidFramePixels); acidTexture.Apply(false, false);
        }

        internal void PrepareSpringWater(float delta)
        {
            if (kind != 3 || delta <= 0f || obj == null) return;
            float nextElapsed = elapsed + delta;
            waterBarrier.SetFrame(nextElapsed < 0f || nextElapsed >= lifetime ? -1 :
                Mathf.Clamp(Mathf.FloorToInt(nextElapsed / .05f), 0, Counts[kind] - 1));
            waterBarrier.AbsorbPools();
        }

        private int SharkBreachFrame()
        {
            if (sharkLandingElapsed >= 0f)
                return 19 + Mathf.Clamp(Mathf.FloorToInt(sharkLandingElapsed / .05f), 0, 8);
            return SharkFlightFirstFrame + Mathf.Clamp(Mathf.FloorToInt(sharkBreachElapsed * 60f), 0, SharkFlightFrameCount - 1);
        }

        private bool TickSharkBreach(float delta)
        {
            // Bound both time and distance, including a long fall at a low FPS.
            // Analytic gravity within each step keeps the arc stable across FPS.
            int steps = System.Math.Max(1, System.Math.Max((int)System.Math.Ceiling(delta * 120f),
                (int)System.Math.Ceiling((Mathf.Abs(sharkVelocityX) + Mathf.Abs(sharkVelocityY) + SharkGravity * delta) * delta / 4f)));
            float step = delta / steps;
            for (int i = 0; i < steps; i++)
            {
                sharkBreachElapsed += step;
                if (sharkLandingElapsed >= 0f) sharkLandingElapsed += step;
                else
                {
                    float nextX = x + sharkVelocityX * step;
                    float nextY = y + sharkVelocityY * step - .5f * SharkGravity * step * step;
                    bool ceiling;
                    bool landed = owner.MoveSharkFlight(ref x, ref y, nextX, nextY, out ceiling);
                    if (Mathf.Abs(x - nextX) > .001f) sharkVelocityX = 0f;
                    sharkVelocityY = ceiling ? 0f : sharkVelocityY - SharkGravity * step;
                    if (landed)
                    {
                        sharkLandingElapsed = 0f;
                        sharkVelocityX = sharkVelocityY = 0f;
                        // The exported entry aligns the nose with the surface,
                        // preserving the interleaved tail/water/head layers.
                        // This whole frame stays fixed at the impact surface.
                        sprite.meshRender.material = ResourcesController.GetMaterial(owner.Info.path, "skill-shark-hunt.png");
                        sprite.RecalcTexture(); sprite.CalcUVs(); sprite.UpdateUVs();
                    }
                }
                int frame = SharkBreachFrame();
                if (sharkLandingElapsed < 0f)
                    owner.SkillSharkObjects(x, y, facing, scale, frame, hitObjects);
                owner.SkillSharkBlast(x, y, facing, scale, frame, hitUnits);
                if (sharkLandingElapsed >= .45f || (sharkLandingElapsed < 0f && sharkBreachElapsed >= SharkFlightLimit))
                    return false;
            }
            sharkLaunchWater.Update(sharkBreachElapsed);
            return true;
        }

        private bool TickCore(float delta)
        {
            if (delta <= 0f) return obj != null && (kind == 1 || elapsed < lifetime);
            float previousElapsed = elapsed;
            elapsed += delta;
            if (obj == null) return false;
            if (kind == 1 && !owner.CanContinueShark) return false;
            if (elapsed >= lifetime && kind != 0 && kind != 1) return false;
            if (elapsed < 0f) return true;
            obj.SetActive(true);
            int frame;
            if (kind == 1)
            {
                float flightDelta = delta;
                if (SharkControlled)
                {
                    flightDelta = 0f;
                    float travelTime = Mathf.Max(0f, Mathf.Min(elapsed, lifetime) - Mathf.Max(0f, previousElapsed));
                    if (sharkInput != 0f) facing = sharkInput;
                    // Follow the actual supporting collider before finding the floor:
                    // an ascending elevator can already be above the old ray origin.
                    if (sharkElevatorSupport != null && sharkElevatorSupport.enabled &&
                        sharkElevatorSupport.gameObject.activeInHierarchy)
                    {
                        Vector3 movement = sharkElevatorSupport.transform.position - sharkElevatorPosition;
                        x += movement.x;
                        y += movement.y;
                    }
                    owner.MoveShark(ref x, ref y, sharkInput, travelTime);
                    RememberSharkElevator();
                    if (elapsed >= lifetime)
                    {
                        BreachShark();
                        flightDelta = elapsed - lifetime;
                    }
                }
                if (!SharkControlled && !TickSharkBreach(flightDelta)) return false;
                if (SharkControlled)
                {
                    int phase = Mathf.FloorToInt(Mathf.Max(0f, elapsed - .3f) / .1f) % 4;
                    frame = elapsed < .3f ? Mathf.FloorToInt(elapsed / .05f) : (phase == 3 ? 4 : 3 + phase);
                }
                else frame = SharkBreachFrame();
            }
            else if (kind == 0)
            {
                float end = TidalTravelDuration;
                if (elapsed >= end + .5f) return false;
                if (elapsed >= end) frame = 6 + Mathf.Clamp(Mathf.FloorToInt((elapsed - end) / .05f), 0, 9);
                else if (elapsed < .3f) frame = Mathf.FloorToInt(elapsed / .1f);
                else
                {
                    int phase = Mathf.FloorToInt((elapsed - .3f) / .1f) % 4;
                    frame = phase == 3 ? 4 : 3 + phase;
                }
            }
            else if (kind==2 && elapsed < MaelstromActiveDuration)
                frame=elapsed<0.25f ? Mathf.FloorToInt(elapsed/0.05f) : 5+Mathf.FloorToInt((elapsed-0.25f)/0.07f)%19;
            else if (kind==2) frame=24+Mathf.Clamp(Mathf.FloorToInt((elapsed-MaelstromActiveDuration)/0.075f),0,7);
            else frame=Mathf.Clamp(Mathf.FloorToInt(elapsed/0.05f),0,Counts[kind]-1);
            if (waterBarrier != null)
            {
                waterBarrier.MoveFacing(facing);
                waterBarrier.SetFrame(frame);
                waterBarrier.AbsorbPools();
                owner.CurrentAcidStage = waterBarrier.AcidStage;
            }
            if (kind == 0)
            {
                float travelTime = Mathf.Max(0f, Mathf.Min(elapsed, TidalTravelDuration) - Mathf.Max(0f, previousElapsed));
                if (tidalBlocked && elapsed < TidalTravelDuration)
                    tidalBlocked = !owner.SkillWaveContact(x, y, facing, scale, frame, hitUnits, hitObjects, waveDamage, elapsed);
                if (!tidalBlocked && travelTime > 0f)
                {
                    int steps = System.Math.Max(1, (int)System.Math.Ceiling(TidalSpeed * travelTime / 8f));
                    float step = facing * TidalSpeed * travelTime / steps;
                    for (int i = 0; i < steps; i++)
                    {
                        float next = x + step;
                        if (!owner.SkillWaveContact(next, y, facing, scale, frame, hitUnits, hitObjects, waveDamage, elapsed))
                        {
                            x = next;
                            tidalBlocked = true;
                            break;
                        }
                        x = next;
                    }
                }
                waterBarrier.MoveTo(x, y);
                waterBarrier.SetFrame(frame);
            }
            else if (kind == 1)
            {
                if (SharkControlled)
                {
                    pulseTime -= delta;
                    if (pulseTime <= 0f)
                    {
                        pulseTime = .3f;
                        owner.SkillSharkPanic(x, y, facing);
                    }
                }
                else
                {
                    if (!resolved)
                    {
                        resolved = true;
                        SortOfFollow.Shake(0.25f,new Vector3(x,y,0f));
                    }
                }
            }
            else if (kind == 2)
            {
                // Removed walls can open the remaining route, bounded by the
                // original +/-48px. Keep completed turns and the 3.6s deadline.
                ExpandMaelstromRoute(previousElapsed);
                int leg;
                float destination = MaelstromDestination(elapsed, out leg);
                x = owner.MoveSkillHorizontally(x, y, destination);
                waterBarrier.MoveTo(x, y);
                if (elapsed < MaelstromActiveDuration)
                {
                    // Anchor pulses to cast time instead of accumulating frame
                    // overshoot. Keep the last 0.1s clear: tanks reduce hits
                    // within 0.096s of a previous hit to one damage.
                    bool pulse = elapsed >= pulseTime && elapsed < MaelstromActiveDuration - .1f;
                    if (pulse) pulseTime = (Mathf.FloorToInt(elapsed / MaelstromPulseInterval) + 1) * MaelstromPulseInterval;
                    owner.SkillMaelstromPullAtTime(x, y, facing, scale, frame, delta, pulse, maelstromCaught, elapsed);
                }
                if (!resolved && elapsed >= MaelstromActiveDuration)
                {
                    resolved=true;
                    // A delayed update can skip dissolve frames; use the same
                    // frame as the sprite instead of the last active mask.
                    owner.SkillMaelstromFinish(x, y, facing, scale, frame, maelstromCaught, hitUnits);
                }
            }
            else if (kind == 3)
            {
                owner.SkillSpringBlast(x,y,facing,scale,frame,springDamage,elapsed + castDelay);
                if (!resolved)
                {
                    resolved=true;
                    SortOfFollow.Shake(0.25f,new Vector3(x,y,0f));
                }
            }
            obj.transform.position=new Vector3(x,y+48f*scale,VisualDepth);
            // Parent scale may flip while the hero moves; freeze travel facing.
            obj.transform.localScale=new Vector3(facing/(owner.transform.localScale.x<0f?-1f:1f),1f,1f);
            UpdateAcidColor(frame, delta);
            sprite.SetLowerLeftPixel(acidTexture == null ? frame*128 : 0,128);
            return true;
        }

        public void Dispose()
        {
            if (kind == 1) owner.EndShark(this);
            if (waterBarrier!=null) waterBarrier.Dispose();
            if (sharkLaunchWater != null) sharkLaunchWater.Dispose();
            if (obj != null) UnityEngine.Object.Destroy(obj);
            if (acidMaterial != null) UnityEngine.Object.Destroy(acidMaterial);
            if (acidTexture != null) UnityEngine.Object.Destroy(acidTexture);
        }
    }
}

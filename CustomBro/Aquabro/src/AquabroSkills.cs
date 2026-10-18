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
                !usingSpecial && !usingPockettedSpecial && !doingMelee && !IsIncapacitated() && activeShark == null)
            {
                int slot = Mathf.Clamp(playerNum, 0, 3);
                SkillSelections[slot] = (SkillSelections[slot] + 1) % SkillNames.Length;
                if (IsMine) SpecialAmmo = originalSpecialAmmo;
                ClearSkillPreview();
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
            if (activeShark != null)
            {
                if (health > 0 && !IsIncapacitated()) activeShark.BreachShark();
                return;
            }
            if (health <= 0 || IsIncapacitated() || doingMelee || usingSpecial || activeShark != null) return;
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
            HashSet<Unit> sharedHits = new HashSet<Unit>();
            SpringDamageState sharedSpring = new SpringDamageState();
            for (int i = -3; i <= 3; i++)
            {
                Vector2 pos = ClampSkillDestination(X + i * 30f, Y + 12f);
                // Stop the sequence at walls rather than stacking columns there.
                if (Mathf.Abs(pos.x - (X + i * 30f)) > 12f) continue;
                float ground = GroundBelow(pos.x, Y + 12f, 55f);
                AddSkillVisual(3, pos.x, ground, i < 0 ? -1f : 1f, 1.4f,
                    Mathf.Abs(i) * 0.14f, Mathf.Abs(i) == 3 ? 0.9f : 0.65f, sharedHits, sharedSpring);
            }
            SortOfFollow.Shake(0.7f, new Vector3(X, Y, 0f));
        }

        private Vector2 ClampSkillDestination(float x, float y)
        {
            Vector3 origin = new Vector3(X, Y + 14f, 0f);
            Vector3 diff = new Vector3(x, y, 0f) - origin;
            RaycastHit hit;
            if (diff.magnitude > 1f && Physics.Raycast(origin, diff.normalized, out hit, diff.magnitude, Map.groundLayer))
                return new Vector2(hit.point.x - diff.normalized.x * 8f, hit.point.y - diff.normalized.y * 8f);
            return new Vector2(x, y);
        }

        private float GroundBelow(float x, float y, float range)
        {
            RaycastHit hit;
            return Physics.Raycast(new Vector3(x, y, 0f), Vector3.down, out hit, range, Map.groundLayer) ? hit.point.y : y - 12f;
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
        {
            Vector3 diff = new Vector3(hitX - x, hitY - y, 0f);
            if (diff.magnitude < 8f) return true;
            foreach (RaycastHit hit in Physics.RaycastAll(new Vector3(x, y, 0f), diff.normalized,
                diff.magnitude - 4f, Map.groundLayer))
            {
                Collider contact = hit.collider;
                if (contact == null || contact.isTrigger || contact.GetComponentInParent<Unit>() != null ||
                    contact.GetComponent<DamageRelay>() != null) continue;
                if (ignoreProps && (contact.GetComponentInParent<FallingBlock>() != null ||
                    contact.GetComponentInParent<Doodad>() != null)) continue;
                return false;
            }
            return true;
        }

        private static bool SkillMaskTouchesRectangle(int kind, float x, float y, float facing, float scale,
            int frame, Bounds bounds)
        {
            if (bounds.max.y <= y + 1f) return false;
            float left = 64f + (bounds.min.x - x) * facing / scale;
            float right = 64f + (bounds.max.x - x) * facing / scale;
            float top = 112f - (bounds.max.y - y) / scale;
            float bottom = 112f - (Mathf.Max(bounds.min.y, y + 1f) - y) / scale;
            float margin = 5f / scale;
            switch (kind)
            {
                case 0: return TidalWavePixelMask.IntersectsRect(frame, left, top, right, bottom, margin);
                case 1: return SharkPixelMask.IntersectsRect(frame, left, top, right, bottom, margin);
                case 2: return MaelstromPixelMask.IntersectsRect(frame, left, top, right, bottom, margin);
                default: return SpringPixelMask.IntersectsRect(frame, left, top, right, bottom, margin);
            }
        }

        private HashSet<Unit> SkillDamageRelays(int kind, float x, float y, float facing, float scale, int frame,
            int damage, float forceX, float forceY, bool radial, HashSet<Unit> alreadyHit)
        {
            return SkillDamageRelays(kind, x, y, facing, scale, frame, damage, forceX, forceY, radial, alreadyHit, null, 0f);
        }

        private HashSet<Unit> SkillDamageRelays(int kind, float x, float y, float facing, float scale, int frame,
            int damage, float forceX, float forceY, bool radial, HashSet<Unit> alreadyHit, SpringDamageState spring, float castTime)
        {
            HashSet<Unit> partTargets = new HashSet<Unit>();
            // Boss parts can be far from Unit.X/Y or absent from Map.units.
            // Send damage through the part to preserve armour and weak-point rules.
            foreach (Collider contact in Physics.OverlapSphere(new Vector3(x, y + 48f * scale, 0f),
                96f * scale + 5f, Map.groundLayer | Map.groundAndDamageableObjects))
            {
                if (contact == null || !contact.enabled || !contact.gameObject.activeInHierarchy) continue;
                DamageRelay relay = contact.GetComponent<DamageRelay>();
                Unit target = relay == null ? null : relay.unit;
                if (!ValidSkillDamageTarget(target)) continue;
                partTargets.Add(target);
                if (alreadyHit.Contains(target)) continue;
                // An armoured/closed part must not consume the cast's hit before
                // an exposed weak point is tested later in the same query.
                if (relay.immunity == DamageType.Explosion ||
                    (relay.ignoreDamageWhileColliderActive != null && relay.ignoreDamageWhileColliderActive.enabled &&
                        relay.ignoreDamageWhileColliderActive.gameObject.activeInHierarchy) ||
                    (relay.ignoreDamageWhileOtherColliderActive != null && relay.ignoreDamageWhileOtherColliderActive.enabled &&
                        relay.ignoreDamageWhileOtherColliderActive.gameObject.activeInHierarchy)) continue;
                Bounds bounds = contact.bounds;
                if (!SkillMaskTouchesRectangle(kind, x, y, facing, scale, frame, bounds)) continue;
                float hitX = Mathf.Clamp(x, bounds.min.x, bounds.max.x);
                float hitY = Mathf.Clamp(y + 48f * scale, Mathf.Max(y + 1f, bounds.min.y), bounds.max.y);
                if (!SkillContactVisible(x, hitY, hitX, hitY, kind == 2)) continue;
                bool first = true;
                if (spring != null && !spring.TryHit(target, castTime, out first)) continue;
                alreadyHit.Add(target);
                float fx = radial ? (hitX < x ? -1f : 1f) * Mathf.Abs(forceX) : forceX;
                MapController.Damage_Networked(this, contact.gameObject,
                    ValueOrchestrator.GetModifiedDamage(damage, playerNum), DamageType.Explosion,
                    first ? fx : 0f, first ? forceY : 0f, hitX, hitY);
            }
            return partTargets;
        }

        internal void SkillBlast(float x, float y, float radius, int damage, float forceX, float forceY, HashSet<Unit> alreadyHit, bool radial)
        {
            if (!IsMine) return;
            foreach (Unit target in Map.GetUnitsInRange(radius + 16f, radius + 16f, x, y, true))
            {
                if (!ValidSkillDamageTarget(target)) continue;
                float dx = target.X - x, dy = target.Y + 6f - y;
                if (dx * dx + dy * dy > radius * radius || !SkillLineOfSight(x, y, target) ||
                    (alreadyHit != null && !alreadyHit.Add(target))) continue;
                int direction = dx < 0f ? -1 : 1;
                Map.KnockAndDamageUnit(this, target, ValueOrchestrator.GetModifiedDamage(damage, playerNum),
                    DamageType.Explosion, radial ? direction * Mathf.Abs(forceX) : forceX, forceY, direction, true, x, y, true);
            }
            Map.HitProjectiles(playerNum, 12, DamageType.Explosion, radius, x, y, forceX, forceY, 0.25f);
        }

        private void AddSkillVisual(int kind, float x, float y, float facing, float lifetime, float delay, float scale)
        {
            AddSkillVisual(kind, x, y, facing, lifetime, delay, scale, null, null);
        }

        private void AddSkillVisual(int kind, float x, float y, float facing, float lifetime, float delay, float scale,
            HashSet<Unit> sharedHits, SpringDamageState sharedSpring)
        {
            skillVisuals.Add(new AquabroSkillVisual(this, kind, x, y, facing, lifetime, delay, scale, sharedHits, sharedSpring));
        }

        internal void SkillSpringBlast(float x, float y, float facing, float scale, int frame,
            SpringDamageState spring, float castTime)
        {
            if (!IsMine || frame < 0 || frame >= AquabroSkillVisual.SpringDissolveStartFrame) return;
            // The build exports this mask from the very same composite as the PNG.
            HashSet<Unit> pulseHits = new HashSet<Unit>();
            HashSet<Unit> partTargets = SkillDamageRelays(3, x, y, facing, scale, frame,
                4, 70f, 650f, true, pulseHits, spring, castTime);
            float range = 64f * scale + 5f + 16f;
            foreach (Unit target in Map.GetUnitsInRange(range, range, x, y + 48f * scale, true))
            {
                if (!ValidSkillDamageTarget(target) || partTargets.Contains(target) || pulseHits.Contains(target)) continue;
                float dx = target.X - x, dy = target.Y + 6f - y;
                if (!SpringPixelMask.Contains(frame, 64f + dx * facing / scale, 112f - dy / scale, 5f / scale) ||
                    !SkillLineOfSight(x, y + Mathf.Clamp(dy, 0f, 112f * scale), target)) continue;
                bool first;
                if (!spring.TryHit(target, castTime, out first)) continue;
                pulseHits.Add(target);
                int direction = dx < 0f ? -1 : 1;
                Map.KnockAndDamageUnit(this, target, ValueOrchestrator.GetModifiedDamage(4, playerNum),
                    DamageType.Explosion, first ? direction * 70f : 0f, first ? 650f : 0f,
                    direction, first, x, y + 24f, true);
            }
        }

        internal Vector2 FindMaelstromDestination(float facing)
        {
            Vector3 origin = new Vector3(X, Y + 14f, 0f);
            Vector2 best = new Vector2();
            float bestScore = float.PositiveInfinity;
            foreach (Unit target in Map.GetUnitsInRange(180f, 112f, X, Y + 6f, false))
            {
                if (!(target is Mook) || target.playerNum >= 0 || !ValidSkillTarget(target)) continue;
                float dx = target.X - X, dy = target.Y - Y;
                if (Mathf.Abs(dx) > 180f || Mathf.Abs(dy) > 96f || dx * facing < 0f) continue;
                Vector3 destination = new Vector3(target.X, target.Y + 14f, 0f);
                Vector3 diff = destination - origin;
                RaycastHit wall, floor;
                if (diff.magnitude > 1f && RaycastSharkTerrain(origin, diff.normalized, diff.magnitude, out wall)) continue;
                // Use real terrain below the enemy, passing through stacked props.
                if (!RaycastSharkTerrain(new Vector3(target.X, target.Y + 2f, 0f), Vector3.down, 80f, out floor)) continue;
                float score = dx * dx + dy * dy;
                if (score >= bestScore) continue;
                bestScore = score; best = new Vector2(target.X, floor.point.y);
            }
            if (bestScore < float.PositiveInfinity) return best;
            // Without a reachable enemy, keep the forward cast and stop at solid
            // terrain. Crates, barrels and decorative objects never shorten it.
            Vector3 fallback = new Vector3(X + facing * 70f, Y + 14f, 0f);
            Vector3 forward = fallback - origin;
            RaycastHit hit;
            if (RaycastSharkTerrain(origin, forward.normalized, forward.magnitude, out hit))
                fallback = new Vector3(hit.point.x - forward.normalized.x * 8f, fallback.y, 0f);
            float ground = RaycastSharkTerrain(fallback, Vector3.down, 80f, out hit) ? hit.point.y : Y + 2f;
            return new Vector2(fallback.x, ground);
        }

        private bool MaelstromLineOfSight(float x, float y, Unit target)
        {
            Vector3 diff = new Vector3(target.X - x, target.Y + 6f - y, 0f);
            RaycastHit hit;
            return diff.magnitude < 8f || !RaycastSharkTerrain(new Vector3(x, y, 0f),
                diff.normalized, diff.magnitude - 4f, out hit);
        }

        private bool MaelstromTouchesTarget(float x, float y, float facing, float scale, int frame,
            Unit target, HashSet<Unit> caught)
        {
            if (!ValidSkillDamageTarget(target)) return false;
            float halfWidth = Mathf.Max(1f, target.width * .5f);
            float top = target.Y + Mathf.Max(target.headHeight, target.height);
            if (top <= y + 1f || target.Y > y + 112f * scale + 5f ||
                Mathf.Abs(target.X - x) > 64f * scale + halfWidth + 5f) return false;
            bool contact = MaelstromPixelMask.IntersectsRect(frame,
                64f + (target.X - halfWidth - x) * facing / scale, 112f - (top - y) / scale,
                64f + (target.X + halfWidth - x) * facing / scale,
                112f - (Mathf.Max(target.Y, y + 1f) - y) / scale, 5f / scale);
            // A captured enemy may enter the transparent mouth or a gap between
            // rotating sheets. Keep pulling it while it remains inside the effect.
            if (!contact && !caught.Contains(target)) return false;
            // Test at the enemy's height: a platform below it must not block an
            // exposed upper rim just because the old ray began near the floor.
            return MaelstromLineOfSight(x, target.Y + 6f, target);
        }

        internal void SkillMaelstromPull(float x, float y, float facing, float scale, int frame,
            float delta, bool pulse, HashSet<Unit> caught)
        {
            if (!IsMine || delta <= 0f || frame < 0 || frame >= AquabroSkillVisual.MaelstromDissolveStartFrame) return;
            HashSet<Unit> pulseHits = new HashSet<Unit>();
            HashSet<Unit> partTargets = pulse ? SkillDamageRelays(2, x, y, facing, scale, frame,
                AquabroSkillVisual.MaelstromPulseDamage, 0f, 0f, false, pulseHits) : null;
            float range = 64f * scale + 64f;
            foreach (Unit target in Map.GetUnitsInRange(range, range, x, y + 48f * scale, true))
            {
                if (!MaelstromTouchesTarget(x, y, facing, scale, frame, target, caught)) continue;
                caught.Add(target);
                if (pulse && !partTargets.Contains(target))
                    Map.KnockAndDamageUnit(this, target, ValueOrchestrator.GetModifiedDamage(AquabroSkillVisual.MaelstromPulseDamage, playerNum),
                        DamageType.Explosion, 0f, 0f, 0, false, x, target.Y + 6f, true);
                if (!(target is Mook) || target.health <= 0) continue;
                float vx = Mathf.MoveTowards(target.xI, Mathf.Clamp((x - target.X) * 6f, -170f, 170f), delta * 1200f);
                float vy = Mathf.MoveTowards(target.yI, Mathf.Clamp((y + 48f * scale - target.Y - 6f) * 7f, -160f, 260f), delta * 2000f);
                ApplyMaelstromKnock(target, vx, vy);
            }
        }

        private static void ApplyMaelstromKnock(Unit target, float vx, float vy)
        {
            // MookBigGuy divides horizontal impulse by 20 and vertical by 5.
            // Compensate only this skill's continuous suction, including subclasses.
            bool heavy = target is MookBigGuy;
            if (heavy)
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

        internal void SkillMaelstromFinish(float x, float y, float facing, float scale,
            HashSet<Unit> caught, HashSet<Unit> alreadyHit)
        {
            if (!IsMine) return;
            HashSet<Unit> partTargets = SkillDamageRelays(2, x, y, facing, scale, AquabroSkillVisual.MaelstromDissolveStartFrame - 1,
                AquabroSkillVisual.MaelstromFinishDamage, 220f, 500f, true, alreadyHit);
            float range = 64f * scale + 64f;
            foreach (Unit target in Map.GetUnitsInRange(range, range, x, y + 48f * scale, true))
            {
                if (partTargets.Contains(target) || !MaelstromTouchesTarget(x, y, facing, scale, AquabroSkillVisual.MaelstromDissolveStartFrame - 1, target, caught) ||
                    !alreadyHit.Add(target)) continue;
                int direction = target.X < x ? -1 : 1;
                Map.KnockAndDamageUnit(this, target, ValueOrchestrator.GetModifiedDamage(AquabroSkillVisual.MaelstromFinishDamage, playerNum),
                    DamageType.Explosion, direction * 220f, 500f, direction, true, x, target.Y + 6f, true);
            }
        }

        private static bool RaycastSharkTerrain(Vector3 origin, Vector3 direction, float distance, out RaycastHit nearest)
        {
            nearest = new RaycastHit();
            float best = float.PositiveInfinity;
            bool found = false;
            // Crates and barrels also use Ground. Inspect every hit so ignoring
            // a prop still leaves a real wall/floor behind it available to stop us.
            // Elevators inherit Doodad, but their solid colliders are terrain.
            foreach (RaycastHit hit in Physics.RaycastAll(origin, direction, distance, Map.groundLayerStrict))
            {
                Collider contact = hit.collider;
                if (contact == null || contact.isTrigger ||
                    contact.GetComponentInParent<Unit>() != null || contact.GetComponent<DamageRelay>() != null ||
                    contact.GetComponentInParent<FallingBlock>() != null ||
                    (contact.GetComponentInParent<Doodad>() != null && contact.GetComponentInParent<Elevator>() == null)) continue;
                if (hit.distance >= best) continue;
                best = hit.distance; nearest = hit; found = true;
            }
            return found;
        }

        private bool FindSharkFloor(float x, float y, bool dropDown, out float floorY)
        {
            RaycastHit floor;
            bool found = RaycastSharkTerrain(new Vector3(x, y + 2f, 0f), Vector3.down,
                float.PositiveInfinity, out floor);
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
            return RaycastSharkTerrain(new Vector3(x, y + 2f, 0f), Vector3.down, 4f, out floor) &&
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

        internal void SkillSharkBlast(float x, float y, float facing, float scale, int frame, HashSet<Unit> alreadyHit)
        {
            if (!IsMine || frame < 6 || frame >= 27) return;
            HashSet<Unit> partTargets = SkillDamageRelays(1, x, y, facing, scale, frame, 28, facing * 180f, 420f, false, alreadyHit);
            float range = 64f * scale + 21f;
            foreach (Unit target in Map.GetUnitsInRange(range, range, x, y + 48f * scale, true))
            {
                if (!ValidSkillDamageTarget(target) || partTargets.Contains(target) || alreadyHit.Contains(target)) continue;
                float dx = target.X - x, dy = target.Y + 6f - y;
                if (!SharkPixelMask.Contains(frame, 64f + dx * facing / scale, 112f - dy / scale, 5f / scale) ||
                    !SkillLineOfSight(x, y + Mathf.Clamp(dy, 0f, 112f * scale), target)) continue;
                alreadyHit.Add(target);
                Map.KnockAndDamageUnit(this, target, ValueOrchestrator.GetModifiedDamage(28, playerNum),
                    DamageType.Explosion, facing * 180f, 420f, facing < 0f ? -1 : 1, true, x, y + 24f, true);
            }
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
            if (!IsMine || frame < 6 || frame >= 27) return;
            foreach (Collider contact in Physics.OverlapSphere(new Vector3(x, y + 48f * scale, 0f),
                82f * scale + 5f, Map.groundAndDamageableObjects))
            {
                if (contact == null || !contact.enabled || !contact.gameObject.activeInHierarchy) continue;
                DamageRelay relay = contact.GetComponent<DamageRelay>();
                if (contact.GetComponentInParent<Unit>() != null || (relay != null && relay.unit != null)) continue;
                Bounds bounds = contact.bounds;
                if (!SharkTouchesRectangle(x,y,facing,scale,frame,bounds.min.x,bounds.min.y,bounds.max.x,bounds.max.y) ||
                    !alreadyHit.Add(contact.gameObject)) continue;
                MapController.Damage_Networked(this, contact.gameObject, ValueOrchestrator.GetModifiedDamage(28, playerNum),
                    DamageType.Crush, facing * 180f, 420f, bounds.center.x, bounds.center.y);
            }
            for (int i = Map.destroyableDoodads.Count - 1; i >= 0; i--)
            {
                Doodad doodad = Map.destroyableDoodads[i];
                if (doodad == null || !doodad.gameObject.activeInHierarchy ||
                    (playerNum >= 0 ? doodad.immuneToHeroDamage : !doodad.CanBeDamagedByMooks)) continue;
                if (!SharkTouchesRectangle(x,y,facing,scale,frame,
                    doodad.centerX - doodad.width * .5f, doodad.centerY - doodad.height * .5f,
                    doodad.centerX + doodad.width * .5f, doodad.centerY + doodad.height * .5f) ||
                    !alreadyHit.Add(doodad.gameObject)) continue;
                bool hit = false;
                doodad.DamageOptional(new DamageObject(ValueOrchestrator.GetModifiedDamage(28, playerNum),
                    DamageType.Crush, facing * 180f, 420f, x, y, this), ref hit);
            }
        }

        private static bool SharkTouchesRectangle(float x,float y,float facing,float scale,int frame,
            float left,float bottom,float right,float top)
        {
            // The waterline sits on the support floor. The contact margin must
            // not extend damage into terrain or objects entirely below it.
            if (top <= y + 1f) return false;
            bottom = Mathf.Max(bottom, y + 1f);
            return SharkPixelMask.IntersectsRect(frame,64f+(left-x)*facing/scale,112f-(top-y)/scale,
                64f+(right-x)*facing/scale,112f-(bottom-y)/scale,5f/scale);
        }

        internal bool SkillWaveContact(float x, float y, float facing, float scale, int frame,
            HashSet<Unit> alreadyHit, HashSet<GameObject> hitObjects)
        {
            if (!IsMine || frame < 0 || frame >= AquabroSkillVisual.TidalDissolveStartFrame) return true;
            HashSet<Unit> partTargets = SkillDamageRelays(0, x, y, facing, scale, frame, 26, facing * 420f, 300f, false, alreadyHit);
            Collider[] contacts = Physics.OverlapSphere(new Vector3(x, y + 48f * scale, 0f),
                82f * scale + 5f, Map.groundAndDamageableObjects);
            Array.Sort(contacts, delegate(Collider a, Collider b)
            { return (a.bounds.center.x * facing).CompareTo(b.bounds.center.x * facing); });
            foreach (Collider contact in contacts)
            {
                if (contact == null || !contact.enabled || !contact.gameObject.activeInHierarchy) continue;
                // Units use the body-mask damage pass below, including large
                // enemies whose colliders may also be in the terrain query.
                DamageRelay relay = contact.GetComponent<DamageRelay>();
                if (contact.GetComponentInParent<Unit>() != null || (relay != null && relay.unit != null)) continue;
                Bounds bounds = contact.bounds;
                if (!WaveTouchesRectangle(x,y,facing,scale,frame,bounds.min.x,bounds.min.y,bounds.max.x,bounds.max.y)) continue;
                if (hitObjects.Add(contact.gameObject))
                    MapController.Damage_Networked(this, contact.gameObject,
                        ValueOrchestrator.GetModifiedDamage(24, playerNum), DamageType.Crush,
                        facing * 420f, 80f, bounds.center.x, bounds.center.y);
                // A surviving wall stops the wave. Floor contact alone does not.
                if (contact != null && contact.enabled && contact.gameObject.activeInHierarchy &&
                    bounds.max.y > y + 8f) return false;
            }
            // Doodads use the same five-pixel boundary, without a separate area blast.
            for (int i=Map.destroyableDoodads.Count-1;i>=0;i--)
            {
                Doodad doodad=Map.destroyableDoodads[i];
                if (doodad==null || !doodad.gameObject.activeInHierarchy ||
                    (playerNum>=0 ? doodad.immuneToHeroDamage : !doodad.CanBeDamagedByMooks)) continue;
                if (!WaveTouchesRectangle(x,y,facing,scale,frame,
                    doodad.centerX-doodad.width*.5f,doodad.centerY-doodad.height*.5f,
                    doodad.centerX+doodad.width*.5f,doodad.centerY+doodad.height*.5f)) continue;
                bool hit=false;
                if (hitObjects.Add(doodad.gameObject))
                    doodad.DamageOptional(new DamageObject(ValueOrchestrator.GetModifiedDamage(24,playerNum),
                        DamageType.Crush,facing*420f,80f,x,y,this),ref hit);
                if (hit && doodad.isImpenetrable) return false;
            }
            // The game's broad query checks feet positions without body size.
            // Leave room for large bodies touching the outer water pixels.
            foreach (Unit target in Map.GetUnitsInRange(80f * scale + 64f, 80f * scale + 64f, x, y + 48f * scale, true))
            {
                if (!ValidSkillDamageTarget(target) || partTargets.Contains(target) || alreadyHit.Contains(target)) continue;
                float halfWidth = Mathf.Max(1f, target.width * .5f);
                float top = target.Y + Mathf.Max(target.headHeight, target.height);
                float dy = target.Y + 6f - y;
                if (!WaveTouchesRectangle(x, y, facing, scale, frame,
                    target.X - halfWidth, target.Y, target.X + halfWidth, top) ||
                    !SkillLineOfSight(x, y + Mathf.Clamp(dy, 0f, 100f * scale), target)) continue;
                alreadyHit.Add(target);
                Map.KnockAndDamageUnit(this, target, ValueOrchestrator.GetModifiedDamage(26, playerNum),
                    DamageType.Explosion, facing * 420f, 300f, facing < 0f ? -1 : 1, true, x, y + 24f, true);
            }
            return true;
        }

        private static bool WaveTouchesRectangle(float x,float y,float facing,float scale,int frame,
            float left,float bottom,float right,float top)
        {
            // Keep the contact margin above the waterline, just like the shark.
            if (top <= y + 1f) return false;
            bottom = Mathf.Max(bottom, y + 1f);
            return TidalWavePixelMask.IntersectsRect(frame,64f+(left-x)*facing/scale,112f-(top-y)/scale,
                64f+(right-x)*facing/scale,112f-(bottom-y)/scale,5f/scale);
        }

        private void TickSkillPreview(float delta)
        {
            if (health <= 0) { ClearSkillPreview(); return; }
            if (delta <= 0f) return;
            for (int i = skillVisuals.Count - 1; i >= 0; i--)
                if (!skillVisuals[i].Tick(delta)) { skillVisuals[i].Dispose(); skillVisuals.RemoveAt(i); }
        }

        private void ClearSkillPreview()
        {
            foreach (AquabroSkillVisual effect in skillVisuals) effect.Dispose();
            skillVisuals.Clear();
        }

        private void OnDisable() { ClearSkillPreview(); }
    }

    // Shared by all seven columns of one cast, including all boss parts.
    internal sealed class SpringDamageState
    {
        private readonly Dictionary<Unit, int> hits = new Dictionary<Unit, int>();
        private readonly Dictionary<Unit, float> nextHit = new Dictionary<Unit, float>();
        internal bool TryHit(Unit target, float castTime, out bool first)
        {
            int count;
            hits.TryGetValue(target, out count);
            first = count == 0;
            if (count >= 7 || (!first && castTime + .00001f < nextHit[target])) return false;
            hits[target] = count + 1;
            // Actual hit time prevents adjacent columns or low FPS from
            // compressing pulses into the original vehicle 0.096s cooldown.
            nextHit[target] = castTime + .12f;
            return true;
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
        private readonly HashSet<Unit> hitUnits;
        private readonly SpringDamageState springDamage;
        private readonly float castDelay;
        private readonly float originX;
        private readonly float maelstromLeftX, maelstromRightX;
        private readonly HashSet<Unit> maelstromCaught = new HashSet<Unit>();
        private readonly HashSet<GameObject> hitObjects = new HashSet<GameObject>();
        private readonly WaterProjectileBarrier waterBarrier;
        private float x, y, elapsed, pulseTime;
        private bool resolved;
        private float tidalStoppedAt = -1f;
        private static readonly int[] Counts = {16,28,32,28};
        private float sharkBreachElapsed = -1f, sharkInput, sharkDashProgress;
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
            owner.ReleaseSharkControl(this);
        }

        public AquabroSkillVisual(Aquabro owner, int kind, float x, float y, float facing, float lifetime, float delay, float scale)
            : this(owner, kind, x, y, facing, lifetime, delay, scale, null) {}

        public AquabroSkillVisual(Aquabro owner, int kind, float x, float y, float facing, float lifetime, float delay, float scale, HashSet<Unit> sharedHits)
            : this(owner, kind, x, y, facing, lifetime, delay, scale, sharedHits, null) {}

        public AquabroSkillVisual(Aquabro owner, int kind, float x, float y, float facing, float lifetime, float delay,
            float scale, HashSet<Unit> sharedHits, SpringDamageState sharedSpring)
        {
            hitUnits = sharedHits ?? new HashSet<Unit>();
            springDamage = sharedSpring ?? new SpringDamageState();
            castDelay = delay;
            this.owner = owner; this.kind = kind; this.x = x; this.y = y;
            originX = x;
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
            if (kind==0 || kind==2 || kind==3) waterBarrier=new WaterProjectileBarrier(owner,kind,x,y,facing,scale);
            obj.SetActive(false);
        }

        private void RememberSharkElevator()
        {
            sharkElevatorSupport = owner.FindSharkElevatorSupport(x, y);
            if (sharkElevatorSupport != null)
                sharkElevatorPosition = sharkElevatorSupport.transform.position;
        }

        public bool Tick(float delta)
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
                if (SharkControlled)
                {
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
                        sharkBreachElapsed = elapsed - lifetime;
                    }
                }
                else sharkBreachElapsed += delta;
                if (sharkBreachElapsed >= 1.1f) return false;
                if (!SharkControlled)
                {
                    float t = Mathf.Clamp(sharkBreachElapsed / .45f, 0f, 1f);
                    float progress = t * t * (3f - 2f * t);
                    x = owner.MoveSkillHorizontally(x, y, x + facing * 24f * (progress - sharkDashProgress));
                    sharkDashProgress = progress;
                }
                if (SharkControlled)
                {
                    int phase = Mathf.FloorToInt(Mathf.Max(0f, elapsed - .3f) / .1f) % 4;
                    frame = elapsed < .3f ? Mathf.FloorToInt(elapsed / .05f) : (phase == 3 ? 4 : 3 + phase);
                }
                else frame = 6 + Mathf.Clamp(Mathf.FloorToInt(sharkBreachElapsed / .05f), 0, 21);
            }
            else if (kind == 0)
            {
                float end = tidalStoppedAt >= 0f ? tidalStoppedAt : TidalTravelDuration;
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
            if (waterBarrier!=null) waterBarrier.SetFrame(frame);
            if (kind == 0)
            {
                float travelTime = Mathf.Max(0f, Mathf.Min(elapsed, TidalTravelDuration) - Mathf.Max(0f, previousElapsed));
                if (tidalStoppedAt < 0f && travelTime > 0f)
                {
                    int steps = System.Math.Max(1, (int)System.Math.Ceiling(TidalSpeed * travelTime / 8f));
                    float step = facing * TidalSpeed * travelTime / steps;
                    for (int i = 0; i < steps; i++)
                    {
                        float next = x + step;
                        if (!owner.SkillWaveContact(next, y, facing, scale, frame, hitUnits, hitObjects))
                        {
                            tidalStoppedAt = elapsed;
                            frame = TidalDissolveStartFrame;
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
                    owner.SkillSharkObjects(x,y,facing,scale,frame,hitObjects);
                    owner.SkillSharkBlast(x,y,facing,scale,frame,hitUnits);
                    if (!resolved)
                    {
                        resolved = true;
                        SortOfFollow.Shake(0.7f,new Vector3(x,y,0f));
                    }
                }
            }
            else if (kind == 2)
            {
                // Move from the first visible frame at constant speed, reversing
                // without a hold: centre -> right -> left -> right, then dissolve.
                // Fit all three legs to the reachable span, so touching a wall
                // reverses immediately instead of waiting for an off-map phase.
                float firstLeg = maelstromRightX - originX;
                float span = maelstromRightX - maelstromLeftX;
                float travel = Mathf.Clamp(elapsed / MaelstromActiveDuration, 0f, 1f) * (firstLeg + 2f * span);
                float destination = travel < firstLeg ? originX + travel :
                    travel < firstLeg + span ? maelstromRightX - (travel - firstLeg) :
                    maelstromLeftX + (travel - firstLeg - span);
                x = owner.MoveSkillHorizontally(x, y, destination);
                waterBarrier.MoveTo(x, y);
                if (elapsed < MaelstromActiveDuration)
                {
                    // Anchor pulses to cast time instead of accumulating frame
                    // overshoot. Keep the last 0.1s clear: tanks reduce hits
                    // within 0.096s of a previous hit to one damage.
                    bool pulse = elapsed >= pulseTime && elapsed < MaelstromActiveDuration - .1f;
                    if (pulse) pulseTime = (Mathf.FloorToInt(elapsed / MaelstromPulseInterval) + 1) * MaelstromPulseInterval;
                    owner.SkillMaelstromPull(x, y, facing, scale, frame, delta, pulse, maelstromCaught);
                }
                if (!resolved && elapsed >= MaelstromActiveDuration)
                {
                    resolved=true;
                    owner.SkillMaelstromFinish(x, y, facing, scale, maelstromCaught, hitUnits);
                    SortOfFollow.Shake(0.65f,new Vector3(x,y,0f));
                }
            }
            else if (kind == 3)
            {
                owner.SkillSpringBlast(x,y,facing,scale,frame,springDamage,elapsed + castDelay);
                if (!resolved)
                {
                    resolved=true;
                    SortOfFollow.Shake(0.5f,new Vector3(x,y,0f));
                }
            }
            obj.transform.position=new Vector3(x,y+48f*scale,VisualDepth);
            // Parent scale may flip while the hero moves; freeze travel facing.
            obj.transform.localScale=new Vector3(facing/(owner.transform.localScale.x<0f?-1f:1f),1f,1f);
            sprite.SetLowerLeftPixel(frame*128,128);
            return true;
        }

        public void Dispose()
        {
            if (kind == 1) owner.EndShark(this);
            if (waterBarrier!=null) waterBarrier.Dispose();
            if (obj != null) UnityEngine.Object.Destroy(obj);
        }
    }
}

using System.Collections.Generic;
using System;
using System.Reflection;
using BroMakerLib;
using BroMakerLib.CustomObjects.Bros;
using BroMakerLib.CustomObjects.Projectiles;
using Rogueforce;
using UnityEngine;

namespace Aquabro
{
    [HeroPreset("Aquabro", HeroType.Rambro)]
    public class Aquabro : CustomHero
    {
        private CustomProjectile tridentPrefab;
        private CustomProjectile chargedTridentPrefab;
        private TridentAttackState tridentAttack;
        private AudioClip[] tridentSounds;
        private float weaponOffsetX;
        private float weaponOffsetY;
        private float bodyVisualOffsetX;
        private float bodyVisualOffsetY;
        private bool weaponUsesBodyCoordinates;
        private bool weaponHiddenForTraversal;
        private bool landingStartedThisUpdate;
        private readonly TridentMovementAnimation movementAnimation = new TridentMovementAnimation();
        protected Texture2D risingWaveTexture;
        private Texture2D customBodyTexture;
        private Texture2D customGunTexture;
        private Material customBodyMaterial;
        private Material customGunMaterial;
        private FieldInfo spriteTextureField;
        private static bool helicopterHarmonyPatched;

        protected override void Awake()
        {
            base.Awake();
            this.tridentAttack = new TridentAttackState();
            this.tridentPrefab = CustomProjectile.CreatePrefab<TridentProjectile>();
            this.chargedTridentPrefab = CustomProjectile.CreatePrefab<ChargedTridentProjectile>();
            this.risingWaveTexture = ResourcesController.GetTexture(Info.path, "RisingWave.png");
            this.customBodyTexture = ResourcesController.GetTexture(Info.path, "sprite.png");
            this.customGunTexture = ResourcesController.GetTexture(Info.path, "gunSprite.png");
            this.customBodyMaterial = ResourcesController.GetMaterial(Info.path, "sprite.png");
            this.customGunMaterial = ResourcesController.GetMaterial(Info.path, "gunSprite.png");
            this.spriteTextureField = FindSpriteTextureField();
            EnsureHelicopterHarmonyPatch();
            TestVanDammeAnim predabro = HeroController.GetHeroPrefab(HeroType.Predabro);
            if (predabro != null && predabro.soundHolder != null)
                this.tridentSounds = predabro.soundHolder.attackSounds;
        }

        protected override void Start()
        {
            base.Start();
            this.meleeType = MeleeType.Custom;
            this.currentMeleeType = MeleeType.Custom;
            this.runningFrameRate = 0.08f;
            this.useNewDuckingFrames = true;
            this.useNewKnifeClimbingFrames = true;
            // 爬梯保持原版站姿，专用动画素材保留备用。
            this.useNewLadderClimbingFrames = false;
            this.useLadderClimbingTransition = false;
            this.sprite.RecalcTexture();
            this.sprite.SetPixelDimensions(32, 32);
            this.sprite.SetSize(32f, 32f);
            this.gunSprite.RecalcTexture();
            this.gunSprite.SetPixelDimensions(32, 32);
            this.gunSprite.SetSize(32f, 32f);
            RenderTrident();
        }

        // 主攻击由松手事件驱动，禁用 Rambro 的自动连射入口。
        protected override void UseFire() { }

        private bool CanUseTrident()
        {
            return this.health > 0 && this.stunTime <= 0f && this.frozenTime <= 0f &&
                !this.isOnHelicopter && this.impaledByTransform == null &&
                this.strungUpBy == null && this.inseminatorUnit == null &&
                !this.usingSpecial && !this.usingPockettedSpecial &&
                !this.throwingHeldObject && !this.hasBeenCoverInAcid;
        }

        protected override void RunFiring()
        {
            if (this.tridentAttack == null) return;
            this.fireDelay = Mathf.Max(0f, this.fireDelay - this.t);
            bool canAttack = CanUseTrident() && this.fireDelay <= 0f;
            if (this.fire && canAttack) StopRolling();
            this.tridentAttack.Step(this.t, this.fire, canAttack);
            if (this.tridentAttack.ChargedNow) WaterCue(10f, this.ducking ? 9f : 21f);
            if (this.tridentAttack.ThrowNow) ThrowTrident();
            if (this.tridentAttack.MeleeNow) StrikeWithTrident();
            if (this.tridentAttack.RestoredNow) WaterCue(8f, 10f);
            if (this.doingMelee && !this.tridentAttack.IsMelee) CancelMelee();
            if (canAttack && (this.tridentAttack.IsCharging || this.tridentAttack.IsMelee))
                ActivateGun();
            RenderTrident();
        }

        private int WeaponDirection
        {
            get
            {
                if (this.Syncronize && !this.IsMine && this.syncedDirection != 0)
                    return this.syncedDirection < 0 ? -1 : 1;
                return this.transform.localScale.x < 0f ? -1 : 1;
            }
        }

        private void ThrowTrident()
        {
            int direction = WeaponDirection;
            bool charged = this.tridentAttack.IsChargedThrow;
            CustomProjectile prefab = charged ? this.chargedTridentPrefab : this.tridentPrefab;
            prefab.SpawnProjectileLocally(this, this.X + direction * 14f,
                this.Y + (this.ducking ? 8f : 11f), direction * (charged ? 660f : 400f),
                0f, this.playerNum, 0f);
            PlayTridentSound(charged ? 0.85f : 1.05f);
            FireFlashAvatar();
            TriggerBroFireEvent();
            SetGestureAnimation(GestureElement.Gestures.None);
            Map.DisturbWildLife(this.X, this.Y, 80f, this.playerNum);
        }

        protected override void RunGun() { RenderTrident(); }
        protected override void SetGunSprite(int spriteFrame, int spriteRow) { RenderTrident(); }

        protected override void SetGunPosition(float xOffset, float yOffset)
        {
            this.weaponOffsetX = xOffset;
            this.weaponOffsetY = yOffset;
            ApplyTridentPosition();
        }

        protected override void SetSpriteOffset(float xOffset, float yOffset)
        {
            this.bodyVisualOffsetX = xOffset;
            this.bodyVisualOffsetY = yOffset;
            base.SetSpriteOffset(xOffset, yOffset);
        }

        protected override void AnimateZipline()
        {
            base.AnimateZipline();
            SetBodyFrame(TridentMovementAnimation.ZiplineBodyFrame(CurrentBodyFrame()));
            RenderTrident();
        }

        protected override void AnimateGesture()
        {
            bool wasFlexing = this.currentGesture == GestureElement.Gestures.Flex;
            base.AnimateGesture();
            if (wasFlexing)
            {
                // The base method selects Rambro's gesture row. Keep its timing and
                // event logic, then redirect the rendered body to Aquabro's flex cells.
                SetBodyFrame(352 + Mathf.Clamp(this.frame, 0, 23));
            }
        }

        private static FieldInfo FindSpriteTextureField()
        {
            // texture 在不同 Broforce 版本中声明于 SpriteSM 或其 SpriteBase 基类。
            Type type = typeof(SpriteSM);
            while (type != null)
            {
                FieldInfo field = type.GetField("texture",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) return field;
                type = type.BaseType;
            }
            return null;
        }

        protected override void AnimateClimbingLadder()
        {
            base.AnimateClimbingLadder();
            if (this.useNewLadderClimbingFrames && IsNearbyLadder(this.transform.localScale.x * 6f, 22f))
            {
                int bodyFrame = CurrentBodyFrame();
                int restingFrame = TridentMovementAnimation.LadderRestBodyFrame(bodyFrame, this.frame);
                if (restingFrame != bodyFrame)
                {
                    this.hangingOneArmed = true;
                    SetBodyFrame(restingFrame);
                }
            }
            RenderTrident();
        }

        private void ApplyTridentPosition()
        {
            if (this.gunSprite == null) return;
            float extension = this.tridentAttack != null &&
                this.tridentAttack.Pose == TridentPose.Thrust ? 9f : 0f;
            // 配对帧跟随身体偏移；突刺肩点已在图集中回移，保留原来的 +9 前伸。
            base.SetGunPosition(this.weaponUsesBodyCoordinates ? this.bodyVisualOffsetX + extension : this.weaponOffsetX + extension,
                this.weaponUsesBodyCoordinates ? this.bodyVisualOffsetY : this.weaponOffsetY);
        }

        private int CurrentBodyFrame()
        {
            if (this.sprite == null) return -1;
            Vector2 pixel = this.sprite.lowerLeftPixel;
            return (Mathf.RoundToInt(pixel.y / 32f) - 1) * 32 + Mathf.RoundToInt(pixel.x / 32f);
        }

        private void SetBodyFrame(int frame)
        {
            this.sprite.SetLowerLeftPixel((frame % 32) * 32f, (frame / 32 + 1) * 32f);
        }

        private void RenderTrident()
        {
            if (this.gunSprite == null) return;
            int bodyFrame = CurrentBodyFrame();
            // 这四类动作的完整双臂都在身体贴图中，持火也不再叠加武器层。
            if (TridentMovementAnimation.IsUnarmedTraversalBodyFrame(bodyFrame))
            {
                this.weaponUsesBodyCoordinates = false;
                this.weaponHiddenForTraversal = true;
                DeactivateGun();
                return;
            }
            int pose = this.tridentAttack == null ? 0 : (int)this.tridentAttack.Pose;
            bool oneArm = !this.doingMelee && (this.actionState == ActionState.Hanging ||
                (this.actionState == ActionState.ClimbingLadder && this.hangingOneArmed) ||
                this.attachedToZipline != null || this.WallDrag);
            bool traversalBody = TridentMovementAnimation.IsTraversalBodyFrame(bodyFrame);
            bool canRenderWeapon = this.currentGesture == GestureElement.Gestures.None && CanUseTrident() &&
                !this.usingSpecial2 && !this.usingSpecial3 && !this.usingSpecial4;
            // 原版可能在切换身体格之前调用 SetGunSprite，离开空手动作后及时恢复武器。
            if (this.weaponHiddenForTraversal && canRenderWeapon &&
                TridentMovementAnimation.WeaponCellForBody(bodyFrame) >= 0)
            {
                ActivateGun();
                this.weaponHiddenForTraversal = false;
            }
            int movementCell = (traversalBody || (!oneArm && !this.doingMelee)) &&
                canRenderWeapon
                ? TridentMovementAnimation.WeaponCellForBody(bodyFrame, pose) : -1;
            this.weaponUsesBodyCoordinates = movementCell >= 0;
            if (this.weaponUsesBodyCoordinates)
            {
                this.gunSprite.transform.localScale = Vector3.one;
                this.gunSprite.SetLowerLeftPixel((movementCell % 32) * 32f,
                    (movementCell / 32 + 1) * 32f);
                // 滑索保留持戟，换手时继续显示其配对的手臂与武器。
                if (traversalBody) ActivateGun();
                ApplyTridentPosition();
                return;
            }
            int column = 0;
            int row = 0;
            if (oneArm) { column = 16; row = 1; }
            else if (this.ducking) { row = 1; }
            else if (this.actionState == ActionState.Running && !this.doingMelee) { column = 16; }
            this.gunSprite.SetLowerLeftPixel((column + pose) * 32f, (row + 1) * 32f);
            ApplyTridentPosition();
        }

        private bool CanPlayLandingAnimation()
        {
            return (this.actionState == ActionState.Idle || this.actionState == ActionState.Running) &&
                CanUseTrident() && !this.ducking && !this.doingMelee &&
                this.rollingFrames <= 0 &&
                !this.usingSpecial2 && !this.usingSpecial3 && !this.usingSpecial4 &&
                this.currentGesture == GestureElement.Gestures.None &&
                this.tridentAttack != null && this.tridentAttack.Pose == TridentPose.Held;
        }

        protected override void Land()
        {
            bool landedFromJump = this.actionState == ActionState.Jumping && this.yI < -20f;
            base.Land();
            if (!landedFromJump) return;
            this.movementAnimation.CancelLanding();
            int bodyFrame = CurrentBodyFrame();
            // 原版原地落地只改为 Idle，身体格可能仍停留在上一帧的空中姿势。
            bool ordinaryBody = TridentMovementAnimation.IsGroundBodyFrame(bodyFrame) ||
                (this.actionState == ActionState.Idle && bodyFrame >= 64 && bodyFrame <= 75);
            if (CanPlayLandingAnimation() && ordinaryBody)
            {
                this.movementAnimation.StartLanding(this.actionState == ActionState.Running);
                this.landingStartedThisUpdate = true;
                SetBodyFrame(this.movementAnimation.LandingBodyFrame);
                RenderTrident();
            }
        }

        private void UpdateLandingAnimation()
        {
            if (!this.movementAnimation.IsLanding) return;
            int bodyFrame = CurrentBodyFrame();
            bool canPlay = CanPlayLandingAnimation() &&
                (TridentMovementAnimation.IsGroundBodyFrame(bodyFrame) ||
                 TridentMovementAnimation.IsLandingBodyFrame(bodyFrame));
            float elapsed = this.landingStartedThisUpdate ? 0f : this.t;
            this.landingStartedThisUpdate = false;
            bool completed = this.movementAnimation.AdvanceLanding(elapsed, canPlay,
                this.actionState == ActionState.Running);
            if (this.movementAnimation.IsLanding)
            {
                SetSpriteOffset(0f, 0f);
                SetBodyFrame(this.movementAnimation.LandingBodyFrame);
            }
            else if (completed || TridentMovementAnimation.IsLandingBodyFrame(bodyFrame))
            {
                if (completed)
                {
                    // 收尾姿势对应跑步首帧；仅重置动画计时，移动和起跳仍由原版处理。
                    this.frame = 0;
                    this.counter = 0f;
                }
                ChangeFrame();
            }
        }

        protected override bool CanStartNewMelee()
        {
            return this.tridentAttack != null && this.tridentAttack.CanMelee &&
                !this.doingMelee && CanUseTrident();
        }

        protected override void StartMelee() { StartCustomMelee(); }

        protected override void AirJump()
        {
            // 海王不提供额外空中起跳；近战续跳由 jumpTime 的清理处理。
            return;
        }

        protected override void StartCustomMelee()
        {
            if (!CanStartNewMelee() || !this.tridentAttack.StartMelee(this.fire)) return;
            this.currentMeleeType = MeleeType.Custom;
            this.frame = 0;
            this.counter = 0f;
            StartMeleeCommon();
            this.jumpingMelee = false;
            // 与原版 StartKnifeMelee 一致，结束本次按住跳跃的续升窗口。
            // 只清理计时，保留起跳或下落已经产生的纵向速度。
            this.jumpTime = 0f;
            ActivateGun();
        }

        protected override void AnimateCustomMelee()
        {
            SetSpriteOffset(0f, 0f);
            SetGunPosition(this.ducking ? 2f : 0f, this.ducking ? -1f : 0f);
            this.sprite.SetLowerLeftPixel(this.ducking ? 192f : 0f, 32f);
            this.frameRate = 0.05f;
            ActivateGun();
            RenderTrident();
        }

        protected override void RunCustomMeleeMovement()
        {
            // 近战期间也可能从地面起跳，Jump(false) 会重新写入 jumpTime。
            // 此分支绕过原版跳跃计时递减，不能把续升窗口留到收招后。
            this.jumpTime = 0f;
            if (this.Y > this.groundHeight + 1f)
            {
                ApplyFallingGravity();
                if (this.yI < this.maxFallSpeed) this.yI = this.maxFallSpeed;
            }
            else
            {
                this.xI = (this.left ? -1f : (this.right ? 1f : 0f)) * this.speed * 0.5f;
            }
        }

        protected override void CancelMelee()
        {
            // 打断也可能早于下一次近战移动；退出前清掉尚未消耗的续升。
            if (this.doingMelee) this.jumpTime = 0f;
            if (this.tridentAttack != null && this.tridentAttack.IsMelee)
                this.tridentAttack.Cancel(this.fire);
            base.CancelMelee();
            RenderTrident();
        }

        private void StrikeWithTrident()
        {
            int direction = WeaponDirection;
            float y = this.Y + (this.ducking ? 8f : 11f);
            int mask = TridentCollision.SolidMask(this.playerNum);
            RaycastHit wall;
            bool blocked = TridentCollision.Trace(this.X, y, direction, 32f, mask, out wall);
            List<Unit> hitUnits = new List<Unit>();
            Mook nearbyMook = this.nearbyMook;
            if (nearbyMook != null && nearbyMook.health > 0 && nearbyMook.CanBeThrown() &&
                TridentStrikeGeometry.IsInEnvelope(nearbyMook.X - this.X, direction, 32f, 8f))
            {
                ThrowBackMook(nearbyMook);
                hitUnits.Add(nearbyMook);
            }
            int meleeDamage = ValueOrchestrator.GetModifiedDamage(8, this.playerNum);
            bool hitUnit = Map.HitUnits(this, this.playerNum, meleeDamage, 0,
                DamageType.Melee, 14f, 12f, this.X + direction * 8f, y,
                direction * 220f, 500f, true, true, false, hitUnits, false, false);
            if (hitUnit)
            {
                EffectsController.CreateMeleeStrikeEffect(this.X + direction * 8f, y,
                    direction, 1f);
                if (this.soundHolder != null && this.soundHolder.meleeHitSound != null)
                    Sound.GetInstance().PlaySoundEffectAt(this.soundHolder.meleeHitSound,
                        0.5f, this.transform.position, 1f, true, false, false, 0f);
            }
            if (blocked)
            {
                MapController.Damage_Networked(this, wall.collider.gameObject,
                    ValueOrchestrator.GetModifiedDamage(4, this.playerNum), DamageType.Melee,
                    direction * 220f, 40f, wall.point.x, wall.point.y);
                EffectsController.CreateBulletPoofEffect(wall.point.x, wall.point.y);
            }
            this.performedMeleeAttack = true;
            TriggerBroMeleeEvent();
        }

        private void PlayTridentSound(float pitch)
        {
            if (this.tridentSounds != null && this.tridentSounds.Length > 0)
                Sound.GetInstance().PlaySoundEffectAt(this.tridentSounds, 0.3f,
                    this.transform.position, pitch, true, false, false, 0f);
        }

        private void WaterCue(float xOffset, float yOffset)
        {
            EffectsController.CreateMuzzleFlashRoundEffectBlue(
                this.X + WeaponDirection * xOffset, this.Y + yOffset,
                0f, WeaponDirection * 10f, 15f, null);
        }

        private void CancelTrident()
        {
            if (this.tridentAttack != null) this.tridentAttack.Cancel(this.fire);
            if (this.doingMelee) CancelMelee();
            RenderTrident();
        }

        public override void ClearAllInput()
        {
            if (this.tridentAttack != null) this.tridentAttack.Cancel(this.fire);
            base.ClearAllInput();
        }

        public override void Stun(float time)
        {
            CancelTrident();
            base.Stun(time);
        }

        public override void Freeze(float time)
        {
            CancelTrident();
            base.Freeze(time);
        }

        public override void Damage(int damage, DamageType damageType, float xI, float yI,
            int direction, MonoBehaviour damageSender, float hitX, float hitY)
        {
            int oldHealth = this.health;
            base.Damage(damage, damageType, xI, yI, direction, damageSender, hitX, hitY);
            if (this.health < oldHealth) CancelTrident();
        }

        protected override void Update()
        {
            base.Update();
            // 本机 BroMaker 版本未提供 OnDeath/OnRevived，死亡期间清除攻击状态。
            // 不改写游戏的死亡姿态；复活后必须重新按下攻击键。
            if (this.health <= 0 && this.tridentAttack != null)
                this.tridentAttack.Cancel(this.fire);
            UpdateLandingAnimation();
            // 原版可能先改武器再改身体；在本帧动画结束后按实际身体格重新同步。
            RenderTrident();
        }

        internal void RestoreCustomTexturesAfterHelicopterReset()
        {
            if (this.spriteTextureField == null) return;
            RestoreCustomSpriteTexture(this.sprite, this.customBodyTexture, this.customBodyMaterial);
            RestoreCustomSpriteTexture(this.gunSprite, this.customGunTexture, this.customGunMaterial);
        }

        private static void EnsureHelicopterHarmonyPatch()
        {
            if (helicopterHarmonyPatched) return;
            new HarmonyLib.Harmony("Aquabro.helicopter-texture").PatchAll(
                typeof(Aquabro).Assembly);
            helicopterHarmonyPatched = true;
        }

        private void RestoreCustomSpriteTexture(SpriteSM target, Texture2D customTexture, Material customMaterial)
        {
            if (target == null || customTexture == null) return;
            Renderer renderer = target.GetComponent<Renderer>();
            bool materialChanged = renderer != null && renderer.sharedMaterial != customMaterial;
            Texture2D spriteTexture = this.spriteTextureField.GetValue(target) as Texture2D;
            if (!materialChanged && spriteTexture == customTexture) return;
            if (renderer != null && customMaterial != null)
                renderer.sharedMaterial = customMaterial;
            this.spriteTextureField.SetValue(target, customTexture);
            target.RecalcTexture();
            // RecalcTexture keeps the previous atlas' normalized cell height.
            // Reapply the 32x32 grid after replacing Rambro's 512/64-high atlases.
            target.SetPixelDimensions(32, 32);
            target.SetSize(32f, 32f);
            target.CalcUVs();
            target.UpdateUVs();
        }

        // 特技：奔涌水墙，沿用原来的 4 次弹药。
        protected override void UseSpecial()
        {
            CancelTrident();
            if (this.SpecialAmmo > 0)
            {
                this.SpecialAmmo--;
                RisingWave.CallMethod(this, this.risingWaveTexture);
                this.pressSpecialFacingDirection = 0;
                return;
            }
            HeroController.FlashSpecialAmmo(this.playerNum);
            ActivateGun();
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(Helicopter), "SetBrosPositions")]
    internal static class HelicopterTexturePositionPatch
    {
        private static void Postfix(Helicopter __instance)
        {
            Aquabro[] bros = __instance == null ? null :
                __instance.GetComponentsInChildren<Aquabro>(true);
            if (bros == null) return;
            for (int i = 0; i < bros.Length; i++)
                bros[i].RestoreCustomTexturesAfterHelicopterReset();
        }
    }
}

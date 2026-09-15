using System.Collections.Generic;
using BroMakerLib;
using BroMakerLib.CustomObjects.Projectiles;
using Rogueforce;
using UnityEngine;

namespace Aquabro
{
    public class TridentProjectile : CustomProjectile
    {
        private List<Unit> hitUnits;
        private int hitCount;
        private float remainingRange;
        private int solidMask;
        private bool terminated;
        protected virtual bool Charged { get { return false; } }

        protected override void Awake()
        {
            SpriteFolder = "projectiles";
            SpriteFileName = "Trident.png";
            SpritePixelDimensions = new Vector2(32f, 32f);
            SpriteWidth = 32f;
            SpriteHeight = 32f;
            // 游戏对象的位置是戟尖，戟身向后延伸。
            SpriteOffset = new Vector3(-13f, 0f, 0f);
            base.Awake();
            Sprite = GetComponent<SpriteSM>();
            Sprite.RecalcTexture();
            Sprite.SetTextureDefaults();
            Sprite.pixelDimensions = new Vector2(32f, 32f);
            Sprite.SetSize(32f, 32f);
            Sprite.SetOffset(SpriteOffset);
            Sprite.SetLowerLeftPixel(Charged ? 32f : 0f, 32f);
            damageType = DamageType.Normal;
            projectileSize = Charged ? 8f : 6f;
            horizontalProjectile = true;
            canReflect = false;
        }

        public override void Fire(float newX, float newY, float newXI, float newYI,
            float newZOffset, int newPlayerNum, MonoBehaviour owner)
        {
            // BroMaker 的自定义预制体是 inactive，先激活以确保 Awake 完成。
            gameObject.SetActive(true);
            hitUnits = new List<Unit>();
            hitCount = 0;
            terminated = false;
            remainingRange = Charged ? 240f : 180f;
            damage = Charged ? 10 : 6;
            life = 1f;
            solidMask = TridentCollision.SolidMask(newPlayerNum);
            base.Fire(newX, newY, newXI, 0f, newZOffset, newPlayerNum, owner);
        }

        protected override void SetRotation()
        {
            transform.localScale = new Vector3(xI < 0f ? -1f : 1f, 1f, 1f);
            transform.eulerAngles = Vector3.zero;
        }

        protected override void CheckSpawnPoint()
        {
            RegisterProjectile();
            TestVanDammeAnim owner = firedBy as TestVanDammeAnim;
            if (owner != null)
            {
                float spawnX = X;
                // 从角色身前扫到离手点，贴墙时不能把戟直接生成到墙后。
                SetXY(owner.X, Y);
                Advance(Mathf.Abs(spawnX - owner.X), false);
            }
            else
            {
                HitUnits();
            }
            SetPosition();
        }

        protected override void TryHitUnitsAtSpawn()
        {
            // 避免普通子弹出生判定的双倍伤害和首次命中销毁。
            HitUnits();
        }

        protected override void HitProjectiles()
        {
            Map.HitProjectiles(playerNum, damageInternal, damageType, projectileSize,
                X, Y, xI, yI, 0.1f);
        }

        protected override void RunProjectile(float deltaTime)
        {
            if (terminated || !gameObject.activeSelf) return;
            Advance(Mathf.Min(Mathf.Abs(xI) * deltaTime, remainingRange), true);
            if (!terminated && remainingRange <= 0.001f) Death();
            SetPosition();
        }

        private void Advance(float distance, bool useRange)
        {
            if (terminated || distance <= 0f) return;
            int direction = xI < 0f ? -1 : 1;
            RaycastHit wall;
            bool blocked = TridentCollision.Trace(X, Y, direction, distance, solidMask, out wall);
            float travel = blocked ? Mathf.Max(0f, wall.distance - 0.05f) : distance;
            float moved = 0f;
            // 高速蓄力戟也每隔至多 3 单位查一次敌人，防止跨过小体型目标。
            while (!terminated && moved < travel)
            {
                float step = Mathf.Min(3f, travel - moved);
                SetXY(X + direction * step, Y);
                moved += step;
                if (useRange) remainingRange = Mathf.Max(0f, remainingRange - step);
                // Projectile.HitProjectiles uses the game's registered projectile
                // list and invokes the target's normal Damage/Death path.
                HitProjectiles();
                HitUnits();
            }
            if (!terminated && blocked)
            {
                SetXY(wall.point.x, Y);
                ProjectileApplyDamageToBlock(wall.collider.gameObject, damageInternal,
                    damageType, direction * 180f, 30f);
                EffectsController.CreateBulletPoofEffect(wall.point.x, wall.point.y);
                Death();
            }
        }

        protected override void HitUnits()
        {
            if (terminated || hitUnits == null) return;
            Unit target;
            while (!terminated && (target = Map.GetFirstUnit(firedBy, playerNum,
                projectileSize, X, Y, true, false, hitUnits)) != null)
            {
                // 先登记后伤害；死亡回调和同一帧后续采样都不能重复命中。
                hitUnits.Add(target);
                if (!TridentCollision.CanReachUnit(X, Y, target, solidMask)) continue;
                bool stopsTrident = target.IsHeavy() || BroMakerUtilities.IsBoss(target);
                int direction = xI < 0f ? -1 : 1;
                Map.KnockAndDamageUnit(firedBy, target,
                    ValueOrchestrator.GetModifiedDamage(damageInternal, playerNum),
                    damageType, direction * (Charged ? 240f : 160f), 50f, direction,
                    true, X, Y, false);
                hitCount++;
                if (stopsTrident || hitCount >= (Charged ? 4 : 2)) Death();
            }
        }

        protected override void RunLife()
        {
            if (terminated) return;
            life -= t;
            if (life <= 0f) Death();
        }

        protected override void Bounce(RaycastHit hit)
        {
            if (terminated) return;
            ProjectileApplyDamageToBlock(hit.collider.gameObject, damageInternal,
                damageType, xI * 0.3f, 30f);
            EffectsController.CreateBulletPoofEffect(hit.point.x, hit.point.y);
            Death();
        }

        public override void Death()
        {
            if (terminated) return;
            terminated = true;
            enabled = false;
            GetComponent<Renderer>().enabled = false;
            DeregisterProjectile();
            EffectsController.CreateMuzzleFlashRoundEffectBlue(X, Y, 0f,
                Mathf.Sign(xI) * 20f, 15f, null);
            Destroy(gameObject);
        }
    }

    public class ChargedTridentProjectile : TridentProjectile
    {
        protected override bool Charged { get { return true; } }
    }
}

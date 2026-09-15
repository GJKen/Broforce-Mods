using BroMakerLib;
using BroMakerLib.CustomObjects.Projectiles;
using UnityEngine;

namespace Aquabro
{
    /// <summary>
    /// 潮汐弹:Aquabro 的主武器弹射物。穿透最多 4 个敌人并施加击退。
    /// 贴图 projectiles\TidalBolt.png(16x16 x 4 帧,水平排列)。
    /// 撞墙/寿命耗尽 → 销毁(重写 Bounce)。敌人单位由 Map.HitLivingUnits 处理。
    /// </summary>
    public class TidalBolt : CustomProjectile
    {
        protected int penetrateCount;
        protected int maxPenetrations = 4;
        protected SpriteSM boltSprite;
        protected static Vector3 zeroVector = Vector3.zero;

        protected override void Awake()
        {
            this.SpriteFolder = "projectiles";
            this.SpriteFileName = "TidalBolt.png";
            this.SpritePixelDimensions = new Vector2(16f, 16f);
            this.SpriteLowerLeftPixel = new Vector2(0f, 0f);
            this.SpriteWidth = 16f;
            this.SpriteHeight = 16f;
            base.Awake();
            this.boltSprite = this.GetComponent<SpriteSM>();
            // base.Awake()(CustomProjectile)的两个坑:
            // 1. SpriteLowerLeftPixel 的"未设置"哨兵值恰好是 (0,0),我们显式设的 (0,0)
            //    会被当成未设置,覆盖成 (0, 贴图高度) = (0,16) → 采样到 64x16 贴图外空白,弹体不可见。
            // 2. 它只把材质挂到 MeshRenderer,从不调 SpriteSM.RecalcTexture();
            //    SpriteBase.texture 是私有字段,只由 RecalcTexture 从材质回填,所以一直是 null,
            //    pixelsPerUV 也就没按贴图实际尺寸(64x16)算过。
            // 参照 Drunken Broster 的手工 SpriteSM 用法:先 RecalcTexture + SetTextureDefaults
            // 刷新纹理信息,再显式重设单行 4 帧(16x16)的帧坐标。
            this.boltSprite.RecalcTexture();
            this.boltSprite.SetTextureDefaults();
            this.boltSprite.pixelDimensions = new Vector2(16f, 16f);
            this.boltSprite.width = 16f;
            this.boltSprite.height = 16f;
            this.boltSprite.SetLowerLeftPixel(0f, 0f);
            this.damage = 3;
            this.damageInternal = this.damage;
            this.fullDamage = this.damage;
            this.damageType = DamageType.Normal;
            this.projectileSize = 6f;
            // 射程对齐兰博:0.6s × 600 单位/s ≈ 360 世界单位(原 1.6s × 360 ≈ 576+,用户反馈飞太远、
            // 且弹体视觉终点与伤害终点不一致);0.6s 后弹体原地销毁并出水花,视觉/伤害终点一致。
            this.life = 0.6f;
        }

        protected override void Update()
        {
            // 弹道:轻微下坠
            this.yI -= 120f * this.t;
            base.Update();
            if (this.boltSprite != null)
            {
                // 依次播放 4 帧
                int frame = Mathf.Clamp((int)(this.life * 20f) % 4, 0, 3);
                this.boltSprite.SetLowerLeftPixel(frame * 16f, 0f);
            }
        }

        public override void Fire(float newX, float newY, float xI, float yI, float zOffset, int playerNum, MonoBehaviour firedBy)
        {
            base.Fire(newX, newY, xI, yI, zOffset, playerNum, firedBy);
            EffectsController.CreateMuzzleFlashRoundEffectBlue(newX + Mathf.Sign(xI) * 4f, newY, 0f, xI * 0.1f, yI * 0.1f, base.transform);
        }

        protected override void HitUnits()
        {
            float grenadeX = 0f;
            float grenadeY = 0f;
            Map.HitGrenades(this.playerNum, 12f, this.X, this.Y, this.xI, this.yI, ref grenadeX, ref grenadeY);

            bool hitLiving = Map.HitLivingUnits(
                this.firedBy, this.playerNum, this.damageInternal, this.damageType,
                this.projectileSize, this.projectileSize / 2f,
                this.X, this.Y, this.xI, this.yI,
                true,   // penetrates
                true,   // knock(击退)
                true,
                true);
            if (hitLiving)
            {
                this.penetrateCount++;
                if (this.penetrateCount >= this.maxPenetrations)
                {
                    this.DestroyProjectile();
                }
            }
        }

        // 撞墙:基类 Update 检测地形后回调这里。销毁并出水花。
        protected override void Bounce(RaycastHit raycastHit)
        {
            this.DestroyProjectile();
        }

        protected void DestroyProjectile()
        {
            EffectsController.CreateMuzzleFlashRoundEffectBlue(this.X, this.Y, 0f, this.xI * 0.2f, this.yI * 0.2f, base.transform);
            this.DeregisterProjectile();
            Destroy(base.gameObject);
        }
    }
}

using BroMakerLib.Loggers;
using System;
using UnityEngine;

namespace Aquabro
{
    /// <summary>
    /// 奔涌波:Aquabro 的特技。沿地面向前推进的水墙,把敌人推飞并造成伤害。
    /// 照抄 BronobiForceWave(FlameWallExplosion 子类)的模式。
    /// </summary>
    public class RisingWave : FlameWallExplosion
    {
        // 波花 Puff 静态缓存,只从 Brofessional 复制一次
        private static Puff _wavePuff;

        public static void CallMethod(TestVanDammeAnim owner, Texture2D waveTexture)
        {
            try
            {
                var wave = new GameObject("RisingWave", new Type[] { typeof(Transform) }).AddComponent<RisingWave>();
                wave.transform.position = owner.transform.position;
                // 顺序关键:先 Setup(贴图)设 lightExplosion/音效,再调基类 Setup 触发爆炸链
                wave.Setup(waveTexture);
                wave.Setup(owner.playerNum, owner, DirectionEnum.Any);
            }
            catch (Exception ex)
            {
                BMLogger.Log("[RisingWave] Create failed: " + ex, LogType.Exception, true);
            }
        }

        // 照抄 MindControlWave.GetForceWavePuff:从 Brofessional 的 matildaTargettingWavePrefab.lightExplosion
        // 复制 Puff 参数,再把材质贴图换成自定义波贴图
        public static Puff GetWavePuff(Texture2D texture)
        {
            if (_wavePuff != null)
            {
                return _wavePuff;
            }
            GameObject go = new GameObject("Aquabro_RisingWave_Puff", new Type[] { typeof(MeshFilter), typeof(MeshRenderer), typeof(SpriteSM) });
            Puff wavePuff = (HeroController.GetHeroPrefab(HeroType.TheBrofessional) as TheBrofessional).matildaTargettingWavePrefab.lightExplosion;
            SpriteSM sprite = go.GetComponent<SpriteSM>();
            sprite.Copy(wavePuff.GetComponent<SpriteSM>());
            _wavePuff = go.AddComponent<Puff>();
            _wavePuff.frameRate = wavePuff.frameRate;
            _wavePuff.pauseFrame = wavePuff.pauseFrame;
            _wavePuff.gameObject.layer = wavePuff.gameObject.layer;
            _wavePuff.spriteSize = wavePuff.spriteSize;
            _wavePuff.frames = wavePuff.frames;
            _wavePuff.rows = wavePuff.rows;
            _wavePuff.loopStartFrame = wavePuff.loopStartFrame;
            _wavePuff.loopEndFrame = wavePuff.loopEndFrame;
            _wavePuff.numLoops = wavePuff.numLoops;
            _wavePuff.requiresGroundBelow = wavePuff.requiresGroundBelow;
            _wavePuff.pauseTime = wavePuff.pauseTime;
            _wavePuff.useGravity = wavePuff.useGravity;
            _wavePuff.gravityM = wavePuff.gravityM;
            _wavePuff.correctRotation = wavePuff.correctRotation;
            _wavePuff.useLightingMultiplier = wavePuff.useLightingMultiplier;

            // 换贴图:复制原材质再换 mainTexture
            Material mat = new Material(wavePuff.GetComponent<MeshRenderer>().material);
            MeshRenderer renderer = _wavePuff.GetComponent<MeshRenderer>();
            mat.mainTexture = texture;
            if (texture != null)
            {
                renderer.material = mat;
            }
            return _wavePuff;
        }

        // 必须在 Setup(playerNum, owner, direction) 之前调用:
        // FlameWallExplosion 的爆炸链依赖 lightExplosion(Puff 贴图),不设就是空引用直接死
        public void Setup(Texture2D texture)
        {
            try
            {
                this.lightExplosion = GetWavePuff(texture);
                MatildaTargettingWave wave = (HeroController.GetHeroPrefab(HeroType.TheBrofessional) as TheBrofessional).matildaTargettingWavePrefab;
                this.flashBangSoundHolder = wave.flashBangSoundHolder;
            }
            catch (Exception ex)
            {
                BMLogger.Log("[RisingWave] Setup texture failed: " + ex, LogType.Exception, true);
            }
        }

        void Awake()
        {
            try
            {
                // 启用每个水墙节点的自定义回调，补充 Boss 部件命中。
                // damageUnits 独立保留普通单位伤害；回调不会对 Boss 执行处决。
                assasinateUnits = true;
                damageDoodads = false;
                damageGround = false;
                damageUnits = true;
                damageAmount = 3;
                damageType = DamageType.Normal;
                knockUnits = true;
                blindUnits = false;
                maxCollumns = 32;
                maxRows = 3;
                rotateExplosionSprite = true;
                totalExplosions = 60;
                explosionRate = 0.04f;
            }
            catch (Exception ex)
            {
                BMLogger.Log("[RisingWave] Awake: " + ex, LogType.Exception, true);
            }
        }

        protected override void TryAssassinateUnits(float x, float y, int xRange, int yRange, int playerNum)
        {
            RisingWaveDamage.HitRelays(firedBy, playerNum, damageAmount, damageType,
                x, y + 3f, Mathf.Sign(x - transform.position.x) * 25f);
            Mook closestMook = Map.GetNearbyMook((float)xRange, (float)yRange, x, y, (forceDirection == DirectionEnum.Left ? -1 : 1), false);
            if (closestMook)
            {
                TestVanDammeAnim owner = firedBy as TestVanDammeAnim;
                if (owner != null && Mathf.Abs(closestMook.X - owner.X) > 12f)
                    return;
                float XI = Mathf.Sign(firedBy.transform.localScale.x) * 310f + (firedBy as TestVanDammeAnim).xI * 0.2f;
                float YI = 220f + (firedBy as TestVanDammeAnim).yI * 0.3f;
                closestMook.xI = XI;
                closestMook.yI = YI;
                closestMook.SetBackFlyingFrame(XI, YI);
                closestMook.transform.parent = firedBy.transform.parent;
                closestMook.Reenable();
                closestMook.StartFallingScream();
                closestMook.EvaluateIsJumping();
                closestMook.ThrowMook(false, base.playerNum);
            }
            Map.DeflectProjectiles(this, base.playerNum, 16f, (firedBy as TestVanDammeAnim).X + Mathf.Sign(base.transform.localScale.x) * 6f, (firedBy as TestVanDammeAnim).Y + 6f, Mathf.Sign(base.transform.localScale.x) * 200f, true);
        }
    }
}

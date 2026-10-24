using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace AquabroMod
{
    // One state per water body, or per entire spring cast. A pool touching
    // several columns is still one acid source for that cast.
    internal sealed class WaterAcidState
    {
        internal int Stage { get; private set; }
        private readonly HashSet<object> sources = new HashSet<object>();
        private float colorLevel, colorTime;
        internal void Absorb(object source)
        { if (source != null && Stage < 2 && sources.Add(source)) Stage++; }
        internal float AdvanceColor(float castTime, float delta)
        {
            float step = Mathf.Min(delta, Mathf.Max(0f, castTime - colorTime));
            colorTime = Mathf.Max(colorTime, castTime);
            colorLevel = AquabroSkillVisual.AdvanceAcidColor(colorLevel, Stage, step);
            return colorLevel;
        }
    }

    // Check before projectile movement/hits, so fast shots cannot jump across
    // the water between hero updates. Damageable rockets and ordinary bullets
    // use the same interception path.
    internal sealed class WaterProjectileBarrier
    {
        private static readonly List<WaterProjectileBarrier> Active = new List<WaterProjectileBarrier>();
        private static readonly List<SlimeGroundCover> AcidSlime = new List<SlimeGroundCover>();
        private readonly Aquabro owner;
        private readonly int kind;
        private readonly float scale;
        private float facing;
        private readonly WaterAcidState acid;
        internal int AcidStage { get { return acid.Stage; } }
        internal void MoveFacing(float value) { facing = value; }
        internal void Absorb(object source) { if (kind != 1) acid.Absorb(source); }
        internal void AbsorbPools()
        {
            if (kind == 1 || !visible || !owner.IsMine || AcidStage >= 2) return;
            foreach (DoodadBloodPool pool in DoodadBloodPool.bloodDoodads)
                if (pool is DoodadAcidPool && pool.gameObject.activeInHierarchy && Intersects(pool.X, pool.Y, 0f, 0f, 4f)) Absorb(pool);
            for (int i = AcidSlime.Count - 1; i >= 0 && AcidStage < 2; i--)
            {
                SlimeGroundCover slime = AcidSlime[i];
                if (slime == null) { AcidSlime.RemoveAt(i); continue; }
                if (!slime.gameObject.activeInHierarchy || slime.slimeColor != BloodColor.Green || slime.damage <= 0) continue;
                Vector3 position = slime.transform.position;
                float nx = slime.direction == DirectionEnum.Left ? -1f : slime.direction == DirectionEnum.Right ? 1f : 0f;
                float ny = slime.direction == DirectionEnum.Down ? -1f : slime.direction == DirectionEnum.Up ? 1f : 0f;
                if (nx == 0f && ny == 0f) continue;
                // The object sits at the block centre; acid covers its exposed
                // 16px face. Sample that face, not the centre inside the wall.
                for (int offset = -4; offset <= 4; offset += 4)
                    if (Intersects(position.x + nx * 8f + ny * offset,
                        position.y + ny * 8f - nx * offset, 0f, 0f, 4f))
                    { Absorb(slime); break; }
            }
        }

        private static void RegisterSlime(SlimeGroundCover __instance)
        {
            if (__instance != null && !AcidSlime.Contains(__instance)) AcidSlime.Add(__instance);
        }
        private float x, y, previousX, previousY;
        private int frame;
        private bool visible;

        internal WaterProjectileBarrier(Aquabro owner, int kind, float x, float y, float facing, float scale)
            : this(owner, kind, x, y, facing, scale, null) {}

        internal WaterProjectileBarrier(Aquabro owner, int kind, float x, float y, float facing, float scale,
            WaterAcidState sharedAcid)
        {
            acid = sharedAcid ?? new WaterAcidState();
            this.owner=owner;this.kind=kind;this.x=x;this.y=y;this.facing=facing;this.scale=scale;
            previousX=x;previousY=y;
            Active.Add(this);
        }

        internal void SetFrame(int frame)
        {
            this.frame=frame;
            visible=frame>=0 && (kind!=3 || frame<AquabroSkillVisual.SpringDissolveStartFrame) &&
                (kind!=2 || frame<AquabroSkillVisual.MaelstromDissolveStartFrame) &&
                (kind!=0 || frame<AquabroSkillVisual.TidalDissolveStartFrame);
        }
        internal void MoveTo(float x, float y) { previousX=this.x;previousY=this.y;this.x=x;this.y=y; }
        internal void Dispose() { visible=false;Active.Remove(this); }

        internal static void Install(Harmony harmony)
        {
            harmony.Patch(typeof(SlimeGroundCover).GetMethod("Setup"), null,
                new HarmonyMethod(typeof(WaterProjectileBarrier).GetMethod("RegisterSlime", BindingFlags.Static | BindingFlags.NonPublic)), null, null);
            foreach (SlimeGroundCover slime in UnityEngine.Object.FindObjectsOfType<SlimeGroundCover>()) RegisterSlime(slime);
            HarmonyMethod prefix=new HarmonyMethod(typeof(WaterProjectileBarrier).GetMethod("BeforeRun",BindingFlags.Static|BindingFlags.NonPublic));
            Type[] signature = new Type[] { typeof(float) };
            foreach (Type type in typeof(Projectile).Assembly.GetTypes())
            {
                if (!typeof(Projectile).IsAssignableFrom(type)) continue;
                MethodInfo method=type.GetMethod("RunProjectile",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.DeclaredOnly,
                    null,signature,null);
                if (method!=null && !method.IsAbstract) harmony.Patch(method,prefix,null,null,null);
            }
        }

        private static bool BeforeRun(Projectile __instance, float __0)
        {
            return !TryBlock(__instance,__0);
        }

        internal static bool TryBlock(Projectile projectile, float delta)
        {
            if (delta<=0f || projectile==null || !projectile.gameObject.activeInHierarchy) return false;
            foreach (WaterProjectileBarrier water in Active)
            {
                if (water.kind == 1 || !water.visible || water.owner==null || !water.owner.IsMine || water.owner.health<=0 ||
                    !water.owner.gameObject.activeInHierarchy ||
                    !GameModeController.DoesPlayerNumDamage(projectile.playerNum,water.owner.playerNum)) continue;
                if (!water.Intersects(projectile.X,projectile.Y,projectile.xI*delta,projectile.yI*delta,
                    System.Math.Max(.5f,projectile.projectileSize))) continue;
                // Douse the shot without invoking rocket Death/MakeEffects,
                // which would explode at the player's feet.
                if (projectile.damageType == DamageType.Acid) water.Absorb(projectile);
                projectile.xI=projectile.yI=0f;
                projectile.life=System.Math.Max(1f,projectile.life);
                projectile.gameObject.SetActive(false);
                if (projectile.Syncronize) projectile.DestroyNetworked();
                else UnityEngine.Object.Destroy(projectile.gameObject);
                return true;
            }
            return false;
        }

        private bool Intersects(float px,float py,float dx,float dy,float radius)
        {
            float startX=px,startY=py,travelX=dx,travelY=dy;
            float waterDx=x-previousX,waterDy=y-previousY;
            // Sweep relative motion too: the moving wave can cross a slow shot.
            px+=waterDx;py+=waterDy;
            dx-=waterDx;dy-=waterDy;
            float margin=radius+5f,near=0f,far=1f;
            float height=112f*scale;
            float halfWidth=64f*scale;
            if (!Clip(px,dx,x-halfWidth-margin,x+halfWidth+margin,ref near,ref far) ||
                !Clip(py,dy,y-16f*scale-margin,y+height+margin,ref near,ref far)) return false;
            float span=far-near;
            float length=System.Math.Max(System.Math.Abs(dx),System.Math.Abs(dy))*span;
            int steps=System.Math.Max(1,(int)System.Math.Ceiling(length/scale));
            SkillContactMask mask = new SkillContactMask(kind, x, y, facing, scale, frame);
            for (int i=0;i<=steps;i++)
            {
                float time=near+span*i/steps;
                if (!mask.Contains(px + dx * time, py + dy * time, margin)) continue;
                float contactX=startX+travelX*time,contactY=startY+travelY*time;
                float waterX=previousX+waterDx*time;
                float waterY=previousY+waterDy*time;
                float sourceY=Mathf.Clamp(contactY,waterY+1f,waterY+height);
                if (!Aquabro.WaterContactVisible(waterX,sourceY,contactX,contactY,null) ||
                    !Aquabro.WaterContactVisible(startX,startY,contactX,contactY,null)) continue;
                return true;
            }
            return false;
        }

        private static bool Clip(float pos,float delta,float min,float max,ref float near,ref float far)
        {
            if (System.Math.Abs(delta)<.00001f) return pos>=min && pos<=max;
            float a=(min-pos)/delta,b=(max-pos)/delta;
            if (a>b) {float swap=a;a=b;b=swap;}
            near=System.Math.Max(near,a);far=System.Math.Min(far,b);
            return near<=far;
        }
    }
}

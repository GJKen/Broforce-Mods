using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace AquabroMod
{
    // Check before projectile movement/hits, so fast shots cannot jump across
    // the water between hero updates. Damageable rockets and ordinary bullets
    // use the same interception path.
    internal sealed class WaterProjectileBarrier
    {
        private static readonly List<WaterProjectileBarrier> Active = new List<WaterProjectileBarrier>();
        private readonly Aquabro owner;
        private readonly int kind;
        private readonly float facing, scale;
        private float x, y, previousX, previousY;
        private int frame;
        private bool visible;

        internal WaterProjectileBarrier(Aquabro owner, int kind, float x, float y, float facing, float scale)
        {
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
            HarmonyMethod prefix=new HarmonyMethod(typeof(WaterProjectileBarrier).GetMethod("BeforeRun",BindingFlags.Static|BindingFlags.NonPublic));
            foreach (Type type in typeof(Projectile).Assembly.GetTypes())
            {
                if (!typeof(Projectile).IsAssignableFrom(type)) continue;
                MethodInfo method=type.GetMethod("RunProjectile",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.DeclaredOnly,
                    null,new Type[] {typeof(float)},null);
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
                if (!water.visible || water.owner==null || !water.owner.IsMine || water.owner.health<=0 ||
                    !water.owner.gameObject.activeInHierarchy ||
                    !GameModeController.DoesPlayerNumDamage(projectile.playerNum,water.owner.playerNum)) continue;
                if (!water.Intersects(projectile.X,projectile.Y,projectile.xI*delta,projectile.yI*delta,
                    System.Math.Max(.5f,projectile.projectileSize))) continue;
                // Douse the shot without invoking rocket Death/MakeEffects,
                // which would explode at the player's feet.
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
            // Sweep relative motion too: the moving wave can cross a slow shot.
            px+=x-previousX;py+=y-previousY;
            dx-=x-previousX;dy-=y-previousY;
            float margin=radius+5f,near=0f,far=1f;
            if (!Clip(px,dx,x-64f*scale-margin,x+64f*scale+margin,ref near,ref far) ||
                !Clip(py,dy,y-16f*scale-margin,y+112f*scale+margin,ref near,ref far)) return false;
            float length=System.Math.Max(System.Math.Abs(dx),System.Math.Abs(dy))*(far-near);
            int steps=System.Math.Max(1,(int)System.Math.Ceiling(length/scale));
            for (int i=0;i<=steps;i++)
            {
                float time=near+(far-near)*i/steps;
                float sx=64f+(px+dx*time-x)*facing/scale;
                float sy=112f-(py+dy*time-y)/scale;
                bool hit=kind==2 ? MaelstromPixelMask.Contains(frame,sx,sy,margin/scale)
                    : kind==0 ? TidalWavePixelMask.Contains(frame,sx,sy,margin/scale)
                    : SpringPixelMask.Contains(frame,sx,sy,margin/scale);
                if (hit) return true;
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

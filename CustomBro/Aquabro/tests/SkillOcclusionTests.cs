using System;
using System.Collections.Generic;
using AquabroMod;
using UnityEngine;

internal static class SkillOcclusionTests
{
    static void Check(bool condition,string message) { if(!condition)throw new Exception(message); }
    static Collider Wall(float x)
    {
        Collider wall=new Collider(x,50,true);
        wall.bounds.min=new Vector3(x-.5f,-20,0);wall.bounds.max=new Vector3(x+.5f,150,0);
        wall.bounds.center=new Vector3(x,65,0);return wall;
    }
    static Vector2 WetPoint(int kind,float facing,float scale,int frame)
    {
        SkillContactMask mask=new SkillContactMask(kind,0,0,facing,scale,frame);
        for(int y=8;y<90;y++)for(int x=16;x<50;x++)
            if(mask.Contains(x*facing*scale,y*scale,0))return new Vector2(x*facing*scale,y*scale);
        throw new Exception("Missing occlusion point "+kind);
    }
    internal static void Run()
    {
        Aquabro owner=new Aquabro();
        try
        {
            Map.Targets.Clear();Physics.Contacts=new Collider[0];Physics.SharkTerrainEnabled=false;Physics.Blocked=false;
            foreach(int kind in new int[]{0,2,3})
            foreach(float facing in new float[]{-1,1})
            foreach(float scale in new float[]{.65f,1f})
            {
                int frame=kind==2?22:4;
                Vector2 p=WetPoint(kind,facing,scale,frame);
                WaterProjectileBarrier water=new WaterProjectileBarrier(owner,kind,0,0,facing,scale);water.SetFrame(frame);
                try
                {
                    Collider wall=Wall(p.x*.5f);Physics.SceneTerrain=new Collider[]{wall};
                    Projectile shot=new Projectile();shot.X=p.x;shot.Y=p.y;shot.damageType=DamageType.Acid;
                    Check(!WaterProjectileBarrier.TryBlock(shot,.016f)&&shot.gameObject.activeInHierarchy&&water.AcidStage==0,"Wall did not protect acid projectile "+kind);
                    DoodadAcidPool pool=new DoodadAcidPool();pool.X=p.x;pool.Y=p.y;DoodadBloodPool.bloodDoodads.Add(pool);
                    water.AbsorbPools();Check(water.AcidStage==0,"Water absorbed acid pool through wall "+kind);
                    wall.enabled=false;
                    water.AbsorbPools();Check(water.AcidStage==1,"Pool did not become reachable after wall removal");
                    Check(WaterProjectileBarrier.TryBlock(shot,.016f)&&water.AcidStage==2,"Projectile consumed budget while occluded");
                    DoodadBloodPool.bloodDoodads.Clear();
                }
                finally {water.Dispose();Physics.SceneTerrain=new Collider[0];}
                // The projectile will hit a wall before reaching otherwise exposed water.
                water=new WaterProjectileBarrier(owner,kind,0,0,facing,scale);water.SetFrame(frame);
                try
                {
                    Projectile shot=new Projectile();shot.X=140*facing;shot.Y=p.y;shot.xI=-140*facing;
                    Physics.SceneTerrain=new Collider[]{Wall(90*facing)};
                    Check(!WaterProjectileBarrier.TryBlock(shot,1f),"Water intercepted projectile beyond its first wall");
                    Physics.SceneTerrain=new Collider[0];Check(WaterProjectileBarrier.TryBlock(shot,1f),"Unobstructed fast projectile passed through water");
                }
                finally {water.Dispose();Physics.SceneTerrain=new Collider[0];}
            }
            foreach(int kind in new int[]{0,1})foreach(float facing in new float[]{-1,1})
            {
                int frame=kind==0?4:12;Vector2 p=WetPoint(kind,facing,1,frame);
                Collider target=new Collider(p.x,p.y,true);Physics.Contacts=new Collider[]{target};
                Doodad prop=new Doodad(p.x,p.y);Map.destroyableDoodads.Add(prop);
                Collider wall=Wall(p.x*.5f);Physics.SceneTerrain=new Collider[]{wall};
                HashSet<GameObject> hits=new HashSet<GameObject>();HashSet<object> units=new HashSet<object>();
                WaveDamageState wave=new WaveDamageState();MapController.ObjectHits=Map.DoodadHits=0;
                for(int pass=0;pass<2;pass++)
                {
                    if(kind==0)owner.SkillWaveContact(0,0,facing,1,frame,units,hits,wave,pass*.2f);
                    else owner.SkillSharkObjects(0,0,facing,1,frame,hits);
                    Check(MapController.ObjectHits==(pass==0?0:1)&&Map.DoodadHits==(pass==0?0:1),"Scenery wall or restored contact failed kind="+kind+" pass="+pass);
                    wall.enabled=false;
                }
                Physics.SceneTerrain=new Collider[0];Physics.Contacts=new Collider[0];Map.destroyableDoodads.Clear();
            }
            // Unlike unit visibility shortcuts, even a wall in a <8px gap blocks.
            Physics.SceneTerrain=new Collider[]{Wall(2)};
            Check(!Aquabro.WaterContactVisible(0,20,4,20,null),"Thin nearby wall ignored");
            Physics.SceneTerrain[0].isTrigger=true;
            Check(Aquabro.WaterContactVisible(0,20,4,20,null),"Trigger blocked water");
            Console.WriteLine("PASS water occlusion: three barriers; acid shots/pools; both facings/scales; projectile wall before water; shark/wave scenery; wall removal preserves hit chance; thin nearby wall; triggers");
        }
        finally
        {
            Physics.SceneTerrain=new Collider[0];Physics.Contacts=new Collider[0];
            DoodadBloodPool.bloodDoodads.Clear();Map.destroyableDoodads.Clear();
        }
    }
}

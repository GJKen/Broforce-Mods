using System;
using System.Collections.Generic;
using System.Drawing;
using AquabroMod;
using UnityEngine;

internal static class SkillMaelstromTests
{
    private static void Check(bool value,string message) {if (!value) throw new Exception(message);}
    private static void Reset()
    {
        Map.Targets.Clear();Map.Hits.Clear();Map.TotalHits=0;Physics.Blocked=false;
        Physics.BlockedBelowY=float.NegativeInfinity;Physics.SharkTerrainEnabled=false;
    }
    private static Mook Enemy(float x,float y)
    {
        Mook target=new Mook();target.X=x;target.Y=y;target.playerNum=-1;
        // canTumble is deliberately false: it must not exclude the enemy.
        Map.Targets.Add(target);return target;
    }

    internal static void Run(Bitmap atlas)
    {
        Aquabro owner=new Aquabro();
        for (int frame=0;frame<24;frame++)
        {
            int px=-1,py=0;
            for (int y=0;y<110;y++) for (int x=0;x<128;x++)
            {
                bool opaque=atlas.GetPixel(frame*128+x,y).A>0;
                Check(MaelstromPixelMask.Mask.Contains(frame,x+.5f,y+.5f,0)==opaque,"Maelstrom mask/PNG mismatch");
                if (opaque && px<0) {px=x;py=y;}
            }
            Check(px>=0,"Missing active water frame");
            foreach (float facing in new float[] {-1,1})
            foreach (float scale in new float[] {.65f,.85f,1f})
            {
                Reset();
                Mook target=Enemy((px+.5f-64)*scale*facing,(112-py-.5f)*scale-6);
                HashSet<object> caught=new HashSet<object>();
                owner.SkillMaelstromPull(0,0,facing,scale,frame,.016f,true,caught);
                Check(caught.Contains(target) && target.KnockCalls==1 && target.ForcedTumble && Map.Hits[target]==3,
                    "Visible maelstrom contact missed / non-tumbling enemy excluded, frame "+frame);
            }
        }
        // Exact paused-game snapshot: platform at Y=568, vortex anchored at 504.
        Reset();Mook rim=Enemy(882.439f,568f);HashSet<object> snapshotCaught=new HashSet<object>();
        Physics.BlockedBelowY=568f;
        Check(!owner.SkillLineOfSight(842.0003f,528f,rim),"Snapshot does not reproduce old blocked low ray");
        owner.SkillMaelstromPull(842.0003f,504f,-1,.85f,22,.016f,true,snapshotCaught);
        Check(snapshotCaught.Contains(rim) && rim.xI<0 && Map.Hits[rim]==3,"Upper-platform rim contact still ignored");
        HashSet<object> finished=new HashSet<object>();
        owner.SkillMaelstromFinish(842.0003f,504f,-1,.85f,24,snapshotCaught,finished);
        Check(Map.Hits[rim]==9,"Upper rim not included in final damage");
        owner.SkillMaelstromFinish(842.0003f,504f,-1,.85f,24,snapshotCaught,finished);
        Check(Map.TotalHits==2,"Finisher damages same target twice");

        Reset();Mook outside=Enemy(60,0);HashSet<object> caughtSet=new HashSet<object>();
        owner.SkillMaelstromPull(0,0,1,1,22,.016f,true,caughtSet);
        Check(outside.KnockCalls==0 && Map.TotalHits==0,"Old circular area still affects dry space");
        // Once caught, passing between rotating sheets does not drop the victim.
        caughtSet.Add(outside);outside.X=55;outside.Y=20;
        owner.SkillMaelstromPull(0,0,1,1,22,.016f,false,caughtSet);
        Check(outside.KnockCalls==1 && outside.xI<0,"Captured enemy falls out through rotating water gap");
        outside.X=200;owner.SkillMaelstromPull(0,0,1,1,22,.016f,false,caughtSet);
        Check(outside.KnockCalls==1,"Captured enemy pulled from outside effect");
        outside.X=55;Physics.Blocked=true;owner.SkillMaelstromPull(0,0,1,1,22,.016f,true,caughtSet);
        Check(outside.KnockCalls==1 && Map.TotalHits==0,"Captured target pulled/damaged through solid wall");
        Physics.Blocked=false;owner.IsMine=false;
        owner.SkillMaelstromPull(0,0,1,1,22,.016f,true,caughtSet);
        owner.SkillMaelstromFinish(0,0,1,1,24,caughtSet,new HashSet<object>());
        Check(outside.KnockCalls==1 && Map.TotalHits==0,"Remote owner applies pull/damage");owner.IsMine=true;
        owner.SkillMaelstromPull(0,0,1,1,22,0,true,caughtSet);
        Check(outside.KnockCalls==1,"Paused suction changes velocity");
        // Original 650px/s^2 vertical pull lost to the game's 1100px/s^2 gravity.
        for (int tick=0;tick<20;tick++)
        {
            outside.yI-=1100f/60f;
            owner.SkillMaelstromPull(0,0,1,1,22,1f/60f,false,caughtSet);
        }
        Check(outside.yI>0,"Suction cannot lift against original gravity");
        int calls=outside.KnockCalls;
        for (int frame=24;frame<32;frame++)
            owner.SkillMaelstromPull(0,0,1,1,frame,.016f,true,caughtSet);
        Check(outside.KnockCalls==calls && Map.TotalHits==0,"Dissolve still pulls/damages");

        Reset();Mook lower=Enemy(0,-30);Mook dead=Enemy(0,20);dead.health=0;
        Mook immune=Enemy(0,20);immune.invulnerable=true;Mook ally=Enemy(0,20);ally.playerNum=owner.playerNum;
        owner.SkillMaelstromPull(0,0,1,1,22,.016f,true,new HashSet<object>());
        Check(lower.KnockCalls+immune.KnockCalls+ally.KnockCalls==0 && dead.KnockCalls==1 && Map.TotalHits==1 && Map.Hits[dead]==3,
            "Corpse missed or below-floor/immune/allied enemy affected");

        Reset();Enemy(0,20);
        AquabroSkillVisual effect=new AquabroSkillVisual(owner,2,0,0,-1,AquabroSkillVisual.MaelstromLifetime,0,.85f);
        ((HashSet<object>)typeof(AquabroSkillVisual).GetField("maelstromCaught",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(effect)).Add(Map.Targets[0]);
        effect.Tick(3.39f);int pulses=Map.TotalHits;
        Check(pulses==1,"Active vortex did not pulse");effect.Tick(.22f);
        Check(Map.TotalHits==pulses+1 && Map.LastUpForce==500,"Finisher not triggered at release");
        effect.Tick(.1f);effect.Tick(0f);
        Check(Map.TotalHits==pulses+1,"Dissolve/pause repeats final damage");
        Check(!effect.Tick(.5f),"Vortex never cleans up");effect.Dispose();

        WaterProjectileBarrier barrier=new WaterProjectileBarrier(owner,2,0,0,1,.85f);
        for (int frame=24;frame<32;frame++)
        {
            barrier.SetFrame(frame);Projectile shot=new Rocket();shot.X=0;shot.Y=20;
            Check(!WaterProjectileBarrier.TryBlock(shot,.02f),"Dissolving vortex blocks rocket");
        }
        barrier.Dispose();Reset();
        CheckHeavyPull(owner);
        CheckSuctionSpread(owner);
        CheckHeavyControl(owner);
        CheckCorpsePull(owner);
        CheckDestination(owner);
        CheckWormDestination(owner);
        CheckDamageTimeline(owner);
        CheckWallTravel(owner);
        CheckWallExpansion(owner);
        CheckFinishVisualFrame(owner,atlas);
        Console.WriteLine("PASS maelstrom: 24 active masks; mirror/scales; live-scene upper-platform rim; physical knock without canTumble gate; gravity lift; captured gaps; wall/floor/ally/pause/ownership; pulse/one finisher; dissolve inactive");
    }

    private sealed class ArmouredHeavy : MookBigGuy {}

    private static void CheckCorpsePull(Aquabro owner)
    {
        foreach(float facing in new float[]{-1,1})
        foreach(int fps in new int[]{30,60,120})
        foreach(Mook corpse in new Mook[]{new Mook(),new MookBigGuy(),new ArmouredHeavy()})
        {
            Reset();corpse.X=25*facing;corpse.Y=0;corpse.playerNum=-1;corpse.health=-1;
            corpse.actionState=ActionState.Dead;Map.Targets.Add(corpse);
            HashSet<object> caught=new HashSet<object>();caught.Add(corpse);
            for(int tick=0;tick<fps/2;tick++)
            {
                corpse.yI-=1100f/fps;
                owner.SkillMaelstromPullAtTime(0,0,facing,.85f,22,1f/fps,false,caught,(float)tick/fps);
            }
            Check(corpse.KnockCalls==fps/2 && corpse.yI>0 && corpse.xI*facing<0,"Corpse not lifted/pulled inward");
            Check(corpse.health==-1 && corpse.actionState==ActionState.Dead && corpse.StunCalls==0,"Corpse revived/stunned by suction");
            int calls=corpse.KnockCalls;
            corpse.X=200;owner.SkillMaelstromPull(0,0,facing,.85f,22,.016f,false,caught);
            corpse.X=25*facing;Physics.Blocked=true;owner.SkillMaelstromPull(0,0,facing,.85f,22,.016f,false,caught);Physics.Blocked=false;
            owner.SkillMaelstromPull(0,0,facing,.85f,22,0,false,caught);
            owner.IsMine=false;owner.SkillMaelstromPull(0,0,facing,.85f,22,.016f,false,caught);owner.IsMine=true;
            corpse.hasBeenCoverInAcid=true;owner.SkillMaelstromPull(0,0,facing,.85f,22,.016f,false,caught);corpse.hasBeenCoverInAcid=false;
            corpse.gameObject.SetActive(false);owner.SkillMaelstromPull(0,0,facing,.85f,22,.016f,false,caught);corpse.gameObject.SetActive(true);
            Check(corpse.KnockCalls==calls,"Corpse pulled outside/wall/pause/remote/acid/inactive");
        }
        Reset();
        Console.WriteLine("PASS corpse suction: normal/heavy/subclass; both directions; 30/60/120 FPS; lift against gravity; dead state preserved; wall/range/acid/inactive/pause/ownership");
    }

    private static void CheckFinishVisualFrame(Aquabro owner,Bitmap atlas)
    {
        // The finisher must follow the frame actually rendered, including a tick
        // that skips the first dissolve frame. Fresh arrivals are not captured.
        foreach(int frame in new int[]{24,26})
        foreach(float facing in new float[]{-1,1})
        foreach(float scale in new float[]{.65f,.85f,1f})
        foreach(bool part in new bool[]{false,true})
        foreach(bool visible in new bool[]{false,true})
        {
            Reset();BossDamageTestStubs.Reset();Physics.Contacts=new Collider[0];
            AquabroSkillVisual effect=new AquabroSkillVisual(owner,2,0,0,facing,AquabroSkillVisual.MaelstromLifetime,0,scale);
            try
            {
                effect.Tick(3.59f);
                SkillContactMask oldMask=new SkillContactMask(2,48,0,facing,scale,23);
                SkillContactMask shownMask=new SkillContactMask(2,48,0,facing,scale,frame);
                Mook target=new Mook();target.playerNum=-1;target.width=10;target.height=target.headHeight=20;
                Collider collider=new Collider(0,0,true);BossPiece boss=new BossPiece();
                bool found=false;
                for(int y=2;y<112&&!found;y++)for(int x=-64;x<64&&!found;x++)
                {
                    target.X=48+x*facing*scale;target.Y=y*scale;
                    collider.bounds=new Bounds(target.X,target.Y);
                    bool before=part?oldMask.IntersectsRect(collider.bounds):oldMask.IntersectsBody(target);
                    bool after=part?shownMask.IntersectsRect(collider.bounds):shownMask.IntersectsBody(target);
                    if(before==visible || after!=visible)continue;
                    // Positive samples must overlap actual PNG pixels, not only
                    // the existing 5px contact allowance.
                    float left=part?collider.bounds.min.x:target.X-5;
                    float right=part?collider.bounds.max.x:target.X+5;
                    float bottom=part?collider.bounds.min.y:target.Y;
                    float top=part?collider.bounds.max.y:target.Y+20;
                    bool pngContact=false;
                    if(visible)
                        for(int py=0;py<112&&!pngContact;py++)for(int px=0;px<128&&!pngContact;px++)
                        {
                            if(atlas.GetPixel(frame*128+px,py).A==0)continue;
                            float a=48+(px-64)*facing*scale,b=a+facing*scale;
                            float upper=(112-py)*scale;
                            pngContact=Math.Max(a,b)>=left && Math.Min(a,b)<=right && upper>=bottom && upper-scale<=top;
                        }
                    found=!visible || pngContact;
                }
                Check(found,"Missing finish frame difference: "+frame+" part="+part+" visible="+visible);
                if(part){collider.gameObject.SetComponent(boss);Physics.Contacts=new Collider[]{collider};}
                else Map.Targets.Add(target);
                effect.Tick(3.6f+(frame-24)*.075f+.01f-3.59f);
                Check(SpriteSM.LastFrame==frame,"Finisher test did not render expected frame");
                int hits=part?BossDamageTestStubs.Requests:Map.TotalHits;
                Check(hits==(visible?1:0),"Finisher uses stale water shape: frame="+frame+" part="+part+" visible="+visible+" hits="+hits);
                if(visible)Check((part?BossDamageTestStubs.Damage[boss]:Map.Hits[target])==9,"Finisher damage changed");
                effect.Tick(.05f);effect.Tick(0);
                Check((part?BossDamageTestStubs.Requests:Map.TotalHits)==hits,"Finisher repeated in dissolve/pause");
            }
            finally{effect.Dispose();}
        }
        Reset();Physics.Contacts=new Collider[0];
        Console.WriteLine("PASS vortex finish matches rendered PNG: fresh body/Boss contact, stale-only exclusion, both facings, three scales, frame 24 and skipped-to-26, single 9 damage, pause");
    }

    private static void CheckWallTravel(Aquabro owner)
    {
        foreach(float wall in new float[]{-28f,-8f,8f,28f})
        foreach(float facing in new float[]{-1f,1f})
        foreach(int fps in new int[]{30,50,60,120})
        {
            Reset();Physics.SharkTerrainEnabled=true;Physics.SharkWallX=wall;
            float left=wall<0 ? wall+8f : -48f;
            float right=wall>0 ? wall-8f : 48f;
            AquabroSkillVisual effect=new AquabroSkillVisual(owner,2,0,0,facing,AquabroSkillVisual.MaelstromLifetime,0,.85f);
            float previous=0,min=0,max=0;int heldFrames=0;
            for(int tick=1;tick<=fps*4;tick++)
            {
                effect.Tick(1f/fps);
                float x=effect.CameraTarget.position.x;
                min=Math.Min(min,x);max=Math.Max(max,x);
                Check(x>=left-.01f && x<=right+.01f,"Vortex crossed wall during full cast");
                if(tick/(float)fps<3.59f)
                {
                    heldFrames=Math.Abs(x-previous)<.001f ? heldFrames+1 : 0;
                    Check(heldFrames<2,"Vortex waits at wall for virtual trajectory: wall="+wall+" facing="+facing+" fps="+fps+" time="+(tick/(float)fps));
                    Check(SpriteSM.LastFrame<24,"Vortex dissolves before completing travel");
                }
                else if(tick/(float)fps>3.61f)
                    Check(Math.Abs(x-right)<.01f && SpriteSM.LastFrame>=24,"Vortex did not dissolve at reachable right endpoint");
                previous=x;
            }
            float tolerance=160f/fps;
            Check(min<=left+tolerance && max>=right-tolerance,"Vortex failed to visit both reachable endpoints");
            effect.Dispose();
        }
        Physics.SharkTerrainEnabled=false;Physics.SharkWallX=float.PositiveInfinity;Reset();
        Console.WriteLine("PASS vortex wall reversal: walls on either side, including wall flush with origin; no held frames; both endpoints; dissolve at reachable right endpoint; both facings; 30/50/60/120 FPS");
    }

    private static void CheckWallExpansion(Aquabro owner)
    {
        foreach(float wall in new float[]{-28f,-8f,8f,28f})
        foreach(float facing in new float[]{-1f,1f})
        foreach(int fps in new int[]{15,30,60,120})
        foreach(float removeAt in new float[]{.3f,1.3f,2.6f,3.59f})
        {
            Reset();Physics.SharkTerrainEnabled=true;Physics.SharkWallX=wall;
            AquabroSkillVisual effect=new AquabroSkillVisual(owner,2,0,0,facing,AquabroSkillVisual.MaelstromLifetime,0,.85f);
            try
            {
                float time=0;
                while(time<removeAt-.00001f)
                {
                    float step=Math.Min(1f/fps,removeAt-time);effect.Tick(step);time+=step;
                }
                float before=effect.CameraTarget.position.x;
                Physics.SharkWallX=float.PositiveInfinity;
                effect.Tick(0);
                Check(effect.CameraTarget.position.x==before,"Paused wall removal moves vortex");
                // A tiny positive step must not remap elapsed time onto the new
                // full route and teleport, even just before the final boundary.
                float tiny=Math.Min(.00001f,(3.6f-time)*.001f);
                effect.Tick(tiny);time+=tiny;
                Check(Math.Abs(effect.CameraTarget.position.x-before)<.25f,"Wall expansion teleports vortex");
                float min=effect.CameraTarget.position.x,max=min;
                while(time<3.61f-.00001f)
                {
                    float step=Math.Min(1f/fps,3.61f-time);effect.Tick(step);time+=step;
                    float x=effect.CameraTarget.position.x;min=Math.Min(min,x);max=Math.Max(max,x);
                    Check(x>=-48.01f && x<=48.01f,"Expansion exceeds original +/-48px bounds");
                }
                Check(Math.Abs(effect.CameraTarget.position.x-48)<.01f && SpriteSM.LastFrame>=24,
                    "Expanded route did not finish at right endpoint on time");
                if(wall<0 && removeAt<=1.3f)Check(min<wall+8f-1f,"Removing left wall did not expand remaining left leg");
                if(wall>0)Check(max>wall-8f+1f,"Removing right wall did not expand right endpoint");
                float finished=effect.CameraTarget.position.x;effect.Tick(.4f);
                Check(effect.CameraTarget.position.x==finished,"Dissolve continues expanding/moving");
                Check(!effect.Tick(.21f),"Wall removal extends lifetime");
            }
            finally{effect.Dispose();}
        }
        // Dissolve must not discover newly opened space.
        Reset();Physics.SharkTerrainEnabled=true;Physics.SharkWallX=28;
        AquabroSkillVisual dissolving=new AquabroSkillVisual(owner,2,0,0,1,AquabroSkillVisual.MaelstromLifetime,0,.85f);
        dissolving.Tick(3.61f);Physics.SharkWallX=float.PositiveInfinity;dissolving.Tick(.1f);
        Check(Math.Abs(dissolving.CameraTarget.position.x-20)<.01f,"Wall removal during dissolve restarts travel");
        dissolving.Dispose();Physics.SharkTerrainEnabled=false;Physics.SharkWallX=float.PositiveInfinity;Reset();
        Console.WriteLine("PASS vortex wall removal: both sides/flush walls; both facings; 15/30/60/120 FPS; all three route legs/late removal; continuous movement; +/-48px cap; original active/lifetime; pause/dissolve");
    }

    private static void CheckDamageTimeline(Aquabro owner)
    {
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        foreach (int fps in new int[]{30,50,60,120})
        {
            Reset();Unit vehicle=new Unit();vehicle.playerNum=-1;vehicle.Y=20;Map.Targets.Add(vehicle);
            AquabroSkillVisual effect=new AquabroSkillVisual(owner,2,0,0,1,AquabroSkillVisual.MaelstromLifetime,0,.85f);
            var caught=(HashSet<object>)typeof(AquabroSkillVisual).GetField("maelstromCaught",flags).GetValue(effect);
            caught.Add(vehicle);
            float time=0,lastHit=-10,minX=0,maxX=0;int total=0,armouredTotal=0,pulses=0,finishers=0,previousHits=0;
            for(int tick=0;tick<fps*5;tick++)
            {
                time+=1f/fps;
                if(time<=.72f)vehicle.X=time*(48f/.72f);
                else if(time<=2.16f)vehicle.X=48f-(time-.72f)*(96f/1.44f);
                else vehicle.X=Math.Min(48f,-48f+(time-2.16f)*(96f/1.44f));
                effect.Tick(1f/fps);
                float x=(float)typeof(AquabroSkillVisual).GetField("x",flags).GetValue(effect);
                float y=(float)typeof(AquabroSkillVisual).GetField("y",flags).GetValue(effect);
                minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);
                Check(y==0,"Vortex root moves vertically instead of horizontally");
                int pausedHits=Map.TotalHits;effect.Tick(0);
                Check(Map.TotalHits==pausedHits && (float)typeof(AquabroSkillVisual).GetField("x",flags).GetValue(effect)==x,"Pause moves/damages vortex");
                if(Map.TotalHits==previousHits)continue;
                Check(Map.TotalHits==previousHits+1,"Multiple hits on same frame");previousHits=Map.TotalHits;
                int damage=Map.Hits[vehicle];total+=damage;
                int actual=time-lastHit<.096f?1:damage;armouredTotal+=actual;
                if(time-lastHit>.096f)lastHit=time;
                if(damage==3)pulses++;
                else {Check(damage==9 && actual==9,"Vehicle cooldown swallowed vortex finisher");finishers++;}
            }
            Check(pulses==9 && finishers==1 && total==36 && armouredTotal==36,"Full moving vortex damage mismatch at "+fps+" FPS: "+total);
            Check(minX< -47 && maxX>47,"Vortex does not travel back and forth");
            effect.Dispose();
        }
        foreach(float facing in new float[]{-1,1})
        {
            Reset();
            AquabroSkillVisual effect=new AquabroSkillVisual(owner,2,100,0,facing,AquabroSkillVisual.MaelstromLifetime,0,.85f);
            float[] times={.03f,.30f,.71f,.72f,.73f,1.44f,2.15f,2.16f,2.17f,3.58f,3.62f,4.1f};
            float[] positions={102f,120f,147.33333f,148f,147.33333f,100f,52.66667f,52f,52.66667f,146.66667f,148f,148f};
            float previous=0;
            for(int i=0;i<times.Length;i++)
            {
                effect.Tick(times[i]-previous);previous=times[i];
                Check(Math.Abs(effect.CameraTarget.position.x-positions[i])<.01f,"Vortex pauses or dissolves before reaching right endpoint at "+times[i]);
                Check((SpriteSM.LastFrame>=24)==(times[i]>=3.6f),"Vortex dissolve timing changed");
            }
            effect.Dispose();
        }
        Reset();Physics.SharkTerrainEnabled=true;Physics.SharkWallX=28;
        Check(owner.MoveSkillHorizontally(0,0,48)<=20,"Moving vortex crosses solid wall");
        Physics.SharkWallX=-28;
        Check(owner.MoveSkillHorizontally(0,0,-48)>=-20,"Mirrored moving vortex crosses solid wall");
        Reset();Console.WriteLine("PASS vortex movement/damage: immediate constant-speed travel; no holds at turns; right endpoint before dissolve in both facings and across finish tick; horizontal +/-48px; 9 x 3 + 9 = 36 at 30/50/60/120 FPS; vehicle cooldown; pause; walls; one finish");
    }

    private static void CheckHeavyControl(Aquabro owner)
    {
        foreach (MookBigGuy target in new MookBigGuy[] {new MookBigGuy(),new ArmouredHeavy()})
        {
            // Stay outside every circulating horizontal target so the expected
            // leftward acceleration does not depend on the object's phase.
            Reset();target.X=45;target.Y=0;target.playerNum=-1;Map.Targets.Add(target);
            target.fire=target.left=target.doingMelee=true;target.xI=-120;target.yI=80;
            HashSet<object> caught=new HashSet<object>();caught.Add(target);
            owner.SkillMaelstromPull(0,0,1,.85f,22,.016f,false,caught);
            Check(target.IsStunned && !target.fire && !target.left && !target.doingMelee,"Heavy remains able to act while captured");
            Check(target.xI< -120 && target.yI>80 && target.actionState==ActionState.Jumping,"Stun reset suction momentum/airborne state");
            target.stunTime=2;target.fire=target.right=true;int stunCalls=target.StunCalls;
            owner.SkillMaelstromPull(0,0,1,.85f,22,.016f,false,caught);
            Check(target.stunTime==2 && target.StunCalls==stunCalls && !target.fire && !target.right,"Existing longer stun shortened / input not cleared");
            target.stunTime=0;owner.SkillMaelstromPull(0,0,1,.85f,22,.016f,false,caught);
            Check(target.IsStunned && target.StunCalls==stunCalls+1,"Expired stun not renewed while captured");
            target.stunTime=0;target.fire=target.left=true;target.X=200;
            owner.SkillMaelstromPull(0,0,1,.85f,22,.016f,false,caught);
            Check(!target.IsStunned && target.fire && target.left,"Heavy controlled outside vortex bounds");
            target.X=25;Physics.Blocked=true;
            owner.SkillMaelstromPull(0,0,1,.85f,22,.016f,false,caught);Physics.Blocked=false;
            Check(!target.IsStunned && target.fire,"Control passes through wall");
            owner.SkillMaelstromPull(0,0,1,.85f,22,0,false,caught);
            owner.IsMine=false;owner.SkillMaelstromPull(0,0,1,.85f,22,.016f,false,caught);owner.IsMine=true;
            owner.SkillMaelstromPull(0,0,1,.85f,24,.016f,false,caught);
            Check(!target.IsStunned && target.fire,"Pause/remote/dissolve applies control");
            target.health=0;owner.SkillMaelstromPull(0,0,1,.85f,22,.016f,false,caught);
            Check(!target.IsStunned,"Dead heavy stunned");
            target.health=100;target.invulnerable=true;owner.SkillMaelstromPull(0,0,1,.85f,22,.016f,false,caught);
            Check(!target.IsStunned,"Invulnerable heavy stunned");
            target.invulnerable=false;target.playerNum=owner.playerNum;owner.SkillMaelstromPull(0,0,1,.85f,22,.016f,false,caught);
            Check(!target.IsStunned,"Allied heavy stunned");
        }
        Reset();
        Console.WriteLine("PASS heavy control: real Stun Stop side effects; preserve momentum; airborne; clear input; longer stun preserved; renew while caught; no control outside/wall/dead/immune/ally/pause/remote/dissolve");
    }

    private static void CheckHeavyPull(Aquabro owner)
    {
        foreach (float facing in new float[] {-1,1})
        foreach (MookBigGuy target in new MookBigGuy[] {new MookBigGuy(),new ArmouredHeavy()})
        {
            Reset();target.X=25f*facing;target.Y=0;target.playerNum=-1;Map.Targets.Add(target);
            HashSet<object> caught=new HashSet<object>();caught.Add(target);
            Mook normal=new Mook();normal.X=target.X;normal.Y=target.Y;normal.playerNum=-1;
            Map.Targets.Add(normal);caught.Add(normal);
            for (int tick=0;tick<30;tick++)
            {
                target.yI-=1100f/60f;normal.yI-=1100f/60f;
                owner.SkillMaelstromPull(0,0,facing,.85f,22,1f/60f,false,caught);
            }
            Check(target.yI>100f && target.xI*facing< -40f && normal.yI>100f && normal.xI*facing< -40f,
                "Suction still defeated by gravity/resistance or no longer pulls inward");
            target.xI=normal.xI=0;target.yI=normal.yI=0;
            var knock=typeof(Aquabro).GetMethod("ApplyMaelstromKnock",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            knock.Invoke(null,new object[]{target,80f,180f});knock.Invoke(null,new object[]{normal,80f,180f});
            Check(Math.Abs(target.xI-normal.xI)<.01f && Math.Abs(target.yI-normal.yI)<.01f && Math.Abs(target.yI-180f)<.01f,
                "Heavy/normal impulse compensation differs for the same requested velocity");
            float vx=target.xI,vy=target.yI;owner.IsMine=false;
            owner.SkillMaelstromPull(0,0,facing,.85f,22,.1f,false,caught);owner.IsMine=true;
            Check(vx==target.xI && vy==target.yI,"Remote compensation changes heavy velocity");
        }
        Reset();
        Console.WriteLine("PASS heavy suction: original horizontal /20 and vertical /5; heavy subclasses; both directions; overcome gravity; ordinary strength; ownership");
    }

    private static void CheckSuctionSpread(Aquabro owner)
    {
        foreach(int fps in new int[]{30,60,120})
        {
            Reset();HashSet<object> caught=new HashSet<object>();
            for(int i=0;i<12;i++)caught.Add(Enemy(0,i%2==0?4:60));
            float dt=1f/fps;
            for(int tick=0;tick<fps*3;tick++)
            {
                foreach(Unit target in Map.Targets)target.yI-=1100f*dt;
                owner.SkillMaelstromPullAtTime(0,0,1,.85f,22,dt,false,caught,tick*dt);
                foreach(Unit target in Map.Targets){target.X+=target.xI*dt;target.Y+=target.yI*dt;}
            }
            float low=float.PositiveInfinity,high=float.NegativeInfinity;
            foreach(Unit target in Map.Targets){low=Math.Min(low,target.Y);high=Math.Max(high,target.Y);}
            Check(low>8 && high>30 && high-low>12,"Captured enemies still cluster at the bottom: fps="+fps+" range="+low+".."+high);
            Mook probe=(Mook)Map.Targets[0];
            // The per-unit phase comes from GetHashCode. Two isolated samples
            // can coincide on the orbit; check movement over the full interval.
            Vector2 start=Aquabro.MaelstromAttractor(probe,0,0,.85f,0);
            float movement=0;
            for(int sample=1;sample<=30;sample++)
                movement=Math.Max(movement,Vector2.Distance(start,Aquabro.MaelstromAttractor(probe,0,0,.85f,sample*.1f)));
            Check(movement>8,"Individual suction target does not circulate");
        }
        Reset();Console.WriteLine("PASS vortex spread: 12 enemies from high/low positions circulate at varied heights against real gravity at 30/60/120 FPS");
    }

    private static void CheckWormDestination(Aquabro owner)
    {
        foreach (float facing in new float[] {-1,1})
        foreach (bool registered in new bool[] {false,true})
        foreach (Unit worm in new Unit[] {new HellBoneWormMiniboss(),new AlienMinibossSandWorm(),
            new AlienGiantBossSandworm(),new AlienWormFacehuggerLauncher()})
        {
            Reset();owner.X=owner.Y=0;
            Physics.SharkTerrainEnabled=true;Physics.SharkFloor=true;Physics.SharkFloorY=0;
            Physics.SharkWallX=float.PositiveInfinity;Physics.SharkPropHits=new RaycastHit[0];
            // Root is far below/outside the registry query; only the exposed head is nearby.
            worm.X=facing*350;worm.Y=-200;worm.playerNum=-1;
            if(registered)Map.Targets.Add(worm);
            Collider head=new Collider(facing*120,24,false);head.Relay=new DamageRelay();head.Relay.unit=worm;
            Collider shell=new Collider(facing*30,24,false);shell.Relay=new DamageRelay();shell.Relay.unit=worm;
            shell.Relay.immunity=DamageType.Explosion;
            Physics.Contacts=new Collider[] {shell,head,head};
            Check(owner.FindMaelstromDestination(facing).x==head.bounds.center.x,
                "Exposed worm omitted when root is not in targeting query: "+worm.GetType().Name+" registered="+registered);
            // Native launcher/large-worm heads overlap the floor at their lower edge.
            // Find ground from their visible centre, not from inside/below the floor.
            foreach(float bottom in new float[] {-8,-40})
            {
                Bounds nativeHead=new Bounds(facing*120,0);
                nativeHead.min.y=bottom;nativeHead.max.y=bottom==-8?16:40;head.bounds=nativeHead;
                Check(owner.FindMaelstromDestination(facing).x==head.bounds.center.x,"Partly emerged worm missed because collider bottom is underground");
            }
            head.bounds=new Bounds(facing*120,24);
            Mook closer=Enemy(facing*40,0);
            Check(owner.FindMaelstromDestination(facing).x==closer.X,"Worm overrides nearer infantry");
            closer.health=0;
            foreach(int gate in new int[] {0,1,2,3,4,5,6,7})
            {
                head.enabled=gate!=0;head.gameObject.activeInHierarchy=gate!=1;head.isTrigger=gate==2;
                head.Relay.ignoreDamageWhileColliderActive=gate==3?new Collider(0,0,false):null;
                worm.health=gate==4?0:100;worm.invulnerable=gate==5;worm.playerNum=gate==6?owner.playerNum:-1;
                head.Relay.MarkDestroyed(gate==7);
                Check(owner.FindMaelstromDestination(facing).x==facing*70,"Closed/dead/friendly worm selected: gate="+gate);
            }
            head.Relay.MarkDestroyed(false);head.enabled=true;head.gameObject.activeInHierarchy=true;head.isTrigger=false;
            head.Relay.ignoreDamageWhileColliderActive=null;worm.health=100;worm.invulnerable=false;worm.playerNum=-1;
            Physics.SharkWallX=facing*60;
            Check(owner.FindMaelstromDestination(facing).x==facing*52,"Exposed worm selected through wall");
            Physics.SharkWallX=float.PositiveInfinity;Physics.SharkFloor=false;
            Check(owner.FindMaelstromDestination(facing).x==facing*70,"Airborne worm without floor selected");
            Physics.SharkFloor=true;
            head.bounds=new Bounds(-facing*120,24);
            Check(owner.FindMaelstromDestination(facing).x==facing*70,"Rear worm selected");
            head.bounds=new Bounds(facing*181,24);
            Check(owner.FindMaelstromDestination(facing).x==facing*70,"Distant worm selected");
            head.bounds=new Bounds(facing*120,100);
            Check(owner.FindMaelstromDestination(facing).x==facing*70,"High worm selected");
        }
        Physics.Contacts=new Collider[0];Physics.SharkWallX=float.PositiveInfinity;
        Physics.SharkFloor=true;Reset();
        Console.WriteLine("PASS vortex worm placement: exposed collision relays independent of root/registry; both directions; nearest/armour/death/friendly/wall/floor/range gates");
    }

    private static void CheckDestination(Aquabro owner)
    {
        Reset();owner.X=owner.Y=0;
        Physics.SharkTerrainEnabled=true;Physics.SharkFloor=true;Physics.SharkFloorY=0;
        Physics.SharkWallX=float.PositiveInfinity;Physics.SharkPropHits=new RaycastHit[0];
        foreach (float facing in new float[] {-1,1})
        {
            Map.Targets.Clear();Mook enemy=Enemy(facing*110,0);Mook behind=Enemy(-facing*20,0);
            Vector2 dest=owner.FindMaelstromDestination(facing);
            Check(dest.x==enemy.X && dest.y==0,"Cast did not prefer facing enemy");
            Mook nearer=Enemy(facing*40,0);dest=owner.FindMaelstromDestination(facing);
            Check(dest.x==nearer.X,"Cast did not select nearest forward enemy");
            nearer.invulnerable=true;enemy.health=0;dest=owner.FindMaelstromDestination(facing);
            Check(dest.x==facing*70 && dest.y==0,"Rear enemy selected when no valid forward enemy exists");
            behind.X=-facing*.01f;dest=owner.FindMaelstromDestination(facing);
            Check(dest.x==facing*70,"Enemy just behind facing boundary selected");
            behind.X=0;dest=owner.FindMaelstromDestination(facing);
            Check(dest.x==0 && dest.y==0,"Enemy directly aligned with caster rejected");
            behind.playerNum=owner.playerNum;dest=owner.FindMaelstromDestination(facing);
            Check(dest.x==facing*70 && dest.y==0,"No target fallback not forward/grounded");
            Map.Targets.Clear();Enemy(facing*181,0);Enemy(facing*30,97);Enemy(-facing*65,0);
            Check(owner.FindMaelstromDestination(facing).x==facing*70,"Selected enemy outside targeting range");
        }
        foreach (float facing in new float[] {-1,1})
        foreach (Unit target in new Unit[] {new Tank(),new TankWeapon(),new Mech(),new AlienSandWorm(),new Unit()})
        {
            Map.Targets.Clear();target.X=facing*124.329f;target.Y=16;target.playerNum=-1;
            Physics.SharkFloorY=16;Map.Targets.Add(target);
            Vector2 destination=owner.FindMaelstromDestination(facing);
            Check(Math.Abs(destination.x-target.X)<.001f && destination.y==16,
                "Forward non-infantry enemy rejected: "+target.GetType().Name+" facing="+facing);
            Mook closer=Enemy(facing*60,16);
            Check(owner.FindMaelstromDestination(facing).x==closer.X,"Vehicle overrides nearer infantry");
            closer.health=0;target.invulnerable=true;
            Check(owner.FindMaelstromDestination(facing).x==facing*70,"Invulnerable vehicle selected");
            target.invulnerable=false;target.health=0;
            Check(owner.FindMaelstromDestination(facing).x==facing*70,"Destroyed vehicle selected");
            target.health=100;target.playerNum=owner.playerNum;
            Check(owner.FindMaelstromDestination(facing).x==facing*70,"Friendly vehicle selected");
            target.playerNum=-1;Physics.SharkWallX=facing*60;
            Check(owner.FindMaelstromDestination(facing).x==facing*52,"Vehicle targeted through a wall");
            Physics.SharkWallX=float.PositiveInfinity;
        }
        Physics.SharkFloorY=0;
        Map.Targets.Clear();Mook blocked=Enemy(110,0);Physics.SharkWallX=60;
        Vector2 stopped=owner.FindMaelstromDestination(1);
        Check(stopped.x==52 && stopped.y==0,"Wall did not reject target/clamp fallback");
        Physics.SharkWallX=float.PositiveInfinity;
        foreach (object propType in new object[] {new CrateBlock(),new BarrelBlock(),new Doodad(0,0)})
        {
            Collider prop=new Collider(20,14,false);prop.ParentComponent=propType;
            Bounds bounds=new Bounds();bounds.min=new Vector3(12,0,0);bounds.max=new Vector3(118,40,0);prop.bounds=bounds;
            RaycastHit hit=new RaycastHit();hit.collider=prop;hit.point=new Vector3(12,40,0);
            Physics.SharkPropHits=new RaycastHit[] {hit};
            Vector2 dest=owner.FindMaelstromDestination(1);
            Check(dest.x==110 && dest.y==0,"Prop blocked target cast or replaced real ground");
            HashSet<object> caught=new HashSet<object>();caught.Add(blocked);
            owner.SkillMaelstromPull(100,0,1,.85f,22,.016f,false,caught);
            Check(blocked.KnockCalls>0,"Prop blocked suction");blocked.KnockCalls=0;
            Physics.SharkWallX=60;
            Check(owner.FindMaelstromDestination(1).x==52,"Ignored real wall behind prop");
            Physics.SharkWallX=float.PositiveInfinity;
            Map.Targets.Clear();Check(owner.FindMaelstromDestination(1).x==70,"Prop blocked empty forward cast");
            Map.Targets.Add(blocked);
        }
        Physics.SharkPropHits=new RaycastHit[0];Physics.SharkFloor=false;
        Check(owner.FindMaelstromDestination(1).x==70,"Enemy without ground selected");
        Physics.SharkTerrainEnabled=false;Physics.SharkFloor=true;Reset();
        Console.WriteLine("PASS vortex targeting: infantry/tank/weapon/mech/worm/other hostile units; nearest forward only; rear rejection in both directions; aligned target; forward fallback; 180px/96px limits; dead/immune/ally rejection; crate/barrel/doodad ignored for placement/ground/suction; wall behind prop blocks; missing floor fallback");
    }
}

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
                Check(MaelstromPixelMask.Contains(frame,x+.5f,y+.5f,0)==opaque,"Maelstrom mask/PNG mismatch");
                if (opaque && px<0) {px=x;py=y;}
            }
            Check(px>=0,"Missing active water frame");
            foreach (float facing in new float[] {-1,1})
            foreach (float scale in new float[] {.65f,.85f,1f})
            {
                Reset();
                Mook target=Enemy((px+.5f-64)*scale*facing,(112-py-.5f)*scale-6);
                HashSet<Unit> caught=new HashSet<Unit>();
                owner.SkillMaelstromPull(0,0,facing,scale,frame,.016f,true,caught);
                Check(caught.Contains(target) && target.KnockCalls==1 && target.ForcedTumble && Map.Hits[target]==3,
                    "Visible maelstrom contact missed / non-tumbling enemy excluded, frame "+frame);
            }
        }
        // Exact paused-game snapshot: platform at Y=568, vortex anchored at 504.
        Reset();Mook rim=Enemy(882.439f,568f);HashSet<Unit> snapshotCaught=new HashSet<Unit>();
        Physics.BlockedBelowY=568f;
        Check(!owner.SkillLineOfSight(842.0003f,528f,rim),"Snapshot does not reproduce old blocked low ray");
        owner.SkillMaelstromPull(842.0003f,504f,-1,.85f,22,.016f,true,snapshotCaught);
        Check(snapshotCaught.Contains(rim) && rim.xI<0 && Map.Hits[rim]==3,"Upper-platform rim contact still ignored");
        HashSet<Unit> finished=new HashSet<Unit>();
        owner.SkillMaelstromFinish(842.0003f,504f,-1,.85f,snapshotCaught,finished);
        Check(Map.Hits[rim]==9,"Upper rim not included in final damage");
        owner.SkillMaelstromFinish(842.0003f,504f,-1,.85f,snapshotCaught,finished);
        Check(Map.TotalHits==2,"Finisher damages same target twice");

        Reset();Mook outside=Enemy(60,0);HashSet<Unit> caughtSet=new HashSet<Unit>();
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
        owner.SkillMaelstromFinish(0,0,1,1,caughtSet,new HashSet<Unit>());
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
        owner.SkillMaelstromPull(0,0,1,1,22,.016f,true,new HashSet<Unit>());
        Check(lower.KnockCalls+dead.KnockCalls+immune.KnockCalls+ally.KnockCalls==0 && Map.TotalHits==1 && Map.Hits[dead]==3,
            "Corpse missed or below-floor/immune/allied enemy affected");

        Reset();Enemy(0,20);
        AquabroSkillVisual effect=new AquabroSkillVisual(owner,2,0,0,-1,AquabroSkillVisual.MaelstromLifetime,0,.85f);
        ((HashSet<Unit>)typeof(AquabroSkillVisual).GetField("maelstromCaught",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(effect)).Add(Map.Targets[0]);
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
        CheckHeavyControl(owner);
        CheckDestination(owner);
        CheckDamageTimeline(owner);
        CheckWallTravel(owner);
        Console.WriteLine("PASS maelstrom: 24 active masks; mirror/scales; live-scene upper-platform rim; physical knock without canTumble gate; gravity lift; captured gaps; wall/floor/ally/pause/ownership; pulse/one finisher; dissolve inactive");
    }

    private sealed class ArmouredHeavy : MookBigGuy {}

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

    private static void CheckDamageTimeline(Aquabro owner)
    {
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        foreach (int fps in new int[]{30,50,60,120})
        {
            Reset();Unit vehicle=new Unit();vehicle.playerNum=-1;vehicle.Y=20;Map.Targets.Add(vehicle);
            AquabroSkillVisual effect=new AquabroSkillVisual(owner,2,0,0,1,AquabroSkillVisual.MaelstromLifetime,0,.85f);
            var caught=(HashSet<Unit>)typeof(AquabroSkillVisual).GetField("maelstromCaught",flags).GetValue(effect);
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
            Reset();target.X=25;target.Y=0;target.playerNum=-1;Map.Targets.Add(target);
            target.fire=target.left=target.doingMelee=true;target.xI=-120;target.yI=80;
            HashSet<Unit> caught=new HashSet<Unit>();caught.Add(target);
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
            HashSet<Unit> caught=new HashSet<Unit>();caught.Add(target);
            Mook normal=new Mook();normal.X=target.X;normal.Y=target.Y;normal.playerNum=-1;
            Map.Targets.Add(normal);caught.Add(normal);
            for (int tick=0;tick<30;tick++)
            {
                target.yI-=1100f/60f;normal.yI-=1100f/60f;
                owner.SkillMaelstromPull(0,0,facing,.85f,22,1f/60f,false,caught);
            }
            Check(target.yI>100f && target.xI*facing< -100f,"Heavy suction still defeated by gravity/resistance");
            Check(Math.Abs(target.xI-normal.xI)<.01f && Math.Abs(target.yI-normal.yI)<.01f,"Heavy suction not compensated to normal strength");
            float vx=target.xI,vy=target.yI;owner.IsMine=false;
            owner.SkillMaelstromPull(0,0,facing,.85f,22,.1f,false,caught);owner.IsMine=true;
            Check(vx==target.xI && vy==target.yI,"Remote compensation changes heavy velocity");
        }
        Reset();
        Console.WriteLine("PASS heavy suction: original horizontal /20 and vertical /5; heavy subclasses; both directions; overcome gravity; ordinary strength; ownership");
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
            HashSet<Unit> caught=new HashSet<Unit>();caught.Add(blocked);
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
        Console.WriteLine("PASS vortex targeting: nearest forward only; rear rejection in both directions; aligned target; forward fallback; 180px/96px limits; invalid targets; crate/barrel/doodad ignored for placement/ground/suction; wall behind prop blocks; missing floor fallback");
    }
}

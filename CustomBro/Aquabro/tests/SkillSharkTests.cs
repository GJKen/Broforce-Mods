using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using AquabroMod;
using UnityEngine;

internal static class SkillSharkTests
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Call(Aquabro owner, string name, params object[] args)
    { typeof(Aquabro).GetMethod(name, Hidden).Invoke(owner, args); }
    private static AquabroSkillVisual Active(Aquabro owner)
    { return (AquabroSkillVisual)typeof(Aquabro).GetField("activeShark", Hidden).GetValue(owner); }
    private static void Reset()
    {
        Map.Targets.Clear(); Map.Hits.Clear(); Map.TotalHits=MapController.ObjectHits=Map.DoodadHits=0;
        Map.destroyableDoodads.Clear();Physics.Contacts=new Collider[0];Physics.Blocked=false;
    }
    private static float Position(Aquabro owner)
    { return (float)typeof(AquabroSkillVisual).GetField("x",Hidden).GetValue(Active(owner)); }
    private static void Steer(Aquabro owner,bool left,bool right)
    {owner.left=left;owner.right=right;Call(owner,"CheckInput");}
    private static void Launch(Aquabro owner)
    {
        owner.SpecialAmmo=3;owner.Grounded=true;owner.health=100;owner.Incapacitated=false;
        owner.actionState=ActionState.Idle;Call(owner,"PressSpecial");Call(owner,"UseSpecial");
    }
    private static Unit AtPixel(int x,int y,float facing)
    {
        Unit target=new Unit(); target.X=(x+.5f-64)*facing;target.Y=112-y-.5f-6;target.playerNum=-1;
        Map.Targets.Add(target); return target;
    }

    internal static void Run(Bitmap atlas)
    {
        Aquabro owner=new Aquabro();
        for (int i=0;i<4 && owner.SelectedSkill!=1;i++) owner.SetGestureAnimation(GestureElement.Gestures.Flex);
        Check(owner.SelectedSkill==1,"Cannot select shark");
        owner.Grounded=false;Call(owner,"PressSpecial");
        Check(!owner.usingSpecial && owner.SpecialAmmo==3,"Air cast accepted/spent ammo");
        owner.Grounded=true;
        foreach (string state in new string[] {"WallDrag","wallClimbing","ledgeGrapple"})
        {
            FieldInfo field=typeof(TestHero).GetField(state);field.SetValue(owner,true);
            Call(owner,"PressSpecial");Check(!owner.usingSpecial,"Traversal cast accepted: "+state);field.SetValue(owner,false);
        }
        owner.attachedToZipline=new object();Call(owner,"PressSpecial");
        Check(!owner.usingSpecial,"Zipline cast accepted");owner.attachedToZipline=null;
        CheckRunningCast(owner);
        CheckControlReturn();
        CheckBreachDash(owner);
        CheckLadderCasts(owner);
        CheckLadderDropInput(owner);
        CheckSubmergedPanic(owner);
        Call(owner,"PressSpecial");owner.Grounded=false;Call(owner,"UseSpecial");
        Check(Active(owner)==null && owner.SpecialAmmo==3,"Walking off edge during wind-up spent ammo");
        owner.usingSpecial=false;owner.Grounded=true;
        HeroController.Dredd.specialRocketPrefab.life=12f;
        Physics.SharkTerrainEnabled=true;Physics.SharkFloor=true;Physics.SharkFloorY=0;
        Launch(owner);
        Check(Active(owner)!=null && owner.SpecialAmmo==2 && owner.sprite.meshRender.enabled && !owner.usingSpecial,"Hero should remain visible after launch");
        Check(HeroController.CameraTarget==Active(owner).CameraTarget,"Camera does not follow shark");
        Check((float)typeof(AquabroSkillVisual).GetField("lifetime",Hidden).GetValue(Active(owner))==12f,"Control duration differs from Dredd rocket");
        owner.up=owner.down=owner.buttonJump=owner.fire=true;Steer(owner,false,true);
        Check(!owner.left && !owner.right && !owner.up && !owner.down && !owner.fire && !owner.buttonJump,"Remote input also reached hero");
        Call(owner,"CalculateMovement");Call(owner,"RunMovement");
        Check(owner.X==0 && owner.xI==0 && !owner.MovementSawTraversal && !owner.MovementSawJump,"Hero moved/climbed during remote control");
        Check(owner.canWallClimb && owner.canLedgeGrapple && owner.canCeilingHang,"Traversal capabilities not restored");
        Reset();Call(owner,"TickSkillPreview",1f);
        Check(Math.Abs(Position(owner)-90f)<.01f && owner.X==0,"Shark movement is not independent of hero");
        Check(Active(owner).SharkControlled && SpriteSM.LastFrame<6 && Map.TotalHits==0,"Shark jumped/attacked without second press");
        Steer(owner,true,false);Call(owner,"TickSkillPreview",.5f);Check(Math.Abs(Position(owner)-45)<.01f,"Cannot reverse shark");
        Steer(owner,false,false);Call(owner,"TickSkillPreview",.2f);Check(Math.Abs(Position(owner)-45)<.01f,"Shark drifts without input");
        Physics.SharkWallX=65f;Steer(owner,false,true);Call(owner,"TickSkillPreview",.5f);
        Check(Math.Abs(Position(owner)-61)<.01f && Active(owner).CameraTarget.position.y==48,"Shark climbed/passed terrain wall");
        Physics.SharkWallX=float.PositiveInfinity;
        Collider prop=new Collider(70,4,false);Physics.Contacts=new Collider[] {prop};
        Call(owner,"TickSkillPreview",.5f);
        Check(Position(owner)>70 && prop.enabled && MapController.ObjectHits==0 && Map.TotalHits==0,"Submerged shark blocked/damaged a prop");
        Physics.SharkFloor=false;float stopped=Position(owner);Call(owner,"TickSkillPreview",.1f);
        Check(Position(owner)==stopped,"Shark moved into air");Physics.SharkFloor=true;
        foreach (float drop in new float[] {96f,1024f,1000000f})
        {
            float testX=0f,testY=248f;Physics.SharkFloorY=testY-drop;
            owner.MoveShark(ref testX,ref testY,1f,.05f);
            Check(testX>0f && testY==248f-drop,"Drop height still limited: "+drop);
        }
        Physics.SharkFloorY=0f;
        owner.Grounded=false;Call(owner,"TickSkillPreview",.1f);Check(Active(owner)!=null,"Remote shark depends on hero ground contact");
        stopped=Position(owner);Call(owner,"TickSkillPreview",0f);Check(Position(owner)==stopped && Active(owner).SharkControlled,"Pause advanced shark");
        owner.SpecialAmmo=0;Call(owner,"PressSpecial");
        Check(!Active(owner).SharkControlled && owner.SpecialAmmo==0,"Second press did not breach without extra ammo");
        Call(owner,"TickSkillPreview",.3f);int breachFrame=SpriteSM.LastFrame;
        Call(owner,"PressSpecial");Call(owner,"TickSkillPreview",.05f);
        Check(SpriteSM.LastFrame>breachFrame && Position(owner)>stopped && Position(owner)<=stopped+24.01f,"Repeated press restarted breach or dash distance is wrong");
        Call(owner,"TickSkillPreview",.8f);
        Check(Active(owner)==null && HeroController.CameraTarget==null && owner.sprite.meshRender.enabled,"Finish did not restore camera/hero");
        Reset();Launch(owner);Steer(owner,false,false);
        Call(owner,"TickSkillPreview",11.9f);Check(Active(owner).SharkControlled,"Dredd-length control expired early");
        Call(owner,"TickSkillPreview",.11f);Check(!Active(owner).SharkControlled && SpriteSM.LastFrame>=6,"Timeout did not start breach");
        Call(owner,"TickSkillPreview",1.2f);Check(Active(owner)==null,"Timed breach never ends");
        foreach (int interruption in new int[] {0,1,2})
        {
            Launch(owner);
            if (interruption==0) owner.health=0;
            else if (interruption==1) owner.Incapacitated=true;
            if (interruption==2) Call(owner,"OnDisable");else Call(owner,"TickSkillPreview",.02f);
            Check(Active(owner)==null && HeroController.CameraTarget==null && owner.sprite.meshRender.enabled,"Interrupted shark/camera remains active");
        }
        Physics.SharkTerrainEnabled=false;
        owner.health=100;owner.Incapacitated=false;owner.Grounded=true;owner.X=owner.Y=0;
        int bodyCount=0,waterCount=0;
        for (int f=0;f<28;f++)
        {
            int bodyX=-1,bodyY=0,waterX=-1,waterY=0;
            for (int py=0;py<128;py++) for (int px=0;px<128;px++)
            {
                bool mask=SharkPixelMask.Contains(f,px+.5f,py+.5f,0);
                Check(!mask || atlas.GetPixel(f*128+px,py).A>0,"Shark hitbox has invisible pixels");
                if (mask) {bodyCount++;bodyX=px;bodyY=py;}
                if (atlas.GetPixel(f*128+px,py).A>0 && !SharkPixelMask.Contains(f,px+.5f,py+.5f,5))
                {waterCount++;waterX=px;waterY=py;}
            }
            foreach (float facing in new float[] {-1,1})
            {
                Reset();HashSet<Unit> hits=new HashSet<Unit>();
                Unit body=bodyX>=0 ? AtPixel(bodyX,bodyY,facing) : null;
                Unit water=waterX>=0 ? AtPixel(waterX,waterY,facing) : null;
                owner.SkillSharkBlast(0,0,facing,1,f,hits);
                int expected=body!=null && f>=6 && f<27 ? 1 : 0;
                Check(Map.TotalHits==expected && (water==null || !Map.Hits.ContainsKey(water)),"Body/water damage mismatch frame "+f);
                if (expected>0) Check(Map.Hits[body]==28,"Shark damage changed");
                owner.SkillSharkBlast(0,0,facing,1,f,hits);
                Check(Map.TotalHits==expected,"Repeated shark damage");
                Reset();if (body!=null) AtPixel(bodyX,bodyY,facing);
                Physics.Blocked=true;owner.SkillSharkBlast(0,0,facing,1,f,new HashSet<Unit>());
                Check(Map.TotalHits==0 || (body!=null && Math.Abs(body.X)<8f),"Shark damages through walls");
                Reset();if (body!=null) AtPixel(bodyX,bodyY,facing);
                Physics.Blocked=false;owner.IsMine=false;owner.SkillSharkBlast(0,0,facing,1,f,new HashSet<Unit>());
                Check(Map.TotalHits==0,"Remote shark applied damage");owner.IsMine=true;
            }
        }
        Check(bodyCount>0 && waterCount>0,"Body/water masks were not separated");
        Check(!SharkPixelMask.Contains(27,64,64,200),"Transparent final frame damages");
        CheckObjects(owner,atlas);
        CheckSupportFloor(owner);
        CheckGroundLayerProps(owner);
        CheckElevatorFloor(owner);
        CheckElevatorMotion(owner);
        Check(HeroController.CameraAdds==HeroController.CameraRemoves,"Camera target leaked");
        Reset();Console.WriteLine("PASS shark: visible stationary hero; remote movement; Dredd duration; prop passthrough; unlimited 96/1024/1000000px drops; support floor preserved; second-press/timeout breach; camera/cleanup; body-only damage; mirror; pause; single hit; LOS/ownership");
    }

    private static void CheckControlReturn()
    {
        FieldInfo delay=typeof(TestHero).GetField("controllingProjectileDelay",Hidden);
        foreach (bool timeout in new bool[] {false,true})
        foreach (float facing in new float[] {-1f,1f})
        {
            Reset();
            Aquabro hero=new Aquabro();hero.transform.localScale=new Vector3(facing,1f,1f);
            Launch(hero);
            AquabroSkillVisual effect=Active(hero);
            int removes=HeroController.CameraRemoves;
            Check(HeroController.CameraTarget==effect.CameraTarget,"Shark camera missing during control");
            if (timeout)
            {
                float lifetime=(float)typeof(AquabroSkillVisual).GetField("lifetime",Hidden).GetValue(effect);
                Call(hero,"TickSkillPreview",lifetime+.001f);
            }
            else Call(hero,"PressSpecial");
            Check(HeroController.CameraTarget==null && HeroController.CameraRemoves==removes+1,
                "Shark camera waits for breach animation to finish");
            Check(Active(hero)==effect && !effect.SharkControlled,
                "Returning camera ended the breach animation");
            Check(Math.Abs((float)delay.GetValue(hero)-.25f)<.00001f,
                "Shark release did not start Dredd's native 0.25s input delay");
            // Simulate native CheckInput consuming part of its own timer; repeated
            // special presses and autonomous animation must not restart it.
            delay.SetValue(hero,.1f);
            Call(hero,"PressSpecial");Call(hero,"TickSkillPreview",.1f);
            Check((float)delay.GetValue(hero)==.1f && HeroController.CameraRemoves==removes+1 && Active(hero)==effect,
                "Breach tick/second press restarted delay, removed camera twice, or ended visuals");
            delay.SetValue(hero,0f);
            hero.right=hero.up=hero.fire=hero.buttonJump=true;
            Call(hero,"CheckInput");
            Check(hero.right && hero.up && hero.fire && hero.buttonJump && Active(hero)==effect,
                "Finished native delay still blocks hero input until animation ends");
            Call(hero,"TickSkillPreview",1.2f);
            Check(Active(hero)==null && HeroController.CameraRemoves==removes+1,
                "Breach cleanup removes camera twice");
        }
        Reset();
        Aquabro longer=new Aquabro();Launch(longer);delay.SetValue(longer,.6f);Call(longer,"PressSpecial");
        Check((float)delay.GetValue(longer)==.6f,"Shark release shortened an existing native input delay");
        Call(longer,"ClearSkillPreview");
        foreach (int interruption in new int[] {0,1,2})
        {
            Aquabro hero=new Aquabro();Launch(hero);int removes=HeroController.CameraRemoves;
            if (interruption==0) hero.health=0;
            else if (interruption==1) hero.Incapacitated=true;
            if (interruption==2) Call(hero,"OnDisable");else Call(hero,"TickSkillPreview",.02f);
            Check(Active(hero)==null && HeroController.CameraTarget==null && HeroController.CameraRemoves==removes+1 &&
                (float)delay.GetValue(hero)==0f,"Interrupted shark leaked camera or introduced a recovery delay");
        }
        Console.WriteLine("PASS shark return: manual/timeout; both facings; immediate camera; native 0.25s delay; animation continues; repeated press; longer delay; interruptions; single cleanup");
    }

    private static void CheckRunningCast(Aquabro owner)
    {
        foreach (float facing in new float[] {-1f,1f})
        foreach (bool movementBeforeRelease in new bool[] {false,true})
        {
            Reset();
            owner.Grounded=true;owner.SpecialAmmo=3;owner.actionState=ActionState.Running;
            owner.transform.localScale=new Vector3(facing,1,1);owner.xI=facing*130f;
            owner.left=facing<0;owner.right=facing>0;
            Call(owner,"PressSpecial");
            Check(owner.usingSpecial && owner.TestBodyCell==392,"Running shark cast skipped its first cast frame");
            if (movementBeforeRelease)
            {
                Call(owner,"CalculateMovement");
                Check(owner.actionState==ActionState.Idle && owner.xI==0f,"Shark wind-up retained running state");
            }
            owner.frame=2;Call(owner,"AnimateSpecial");
            Check(Active(owner)!=null && owner.actionState==ActionState.Idle && owner.frame==0 && owner.xI==0f,
                "Running launch chose a running pose for the stationary hero");
            float heroX=owner.X;
            // Guard against a stale running state being reintroduced while controlling.
            owner.actionState=ActionState.Running;
            Steer(owner,facing<0,facing>0);Call(owner,"CalculateMovement");Call(owner,"RunMovement");
            Check(owner.actionState==ActionState.Idle && owner.X==heroX && owner.xI==0f,
                "Shark steering retained hero running state or movement");
            Call(owner,"ClearSkillPreview");
        }
        owner.left=owner.right=false;owner.actionState=ActionState.Idle;
        Console.WriteLine("PASS shark running cast: both facings; cast frame; wind-up; immediate release; stationary idle hero during steering");
    }

    private static void CheckBreachDash(Aquabro owner)
    {
        foreach (float facing in new float[] {-1,1})
        foreach (int fps in new int[] {30,60,120})
        {
            Reset();Physics.SharkTerrainEnabled=true;Physics.SharkWallX=float.PositiveInfinity;
            AquabroSkillVisual effect=new AquabroSkillVisual(owner,1,0,0,facing,12,0,1);
            effect.BreachShark();
            for(int i=0;i<fps*.6f;i++)effect.Tick(1f/fps);
            Check(Math.Abs(effect.CameraTarget.position.x-facing*24)<.01f,"Shark breach does not advance exactly 24 pixels");
            Check(effect.CameraTarget.position.y==48,"Breach changed its floor origin");
            float x=effect.CameraTarget.position.x;effect.Tick(0);effect.BreachShark();effect.Tick(.05f);
            Check(effect.CameraTarget.position.x==x,"Pause/repeated press restarts dash");effect.Dispose();
            Physics.SharkWallX=facing*20;
            effect=new AquabroSkillVisual(owner,1,0,0,facing,12,0,1);effect.BreachShark();effect.Tick(.5f);
            Check(Math.Abs(effect.CameraTarget.position.x)<=12.01f,"Shark dash crosses wall");effect.Dispose();
        }
        Reset();Physics.SharkTerrainEnabled=false;Physics.SharkWallX=float.PositiveInfinity;
        Console.WriteLine("PASS shark breach dash: +24 pixels over 0.45s; mirror/FPS; fixed floor origin; wall; pause/repeated press");
    }

    private static void CheckLadderCasts(Aquabro owner)
    {
        owner.Grounded=false;
        foreach (ActionState state in new ActionState[] {ActionState.Idle,ActionState.Running,ActionState.ClimbingLadder})
        {
            owner.actionState=state;
            owner.LadderTopSupport=state!=ActionState.ClimbingLadder;
            owner.LadderContact=state==ActionState.ClimbingLadder;
            owner.SpecialAmmo=3;
            Call(owner,"PressSpecial");
            Check(owner.usingSpecial,"Ladder/top cast rejected: "+state);
            Call(owner,"UseSpecial");
            Check(Active(owner)!=null && owner.SpecialAmmo==2,"Ladder/top release rejected: "+state);
            owner.up=owner.down=owner.buttonJump=true;Steer(owner,false,true);
            Call(owner,"CalculateMovement");Call(owner,"RunMovement");
            ActionState expected=state==ActionState.Running ? ActionState.Idle : state;
            Check(owner.xI==0 && !owner.MovementSawTraversal && !owner.MovementSawJump && owner.actionState==expected,
                "Ladder cast detached/moved hero");
            Call(owner,"ClearSkillPreview");
        }
        owner.SpecialAmmo=3;owner.LadderTopSupport=true;owner.LadderContact=true;
        owner.actionState=ActionState.Jumping;Call(owner,"PressSpecial");
        Check(!owner.usingSpecial,"Air cast near ladder accepted");
        owner.LadderTopSupport=false;owner.actionState=ActionState.ClimbingLadder;
        Call(owner,"PressSpecial");owner.LadderContact=false;Call(owner,"UseSpecial");
        Check(Active(owner)==null && owner.SpecialAmmo==3,"Losing ladder during wind-up spent ammo");
        owner.usingSpecial=false;Call(owner,"PressSpecial");
        Check(!owner.usingSpecial,"Stale ladder state without contact accepted");
        owner.Grounded=true;owner.actionState=ActionState.Idle;
        Console.WriteLine("PASS shark ladder: top standing/running and climbing cast; stationary control; air near ladder rejected; lost contact spends no ammo");
    }

    private static void CheckSubmergedPanic(Aquabro owner)
    {
        Reset();Launch(owner);
        Unit left=new Unit();left.X=-30;left.playerNum=-1;Map.Targets.Add(left);
        Unit right=new Unit();right.X=30;right.playerNum=-1;Map.Targets.Add(right);
        Unit ally=new Unit();ally.X=20;ally.playerNum=owner.playerNum;Map.Targets.Add(ally);
        Unit far=new Unit();far.X=65;far.playerNum=-1;Map.Targets.Add(far);
        Unit below=new Unit();below.X=20;below.Y=-16;below.playerNum=-1;Map.Targets.Add(below);
        Unit high=new Unit();high.X=20;high.Y=48;high.playerNum=-1;Map.Targets.Add(high);
        Unit immune=new Unit();immune.X=20;immune.playerNum=-1;immune.invulnerable=true;Map.Targets.Add(immune);
        Unit dead=new Unit();dead.X=20;dead.playerNum=-1;dead.health=0;Map.Targets.Add(dead);
        Call(owner,"TickSkillPreview",.01f);
        Check(left.PanicCalls==1 && left.PanicDirection==-1 && right.PanicCalls==1 && right.PanicDirection==1 && right.PanicDuration>0,
            "Submerged shark does not scare enemies away from fin");
        Check(ally.PanicCalls==0 && far.PanicCalls==0 && below.PanicCalls==0 && high.PanicCalls==0 && immune.PanicCalls==0 && dead.PanicCalls==0 && Map.TotalHits==0,
            "Submerged fear affects allies/out-of-range/inactive targets or deals damage");
        Call(owner,"TickSkillPreview",.01f);Call(owner,"TickSkillPreview",0f);
        Check(right.PanicCalls==1,"Fear refreshes every frame or while paused");
        Physics.Blocked=true;Call(owner,"TickSkillPreview",.31f);
        Check(right.PanicCalls==1,"Fear passes through walls");Physics.Blocked=false;
        owner.IsMine=false;Call(owner,"TickSkillPreview",.31f);
        Check(right.PanicCalls==1,"Remote shark applies fear");owner.IsMine=true;
        Call(owner,"TickSkillPreview",.31f);Check(right.PanicCalls==2,"Submerged fear does not refresh");
        Call(owner,"PressSpecial");Call(owner,"TickSkillPreview",.31f);
        Check(right.PanicCalls==2,"Breach still applies submerged fear");
        Call(owner,"ClearSkillPreview");Reset();owner.SpecialAmmo=3;
        Console.WriteLine("PASS submerged shark fear: enemies flee away; refresh; no damage; no allies/below floor/through walls; pause/ownership; breach ends fear");
    }

    private static void CheckLadderDropInput(Aquabro owner)
    {
        Collider ladder=new Collider(0,280,true);
        Bounds bounds=new Bounds();bounds.min=new Vector3(-16,248,0);bounds.max=new Vector3(16,312,0);ladder.bounds=bounds;
        foreach (float height in new float[] {312f,280f})
        foreach (float direction in new float[] {-1f,1f})
        foreach (int fps in new int[] {30,60,120})
        {
            Reset();Physics.LadderContacts=new Collider[] {ladder};
            Physics.SharkTerrainEnabled=true;Physics.SharkFloor=true;Physics.SharkFloorY=0;
            Physics.SharkWallX=float.PositiveInfinity;owner.X=0;owner.Y=height;
            Launch(owner);
            Check(Active(owner).CameraTarget.position.y==height+48,"Ladder cast fell before down input");
            owner.down=false;Steer(owner,false,false);Call(owner,"TickSkillPreview",.02f);
            Check(Active(owner).CameraTarget.position.y==height+48,"Idle shark fell on ladder");
            Steer(owner,direction<0,direction>0);Call(owner,"TickSkillPreview",1f/fps);
            Check(Active(owner).CameraTarget.position.y==height+48 && Position(owner)*direction>0,"Horizontal ladder input caused drop");
            owner.down=true;Steer(owner,false,false);
            Check(!owner.down,"Drop input reached hero");
            Call(owner,"TickSkillPreview",0f);
            Check(Active(owner).CameraTarget.position.y==height+48,"Paused down input moved shark");
            Call(owner,"TickSkillPreview",1f/fps);
            Check(Active(owner).CameraTarget.position.y==48,"Down alone did not drop to solid ground");
            owner.down=false;Steer(owner,false,false);
            Physics.SharkFloorY=-96;Call(owner,"TickSkillPreview",1f/fps);
            Check(Active(owner).CameraTarget.position.y==-48,"Idle shark did not refresh changed ground support");
            Call(owner,"ClearSkillPreview");
        }
        // Walking off the ladder still follows ordinary descending terrain.
        owner.X=0;owner.Y=312;Physics.SharkFloorY=0;Launch(owner);
        Steer(owner,false,true);Call(owner,"TickSkillPreview",.3f);
        Check(Active(owner).CameraTarget.position.y==48,"Ordinary descent past ladder edge blocked");
        Call(owner,"ClearSkillPreview");
        // No destination below: down cannot drop the shark into the void.
        Physics.SharkFloor=false;Launch(owner);owner.down=true;Steer(owner,false,false);
        Call(owner,"TickSkillPreview",.1f);
        Check(Active(owner).CameraTarget.position.y==360,"Down dropped shark into void");
        owner.down=false;Steer(owner,false,false);Call(owner,"ClearSkillPreview");
        Physics.SharkFloor=true;Physics.SharkFloorY=248;Launch(owner);
        owner.down=true;Steer(owner,false,true);Call(owner,"TickSkillPreview",.1f);
        Check(Math.Abs(Position(owner)-9)<.01f && Active(owner).CameraTarget.position.y==296,
            "Down plus horizontal input did not drop onto solid floor");
        // A held down key cannot pass through the solid destination floor.
        owner.down=true;Steer(owner,false,false);Call(owner,"TickSkillPreview",.1f);
        Check(Active(owner).CameraTarget.position.y==296,"Held down passed through solid ground");
        Call(owner,"ClearSkillPreview");
        owner.down=false;owner.X=0;Launch(owner);
        Call(owner,"TickSkillPreview",.1f);
        Check(Active(owner).CameraTarget.position.y==360,"Previous cast down input leaked into new shark");
        Call(owner,"ClearSkillPreview");
        Physics.LadderContacts=new Collider[0];Physics.SharkFloor=true;Physics.SharkTerrainEnabled=false;
        Physics.SharkFloorY=0;
        owner.X=owner.Y=0;
        Console.WriteLine("PASS shark ladder drop: top/body cast height; idle/horizontal support; down alone; pause/release; both directions/FPS; ordinary descent; void protection");
    }

    private static void CheckGroundLayerProps(Aquabro owner)
    {
        Physics.SharkTerrainEnabled=true;Physics.SharkFloor=true;Physics.SharkFloorY=0;
        foreach (object propType in new object[] {new CrateBlock(),new BarrelBlock(),new Doodad(0,0)})
        {
            Collider prop=new Collider(12,4,false);prop.ParentComponent=propType;
            Bounds bounds=new Bounds();bounds.min=new Vector3(8,0,0);bounds.max=new Vector3(24,32,0);prop.bounds=bounds;
            RaycastHit hit=new RaycastHit();hit.collider=prop;hit.point=new Vector3(8,4,0);
            Physics.SharkPropHits=new RaycastHit[] {hit};Physics.SharkWallX=float.PositiveInfinity;
            float x=0,y=0;owner.MoveShark(ref x,ref y,1,.5f);
            Check(Math.Abs(x-45f)<.01f && y==0 && prop.enabled,"Ground-layer prop blocked/damaged shark: "+propType.GetType().Name);
            Physics.SharkWallX=40;x=y=0;owner.MoveShark(ref x,ref y,1,.5f);
            Check(x==36 && y==0,"Ignored real wall behind Ground-layer prop");
            // A crate may be the first downward ray hit when spawning on it.
            hit.point=new Vector3(8,32,0);Physics.SharkPropHits=new RaycastHit[] {hit};
            owner.X=12;owner.Y=32;Launch(owner);
            Check(Active(owner).CameraTarget.position.y==48,"Shark spawned on prop top instead of ground below it");
            Call(owner,"ClearSkillPreview");
        }
        owner.X=owner.Y=0;Physics.SharkPropHits=new RaycastHit[0];Physics.SharkWallX=float.PositiveInfinity;
        Physics.SharkTerrainEnabled=false;
        Console.WriteLine("PASS shark Ground-layer props: crate/barrel/doodad passthrough; real wall behind props blocks; spawning ignores prop top");
    }

    private static void CheckElevatorFloor(Aquabro owner)
    {
        foreach (object elevator in new object[] {new Elevator(),new GiantElevator()})
        foreach (bool groundBelow in new bool[] {false,true})
        {
            Reset();Physics.SharkTerrainEnabled=true;Physics.SharkFloor=groundBelow;
            Physics.SharkFloorY=0;Physics.SharkWallX=float.PositiveInfinity;
            Collider roof=new Collider(0,309.5f,true);roof.ParentComponent=elevator;
            Bounds bounds=new Bounds();bounds.min=new Vector3(-24,307,0);bounds.max=new Vector3(24,312,0);roof.bounds=bounds;
            RaycastHit hit=new RaycastHit();hit.collider=roof;hit.point=new Vector3(0,312,0);
            Physics.SharkPropHits=new RaycastHit[] {hit};
            owner.X=0;owner.Y=312;Launch(owner);
            Check(Active(owner).CameraTarget.position.y==360,"Shark did not spawn on elevator roof");
            Call(owner,"ClearSkillPreview");
            foreach (float direction in new float[] {-1f,1f})
            {
                float x=0,y=312;owner.MoveShark(ref x,ref y,direction,.1f);
                Check(Math.Abs(x-direction*9)<.01f && y==312,"Shark stuck on/fell through elevator roof");
                Physics.SharkWallX=direction*16;x=0;y=312;
                owner.MoveShark(ref x,ref y,direction,.3f);
                Check(x==direction*12 && y==312,"Real wall beside elevator ignored");
                Physics.SharkWallX=float.PositiveInfinity;
            }
        }
        owner.X=owner.Y=0;Physics.SharkPropHits=new RaycastHit[0];Physics.SharkFloor=true;
        Physics.SharkTerrainEnabled=false;
        Console.WriteLine("PASS shark elevator roofs: normal/giant; spawn; left/right movement; ground/void below; adjacent walls");
    }

    private static void ShiftElevator(Collider roof, float dx, float dy)
    {
        Vector3 position=roof.transform.position;
        roof.transform.position=new Vector3(position.x+dx,position.y+dy,0);
        Bounds bounds=roof.bounds;
        bounds.min=new Vector3(bounds.min.x+dx,bounds.min.y+dy,0);
        bounds.max=new Vector3(bounds.max.x+dx,bounds.max.y+dy,0);
        bounds.center=new Vector3(bounds.center.x+dx,bounds.center.y+dy,0);
        roof.bounds=bounds;
        RaycastHit hit=new RaycastHit();hit.collider=roof;hit.point=new Vector3(bounds.center.x,bounds.max.y,0);
        Physics.SharkPropHits=new RaycastHit[] {hit};
    }

    private static void CheckElevatorMotion(Aquabro owner)
    {
        foreach (bool giant in new bool[] {false,true})
        foreach (int fps in new int[] {15,30,60,120})
        foreach (float direction in new float[] {-1f,1f})
        {
            Reset();Physics.SharkTerrainEnabled=true;Physics.SharkFloor=true;Physics.SharkFloorY=0;
            Physics.SharkWallX=float.PositiveInfinity;
            Collider roof=new Collider(0,309.5f,true);
            roof.ParentComponent=giant ? (object)new GiantElevator() : new Elevator();
            Bounds bounds=new Bounds();bounds.min=new Vector3(-24,307,0);bounds.max=new Vector3(24,312,0);
            bounds.center=new Vector3(0,309.5f,0);roof.bounds=bounds;ShiftElevator(roof,0,0);
            owner.X=0;owner.Y=312;Launch(owner);Steer(owner,false,false);
            float dt=1f/fps,dx=giant ? 60f*dt : 0f,dy=direction*90f*dt;
            ShiftElevator(roof,dx,dy);
            Call(owner,"TickSkillPreview",0f);
            Check(Position(owner)==0 && Active(owner).CameraTarget.position.y==360,"Paused elevator advanced shark");
            for (int i=0;i<fps/2;i++)
            {
                if (i>0) ShiftElevator(roof,dx,dy);
                Call(owner,"TickSkillPreview",dt);
                Check(Active(owner).SharkControlled && owner.SpecialAmmo==2,"Elevator motion ended/recast shark");
                Check(Math.Abs(Position(owner)-roof.transform.position.x)<.01f &&
                    Math.Abs(Active(owner).CameraTarget.position.y-(roof.bounds.max.y+48))<.01f,
                    "Idle shark lost moving elevator: giant="+giant+", fps="+fps+", direction="+direction);
            }
            ShiftElevator(roof,dx,dy);Steer(owner,false,true);Call(owner,"TickSkillPreview",.05f);
            Check(Math.Abs(Position(owner)-(roof.transform.position.x+4.5f))<.01f &&
                Math.Abs(Active(owner).CameraTarget.position.y-(roof.bounds.max.y+48))<.01f,
                "Steering while elevator moves lost relative movement/support");
            // Steering off the roof detaches; later platform motion must not carry it.
            Steer(owner,false,true);Call(owner,"TickSkillPreview",.5f);
            Check(Active(owner).CameraTarget.position.y==48,"Swimming off elevator did not find lower ground");
            float detachedX=Position(owner);Steer(owner,false,false);ShiftElevator(roof,20,40);
            Call(owner,"TickSkillPreview",dt);
            Check(Math.Abs(Position(owner)-detachedX)<.01f && Active(owner).CameraTarget.position.y==48,
                "Detached shark still follows elevator");
            Call(owner,"ClearSkillPreview");
            // New casts reacquire current support; disabled roofs release it even while idle.
            owner.X=roof.bounds.center.x;owner.Y=roof.bounds.max.y;Launch(owner);Steer(owner,false,false);
            roof.enabled=false;Call(owner,"TickSkillPreview",dt);
            Check(Active(owner).CameraTarget.position.y==48,"Disabled elevator left shark suspended");
            Call(owner,"ClearSkillPreview");roof.enabled=true;
            owner.X=roof.bounds.center.x;owner.Y=roof.bounds.max.y;Launch(owner);Steer(owner,false,false);
            Call(owner,"PressSpecial");float breachY=Active(owner).CameraTarget.position.y;
            ShiftElevator(roof,10,40);Call(owner,"TickSkillPreview",dt);
            Check(Active(owner).CameraTarget.position.y==breachY,"Breaching shark still attached to elevator");
            Call(owner,"ClearSkillPreview");
        }
        owner.X=owner.Y=0;Physics.SharkPropHits=new RaycastHit[0];Physics.SharkTerrainEnabled=false;
        Physics.SharkFloor=true;Physics.SharkFloorY=0;
        Console.WriteLine("PASS shark moving elevators: normal/giant; idle up/down/diagonal; 15/30/60/120 FPS; pause; detach; disabled support; recast; breach");
    }

    private static void CheckSupportFloor(Aquabro owner)
    {
        foreach (float waterline in new float[] {0f,248f})
        foreach (float facing in new float[] {-1f,1f})
        {
            Reset();Collider floor=new Collider(0,waterline-8f,false);
            Bounds bounds=new Bounds();bounds.min=new Vector3(-64f,waterline-16f,0);bounds.max=new Vector3(64f,waterline,0);
            bounds.center=new Vector3(0,waterline-8f,0);floor.bounds=bounds;Physics.Contacts=new Collider[] {floor};
            for (int frame=6;frame<28;frame++) owner.SkillSharkObjects(0,waterline,facing,1,frame,new HashSet<GameObject>());
            Check(floor.enabled && MapController.ObjectHits==0,"Breach destroyed supporting floor");
        }
    }

    private static void CheckObjects(Aquabro owner,Bitmap atlas)
    {
        int bx=-1,by=0,wx=-1,wy=0;
        for (int y=0;y<128;y++) for (int x=0;x<128;x++)
        {
            if (SharkPixelMask.Contains(10,x+.5f,y+.5f,0)) {bx=x;by=y;}
            if (atlas.GetPixel(10*128+x,y).A>0 && !SharkPixelMask.IntersectsRect(10,x-.5f,y-.5f,x+1.5f,y+1.5f,5)) {wx=x;wy=y;}
        }
        Check(bx>=0 && wx>=0,"Need body/water object samples");
        foreach (float facing in new float[] {-1,1})
        {
            Reset();Unit body=AtPixel(bx,by,facing),water=AtPixel(wx,wy,facing);
            Collider prop=new Collider(body.X,body.Y+6,false),splash=new Collider(water.X,water.Y+6,false);
            Physics.Contacts=new Collider[] {prop,splash};
            Doodad doodad=new Doodad(body.X,body.Y+6),immune=new Doodad(body.X,body.Y+6),waterDoodad=new Doodad(water.X,water.Y+6);
            immune.immuneToHeroDamage=true;Map.destroyableDoodads.Add(doodad);Map.destroyableDoodads.Add(immune);Map.destroyableDoodads.Add(waterDoodad);
            HashSet<GameObject> hits=new HashSet<GameObject>();
            owner.SkillSharkObjects(0,0,facing,1,0,hits);Check(MapController.ObjectHits==0 && Map.DoodadHits==0,"Warning destroys objects");
            owner.SkillSharkObjects(0,0,facing,1,10,hits);
            Check(MapController.ObjectHits==1 && !prop.enabled && splash.enabled && Map.DoodadHits==1 && doodad.Damaged && !immune.Damaged && !waterDoodad.Damaged,
                "Body/water object destruction mismatch");
            owner.SkillSharkObjects(0,0,facing,1,10,hits);Check(Map.DoodadHits==1,"Repeated doodad damage");
            owner.SkillSharkObjects(0,0,facing,1,27,new HashSet<GameObject>());Check(Map.DoodadHits==1,"Empty frame destroys objects");
            owner.IsMine=false;owner.SkillSharkObjects(0,0,facing,1,10,new HashSet<GameObject>());Check(Map.DoodadHits==1,"Remote owner destroys objects");owner.IsMine=true;
        }
    }
}

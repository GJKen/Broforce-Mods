// 编译本文件与真实 src/AquabroSkills.cs，使用轻量游戏桩验证技能逻辑。
using System;
using System.Collections.Generic;
using System.Reflection;
using AquabroMod;
using UnityEngine;

public static class SkillSpringTests
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static Unit Target(float x, float centerY)
    {
        Unit unit = new Unit(); unit.X = x; unit.Y = centerY - 6f; unit.playerNum = -1;
        Map.Targets.Add(unit); return unit;
    }
    private static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static void Reset() { Map.Targets.Clear(); Map.Hits.Clear(); Map.TotalHits = 0; Map.DamageObserver=null; MapController.ObjectHits=Map.DoodadHits=0; Map.destroyableDoodads.Clear(); Physics.Contacts=new Collider[0]; }
    private static Vector2 PixelWorld(int px, int py, float scale, float facing)
    { return new Vector2((px + .5f - 64f) * scale * facing, (112f - py - .5f) * scale); }
    public static void Main(string[] args)
    {
        try
        {
            CheckAcid();
            PixelMaskTests.Run();
            CheckCastStart();
            CheckSpringTerrainPlacement();
            using (System.Drawing.Bitmap atlas = new System.Drawing.Bitmap(args[0])) Run(atlas);
            using (System.Drawing.Bitmap atlas = new System.Drawing.Bitmap(args[1])) CheckTidalWave(atlas);
            using (System.Drawing.Bitmap atlas = new System.Drawing.Bitmap(args[2])) SkillSharkTests.Run(atlas, args[2]);
            using (System.Drawing.Bitmap atlas = new System.Drawing.Bitmap(args[3])) SkillMaelstromTests.Run(atlas);
            SkillDamageTargetTests.Run();
            BossSkillDamageTests.Run();
            SkillOcclusionTests.Run();
            CheckSkillHud();
        }
        catch (Exception error) { Console.WriteLine("FAIL: " + error); Environment.Exit(1); }
    }
    private static void CheckAcid()
    {
        Check(!Aquabro.RollsCorrosion(0,0), "Clean water corrodes");
        Check(Aquabro.RollsCorrosion(1,.49999) && !Aquabro.RollsCorrosion(1,.5), "Stage one probability boundary");
        Check(Aquabro.RollsCorrosion(2,.99999), "Stage two probability");
        Aquabro owner=new Aquabro();
        WaterProjectileBarrier water=new WaterProjectileBarrier(owner,0,0,0,1,1);
        object blob=new object();water.Absorb(blob);water.Absorb(blob);
        Check(water.AcidStage==1,"Same acid source counted twice");
        water.Absorb(new object());water.Absorb(new object());Check(water.AcidStage==2,"Stage cap");
        water.Dispose();
        for (int kind=0;kind<4;kind++)
        {
            water=new WaterProjectileBarrier(owner,kind,0,0,1,1);water.SetFrame(4);
            SkillContactMask mask=new SkillContactMask(kind,0,0,1,1,4);
            float px=0,py=0;bool found=false;
            for(int yy=1;yy<112&&!found;yy++)for(int xx=-64;xx<64;xx++)
                if(mask.Contains(xx,yy,0)){px=xx;py=yy;found=true;break;}
            Check(found,"Missing water test contact");
            Projectile acid=new Projectile();acid.damageType=DamageType.Acid;acid.X=px;acid.Y=py;
            if (kind == 1)
            {
                Check(!WaterProjectileBarrier.TryBlock(acid,.02f),"Shark absorbs acid");
                water.Absorb(new object());water.AbsorbPools();
                Check(water.AcidStage==0,"Shark contaminated");water.Dispose();continue;
            }
            Check(!WaterProjectileBarrier.TryBlock(acid,0) && water.AcidStage==0,"Paused acid absorption");
            Check(WaterProjectileBarrier.TryBlock(acid,.02f) && water.AcidStage==1,"Acid projectile not absorbed kind "+kind);
            DoodadAcidPool pool=new DoodadAcidPool();pool.X=px;pool.Y=py;DoodadBloodPool.bloodDoodads.Add(pool);
            water.AbsorbPools();water.AbsorbPools();Check(water.AcidStage==2,"Pool stage/dedup");
            water.Dispose();DoodadBloodPool.bloodDoodads.Clear();
        }
        float color=0;
        color=AquabroSkillVisual.AdvanceAcidColor(color,2,.15f);
        Check(Math.Abs(color-.5f)<.001f,"Gradient skips intermediate color");
        Check(AquabroSkillVisual.AdvanceAcidColor(color,2,0)==color,"Paused gradient advances");
        color=AquabroSkillVisual.AdvanceAcidColor(color,2,.15f);
        Check(Math.Abs(color-1)<.001f,"Gradient skips stage one");
        color=AquabroSkillVisual.AdvanceAcidColor(color,2,.15f);
        Check(Math.Abs(color-1.5f)<.001f,"Second gradient jumps");
        Check(AquabroSkillVisual.AdvanceAcidColor(color,2,1)==2,"Gradient overshoots");
        string fixture=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"Aquabro-acid-png-"+Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(fixture);owner.Info.path=fixture;
        for(int stage=0;stage<3;stage++)
        foreach (int kind in new int[] {0,3})
        {
            int frames=kind==3?28:16;
            using(System.Drawing.Bitmap png=new System.Drawing.Bitmap(frames*128,128))
            {
                System.Drawing.Color source=stage==0?System.Drawing.Color.FromArgb(137,0,0,200):stage==1?System.Drawing.Color.FromArgb(137,100,200,0):System.Drawing.Color.FromArgb(137,200,240,0);
                for(int y=0;y<128;y++)for(int x=0;x<frames*128;x++)png.SetPixel(x,y,source);
                for(int frame=0;frame<frames;frame++)png.SetPixel(frame*128+1,127,System.Drawing.Color.Transparent);
                png.Save(System.IO.Path.Combine(fixture,"skill-"+(kind==3?"abyssal-spring":"tidal-wave")+(stage==0?"":"-acid-"+stage)+".png"),System.Drawing.Imaging.ImageFormat.Png);
            }
        }
        AquabroSkillVisual visual=new AquabroSkillVisual(owner,0,0,0,1,2,0,1);
        WaterProjectileBarrier visualWater=(WaterProjectileBarrier)typeof(AquabroSkillVisual).GetField("waterBarrier",Private).GetValue(visual);
        visualWater.Absorb(new object());
        visual.Tick(.15f);
        Texture2D mixed=(Texture2D)typeof(AquabroSkillVisual).GetField("acidTexture",Private).GetValue(visual);
        Color32 sample=mixed.GetPixels32()[0];
        Check(sample.r==50 && sample.g==100 && sample.b==100 && sample.a==137,"PNG frame gradient or alpha changed; render-cache poison must be ignored: "+sample.r+","+sample.g+","+sample.b+","+sample.a);
        Check(mixed.GetPixels32()[1].a==0,"Transparent PNG pixel became opaque gray");
        visualWater.Absorb(new object());visual.Tick(.15f);
        sample=mixed.GetPixels32()[0];Check(sample.r==100 && sample.g==200,"Continuous upgrade did not pass stage one");
        visual.Tick(.3f);sample=mixed.GetPixels32()[0];Check(sample.r==200 && sample.g==240,"Stage two gradient endpoint");
        visual.Dispose();
        CheckSpringSharedAcid(fixture);
        System.IO.Directory.Delete(fixture,true);owner.Info.path="";
        AquabroSkillVisual sharkVisual=new AquabroSkillVisual(owner,1,0,0,1,4,0,1);
        Check(typeof(AquabroSkillVisual).GetField("waterBarrier",Private).GetValue(sharkVisual)==null,"Shark still creates water barrier");
        sharkVisual.Tick(.1f);Check(typeof(AquabroSkillVisual).GetField("acidTexture",Private).GetValue(sharkVisual)==null,"Shark creates acid texture");sharkVisual.Dispose();
        CheckNativeAcid();
        CheckSlimeSources();
        Console.WriteLine("PASS acid: three skills + shark excluded; PNG decoding; alpha; smooth gradient; 50%/100% boundaries; source dedup; projectile/pool contact; native acid coating replaces deferred damage");
    }
    private static void CheckSlimeSources()
    {
        MethodInfo register=typeof(WaterProjectileBarrier).GetMethod("RegisterSlime",Private|BindingFlags.Static);
        FieldInfo tracked=typeof(WaterProjectileBarrier).GetField("AcidSlime",Private|BindingFlags.Static);
        Aquabro owner=new Aquabro();
        foreach(int kind in new int[]{0,2,3})
        foreach(float facing in new float[]{-1,1})
        foreach(float scale in new float[]{.65f,1f})
        foreach(DirectionEnum direction in new DirectionEnum[]{DirectionEnum.Up,DirectionEnum.Down,DirectionEnum.Left,DirectionEnum.Right})
        {
            int frame=kind==2?22:4;
            SkillContactMask mask=new SkillContactMask(kind,0,0,facing,scale,frame);
            Vector2 point=new Vector2();bool found=false;
            for(int py=10;py<90&&!found;py++)for(int px=16;px<48;px++)
                if(mask.Contains(px*facing*scale,py*scale,0)){point=new Vector2(px*facing*scale,py*scale);found=true;break;}
            Check(found,"No slime contact fixture");
            float nx=direction==DirectionEnum.Right?1:direction==DirectionEnum.Left?-1:0;
            float ny=direction==DirectionEnum.Up?1:direction==DirectionEnum.Down?-1:0;
            SlimeGroundCover slime=new SlimeGroundCover();slime.direction=direction;
            slime.transform.position=new Vector3(point.x-nx*8,point.y-ny*8,0);
            if(register!=null)register.Invoke(null,new object[]{slime});
            WaterProjectileBarrier water=new WaterProjectileBarrier(owner,kind,0,0,facing,scale);water.SetFrame(frame);
            try
            {
                Collider wall=new Collider(point.x*.5f,50,true);
                wall.bounds.min=new Vector3(point.x*.5f-.5f,-100,0);wall.bounds.max=new Vector3(point.x*.5f+.5f,200,0);
                Physics.SceneTerrain=new Collider[]{wall};
                water.AbsorbPools();Check(water.AcidStage==0,"Death slime contaminated through wall");
                wall.enabled=false;slime.slimeColor=BloodColor.Red;
                water.AbsorbPools();Check(water.AcidStage==0,"Red blood contaminated water");
                slime.slimeColor=BloodColor.Green;slime.damage=0;
                water.AbsorbPools();Check(water.AcidStage==0,"Harmless slime contaminated water");
                slime.damage=15;slime.gameObject.SetActive(false);
                water.AbsorbPools();Check(water.AcidStage==0,"Inactive slime contaminated water");
                slime.gameObject.SetActive(true);owner.IsMine=false;
                water.AbsorbPools();Check(water.AcidStage==0,"Remote slime contamination");owner.IsMine=true;
                water.SetFrame(-1);water.AbsorbPools();Check(water.AcidStage==0,"Hidden water absorbed slime");
                water.SetFrame(frame);water.AbsorbPools();water.AbsorbPools();
                Check(water.AcidStage==1,"Death slime was not absorbed once kind="+kind+" direction="+direction);
                SlimeGroundCover second=new SlimeGroundCover();second.direction=direction;second.transform.position=slime.transform.position;
                if(register!=null)register.Invoke(null,new object[]{second});
                water.AbsorbPools();Check(water.AcidStage==2,"Second death slime did not upgrade water");
                Check(slime.gameObject.activeInHierarchy&&slime.damage==15,"Absorption consumed death slime");
            }
            finally
            {
                water.Dispose();owner.IsMine=true;Physics.SceneTerrain=new Collider[0];
                if(tracked!=null)((System.Collections.IList)tracked.GetValue(null)).Clear();
            }
        }
        Console.WriteLine("PASS death slime acid: three skills, four surfaces, both facings/scales; wall removal, source dedup, two stages, colour/damage/active/owner/visible filters; residue preserved");
    }
    private static void CheckNativeAcid()
    {
        MethodInfo damage = typeof(Aquabro).GetMethod("DamageSkillUnit", Private);
        foreach (Unit target in new Unit[] { new Mook(), new MookSuicide(), new MookSuicideUndead(), new MookRiotShield(), new MookBigGuy(), new Alien(), new AlienMosquito(), new HellLostSoul(), new Tank() })
        {
            Reset(); Aquabro owner = new Aquabro(); owner.CurrentAcidStage = 2; target.playerNum = -1;
            if(target is MookRiotShield)((MookRiotShield)target).Facing=-1;
            damage.Invoke(owner, new object[] { target, 26, 420f, 300f, 1, true, 0f, 0f });
            bool compatible = target is Mook && !(target is AlienMosquito) || target is HellLostSoul;
            Check(Map.LastDamageType == (compatible ? DamageType.Acid : DamageType.Explosion), "Native acid eligibility " + target.GetType());
            Check(Map.TotalHits == 1 && Map.Hits[target] == 26, "Native acid split/duplicated damage");
            Check(Map.LastUpForce == (compatible ? 0f : 300f), "Native acid launch/fallback " + target.GetType());
            if (compatible) Check(target.HasBeenCoveredInAcid(), "Native coating not entered");
        }
        foreach (bool resistant in new bool[] { false, true })
        {
            Reset(); Aquabro owner = new Aquabro(); owner.CurrentAcidStage = resistant ? 2 : 0;
            Mook target = new Mook(); target.playerNum = -1; target.canBeCoveredInAcid = !resistant;
            damage.Invoke(owner, new object[] { target, 4, 70f, 650f, 1, true, 0f, 0f });
            Check(Map.LastDamageType == DamageType.Explosion && !target.HasBeenCoveredInAcid() && Map.LastUpForce == 650f,
                "Clean/resistant target lost original damage");
        }
        foreach (int kind in new int[] { 0, 2, 3 })
        foreach (bool relay in new bool[] { false, true })
        foreach (Mook nativeVictim in new Mook[] {new Mook(),new MookSuicide(),new MookSuicideUndead(),new MookRiotShield()})
        foreach (float enemyFacing in new float[] {-1,1})
        {
            Reset(); Physics.SharkTerrainEnabled=false;
            Aquabro owner = new Aquabro(); owner.CurrentAcidStage = 2;
            SkillContactMask mask = new SkillContactMask(kind, 0, 0, 1, 1, kind == 0 ? 4 : 10);
            float px=0,py=0; bool found=false;
            for(int yy=6; yy<100 && !found; yy++) for(int xx=-50; xx<50; xx++)
                if(mask.Contains(xx,yy,0)){px=xx;py=yy;found=true;break;}
            Check(found,"Native acid mask fixture");
            Mook victim=nativeVictim; victim.hasBeenCoverInAcid=false;
            if(victim is MookRiotShield)((MookRiotShield)victim).Facing=(int)enemyFacing;
            victim.playerNum=-1; victim.X=px;victim.Y=py-6;Map.Targets.Add(victim);
            if(relay)
            {
                Collider contact=new Collider(px,py,true);contact.Relay=new DamageRelay();contact.Relay.unit=victim;
                Physics.Contacts=new Collider[]{contact};
            }
            if(kind==0) owner.SkillWaveContact(0,0,1,1,4,new HashSet<object>(),new HashSet<GameObject>(),new WaveDamageState(),0);
            if(kind==2) owner.SkillMaelstromPullAtTime(0,0,1,1,10,.02f,true,new HashSet<object>(),.4f);
            if(kind==3) owner.SkillSpringBlast(0,0,1,1,10,new SpringDamageState(),0);
            Check(victim.HasBeenCoveredInAcid() && Map.LastDamageType==DamageType.Acid && Map.LastUpForce==0,
                "Poison skill did not coat without launch kind="+kind+" relay="+relay);
            Check(Map.TotalHits==1,"Body/relay duplicate native acid");
            Check(victim.KnockCalls==0,"Vortex pulled newly coated victim");
            // Once coated, stage-one hits keep Acid instead of blowing up the skeleton.
            owner.CurrentAcidStage=1;
            for(int hit=0;hit<12;hit++)damage.Invoke(owner,new object[]{victim,3,220f,500f,1,true,0f,0f});
            Check(Map.LastDamageType==DamageType.Acid && Map.LastUpForce==0,"Coated enemy launched by subsequent poison hit");
        }
        // An Acid-immune proxy must retain its native gate and fall back to water damage.
        Reset(); Aquabro proxyOwner=new Aquabro();proxyOwner.CurrentAcidStage=2;
        Mook proxied=new Mook();proxied.playerNum=-1;proxied.X=0;proxied.Y=20;Map.Targets.Add(proxied);
        Collider proxy=new Collider(0,26,true);proxy.Relay=new DamageRelay();proxy.Relay.unit=proxied;proxy.Relay.immunity=DamageType.Acid;
        Physics.Contacts=new Collider[]{proxy};
        proxyOwner.SkillSpringBlast(0,0,1,1,10,new SpringDamageState(),0);
        Check(!proxied.HasBeenCoveredInAcid() && Map.LastDamageType==DamageType.Explosion,"Acid immunity bypassed");
        Reset();
        Console.WriteLine("PASS native acid: wave/vortex/spring body+proxy; coating; no launch/pull; coated follow-up; heavy/lost-soul; clean/Alien/mosquito/vehicle/resistant fallback; proxy immunity; no deferred damage");
    }

    private static void CheckSpringSharedAcid(string fixture)
    {
        Reset();
        Aquabro owner=new Aquabro();owner.Info.path=fixture;
        typeof(Aquabro).GetField("castingSkill",Private).SetValue(owner,3);
        typeof(Aquabro).GetMethod("UseSpecial",Private).Invoke(owner,null);
        var columns=(List<AquabroSkillVisual>)typeof(Aquabro).GetField("skillVisuals",Private).GetValue(owner);
        Check(columns.Count==7,"Shared acid fixture missing columns");
        FieldInfo waterField=typeof(AquabroSkillVisual).GetField("waterBarrier",Private);
        WaterProjectileBarrier center=(WaterProjectileBarrier)waterField.GetValue(columns[3]);
        MethodInfo tick=typeof(Aquabro).GetMethod("TickSkillPreview",Private);
        tick.Invoke(owner,new object[]{.051f});
        // A real intercepted projectile contaminates the entire cast, including
        // outer columns that have not appeared yet.
        SkillContactMask mask=new SkillContactMask(3,owner.X,owner.Y,1,.65f,1);
        Projectile shot=new Projectile();shot.damageType=DamageType.Acid;
        bool found=false;
        for(int yy=1;yy<74&&!found;yy++)for(int xx=-41;xx<42;xx++)
            if(mask.Contains(xx,yy,0)){shot.X=xx;shot.Y=yy;found=true;break;}
        Check(found && WaterProjectileBarrier.TryBlock(shot,.02f),"Shared acid projectile missed");
        foreach(AquabroSkillVisual column in columns)
            Check(((WaterProjectileBarrier)waterField.GetValue(column)).AcidStage==1,"One contaminated column left another clean");
        ((WaterProjectileBarrier)waterField.GetValue(columns[0])).Absorb(shot);
        Check(center.AcidStage==1,"One projectile counted across columns twice");
        tick.Invoke(owner,new object[]{0f});
        Check((float)typeof(AquabroSkillVisual).GetField("acidColorLevel",Private).GetValue(columns[3])==0f,"Paused shared color advanced");
        for(int i=0;i<9;i++) tick.Invoke(owner,new object[]{.05f});
        foreach(AquabroSkillVisual column in columns)
            Check(Math.Abs((float)typeof(AquabroSkillVisual).GetField("acidColorLevel",Private).GetValue(column)-1f)<.001f,
                "Delayed column did not inherit shared color");
        object poolSource=new object();
        foreach(AquabroSkillVisual column in columns) ((WaterProjectileBarrier)waterField.GetValue(column)).Absorb(poolSource);
        Check(center.AcidStage==2,"New source failed to upgrade entire cast");
        tick.Invoke(owner,new object[]{.15f});
        foreach(AquabroSkillVisual column in columns)
            Check(Math.Abs((float)typeof(AquabroSkillVisual).GetField("acidColorLevel",Private).GetValue(column)-1.5f)<.001f,
                "Seven columns accelerated shared stage-two gradient");
        tick.Invoke(owner,new object[]{.15f});
        foreach(AquabroSkillVisual column in columns)
            Check(Math.Abs((float)typeof(AquabroSkillVisual).GetField("acidColorLevel",Private).GetValue(column)-2f)<.001f,
                "Shared stage-two gradient endpoint");
        owner.specialResolved=false;
        typeof(Aquabro).GetMethod("UseSpecial",Private).Invoke(owner,null);
        Check(columns.Count==14,"Second cast failed to create an independent column group");
        for(int i=7;i<columns.Count;i++)
            Check(((WaterProjectileBarrier)waterField.GetValue(columns[i])).AcidStage==0,"New cast inherited previous cast pollution");
        ((WaterProjectileBarrier)waterField.GetValue(columns[7])).Absorb(poolSource);
        for(int i=7;i<columns.Count;i++)
            Check(((WaterProjectileBarrier)waterField.GetValue(columns[i])).AcidStage==1,"New cast reused previous cast source dedup");
        typeof(Aquabro).GetMethod("ClearSkillPreview",Private).Invoke(owner,null);

        // Left pool must affect a right-column hit before reverse-order damage.
        Reset();owner=new Aquabro();owner.Info.path=fixture;
        columns=(List<AquabroSkillVisual>)typeof(Aquabro).GetField("skillVisuals",Private).GetValue(owner);
        SpringDamageState shared=new SpringDamageState();shared.Acid.Absorb(new object());
        AquabroSkillVisual left=new AquabroSkillVisual(owner,3,-1000,0,-1,1.4f,0,1,null,shared);
        AquabroSkillVisual right=new AquabroSkillVisual(owner,3,1000,0,1,1.4f,0,1,null,shared);
        columns.Add(left);columns.Add(right);
        typeof(AquabroSkillVisual).GetField("elapsed",Private).SetValue(left,.499f);
        typeof(AquabroSkillVisual).GetField("elapsed",Private).SetValue(right,.499f);
        mask=new SkillContactMask(3,0,0,1,1,10);found=false;float px=0,py=0;
        for(int yy=1;yy<112&&!found;yy++)for(int xx=-64;xx<64;xx++)
            if(mask.Contains(xx,yy,0)){px=xx;py=yy;found=true;break;}
        Check(found,"Shared acid pool fixture missing contact");
        DoodadAcidPool pool=new DoodadAcidPool();pool.X=-1000-px;pool.Y=py;
        DoodadBloodPool.bloodDoodads.Add(pool);
        Mook victim=new Mook();victim.playerNum=-1;victim.X=1000+px;victim.Y=py-6;Map.Targets.Add(victim);int damage=0;
        Map.DamageObserver=delegate(Unit unit,int amount){if(unit==victim)damage+=amount;};
        tick.Invoke(owner,new object[]{.002f});
        Check(((WaterProjectileBarrier)waterField.GetValue(right)).AcidStage==2,"Left pool failed to upgrade right column in same tick");
        Check(damage==4 && victim.HasBeenCoveredInAcid() && Map.LastDamageType==DamageType.Acid && Map.LastUpForce==0,
            "Left pool did not apply native acid on right-side hit in same tick");
        DoodadBloodPool.bloodDoodads.Clear();
        typeof(Aquabro).GetMethod("ClearSkillPreview",Private).Invoke(owner,null);Reset();
        // The same pool touching several columns only grants one stage.
        shared=new SpringDamageState();
        WaterProjectileBarrier a=new WaterProjectileBarrier(owner,3,0,0,1,1,shared.Acid);
        WaterProjectileBarrier b=new WaterProjectileBarrier(owner,3,0,0,1,1,shared.Acid);
        a.SetFrame(10);b.SetFrame(10);pool=new DoodadAcidPool();pool.X=px;pool.Y=py;
        DoodadBloodPool.bloodDoodads.Add(pool);a.AbsorbPools();b.AbsorbPools();
        Check(a.AcidStage==1 && b.AcidStage==1,"One acid pool upgraded shared cast twice");
        a.SetFrame(18);b.SetFrame(18);pool=new DoodadAcidPool();pool.X=px;pool.Y=py;
        DoodadBloodPool.bloodDoodads.Add(pool);a.AbsorbPools();b.AbsorbPools();
        Check(a.AcidStage==1,"Dissolving columns absorbed a new pool");
        a.Dispose();b.Dispose();DoodadBloodPool.bloodDoodads.Clear();Reset();
        Console.WriteLine("PASS spring shared acid: intercepted projectile; seven columns; source dedup; delayed color; pause; fresh next cast; same-tick left pool/right native acid hit; dissolve inactive");
    }
    private static void CheckCastStart()
    {
        for (int skill = 0; skill < 4; skill++)
        {
            Aquabro owner = new Aquabro();
            while (owner.SelectedSkill != skill) owner.SetGestureAnimation(GestureElement.Gestures.Flex);
            owner.frame = 5;
            owner.TestBodyCell = 0;
            owner.TestGunVisible = true;
            owner.TestGunPosition = new Vector2(9f, -2f);
            int ammo = owner.SpecialAmmo;
            typeof(Aquabro).GetMethod("PressSpecial", Private).Invoke(owner, null);
            Check(owner.usingSpecial && owner.frame == 0, "Cast did not start at first frame");
            Check(owner.TestBodyCell == 384 + skill * 8 && !owner.TestGunVisible,
                "Cast start left idle body and displaced independent arms visible before animation tick");
            Check(owner.SpecialAmmo == ammo, "Cast start released skill before frame 3");
        }
        Aquabro reset = new Aquabro();
        while (reset.SelectedSkill != 0) reset.SetGestureAnimation(GestureElement.Gestures.Flex);
        reset.SpecialAmmo = 0;
        reset.TestBodyCell = 0;
        reset.TestGunVisible = true;
        typeof(Aquabro).GetMethod("PressSpecial", Private).Invoke(reset, null);
        Check(!reset.usingSpecial && reset.TestBodyCell == 0 && reset.TestGunVisible,
            "Rejected cast changed the current pose");
        Console.WriteLine("PASS cast start: all four skills show first cast frame and hide independent arms immediately; no early release; empty ammo preserves pose");
    }
    private static void CheckSkillSwitchPreservesEffects()
    {
        Reset(); Physics.SharkTerrainEnabled=false;
        Aquabro owner = new Aquabro();
        while (owner.SelectedSkill != 0) owner.SetGestureAnimation(GestureElement.Gestures.Flex);
        owner.SpecialAmmo = 2;
        var effects = (List<AquabroSkillVisual>)typeof(Aquabro).GetField("skillVisuals", Private).GetValue(owner);
        var wave = new AquabroSkillVisual(owner, 0, 0, 0, 1, 2, 0, .85f); effects.Add(wave);
        owner.CurrentAcidStage = 2;
        Mook coated = new Mook(); coated.playerNum = -1;
        typeof(Aquabro).GetMethod("DamageSkillUnit", Private).Invoke(owner, new object[] { coated, 4, 70f, 650f, 1, true, 0f, 0f });
        owner.SetGestureAnimation(GestureElement.Gestures.Flex);
        Check(owner.SelectedSkill == 1 && owner.SpecialAmmo == 2 && effects.Contains(wave),
            "Switch refunded ammo or removed existing wave");
        Check(coated.HasBeenCoveredInAcid(), "Switch removed native acid state");
        var shark = new AquabroSkillVisual(owner, 1, 0, 0, 1, 12, 0, 1); effects.Add(shark);
        typeof(Aquabro).GetField("activeShark", Private).SetValue(owner, shark);
        owner.SetGestureAnimation(GestureElement.Gestures.Flex);
        Check(owner.SelectedSkill == 1 && owner.SpecialAmmo == 2, "Remote steering allowed gesture switch");
        shark.BreachShark(); owner.SetGestureAnimation(GestureElement.Gestures.Flex);
        Check(owner.SelectedSkill == 2 && effects.Contains(shark) && owner.SpecialAmmo == 2,
            "Breach prevented switching or removed autonomous shark");
        typeof(Aquabro).GetMethod("PressSpecial", Private).Invoke(owner, null);
        typeof(Aquabro).GetMethod("UseSpecial", Private).Invoke(owner, null);
        Check(owner.SpecialAmmo == 1 && effects.Count == 3 && effects.Contains(wave) && effects.Contains(shark),
            "New skill during breach failed or reset existing effects");
        typeof(Aquabro).GetMethod("ClearSkillPreview", Private).Invoke(owner, null);
        Aquabro empty = new Aquabro(); empty.SpecialAmmo = 0;
        for (int i = 0; i < 4; i++) empty.SetGestureAnimation(GestureElement.Gestures.Flex);
        Check(empty.SpecialAmmo == 0, "Empty skill cycle refilled ammunition");
        while (empty.SelectedSkill != 0) empty.SetGestureAnimation(GestureElement.Gestures.Flex);
        Console.WriteLine("PASS switching: shared ammo unchanged; wave/shark and native acid retained; controlled shark blocks switching; breach allows another skill");
    }

    private static void Run(System.Drawing.Bitmap atlas)
    {
        CheckSkillSwitchPreservesEffects();
        Aquabro owner = new Aquabro();
        for (int i = 0; i < 3; i++) owner.SetGestureAnimation(GestureElement.Gestures.Flex);
        Check(owner.SelectedSkill == 3, "Spring selection missing");
        owner.SetGestureAnimation(GestureElement.Gestures.Flex);
        Check(owner.SelectedSkill == 0, "Four-skill cycle does not wrap");

        // Compare the generated mask with the independently decoded exported PNG.
        // Exercise every frame and the exact five game-pixel boundary at all scales/facings.
        for (int f = 0; f < 28; f++)
        {
            int right = -1, rightY = 0;
            for (int py = 0; py < 128; py++) for (int px = 0; px < 128; px++)
            {
                int alpha = atlas.GetPixel(f * 128 + px, py).A;
                Check(alpha == 0 || alpha == 255, "Transparent ghost in source");
                Check(SpringPixelMask.Mask.Contains(f, px + .5f, py + .5f, 0) == (alpha > 0), "Mask/PNG mismatch");
                if (alpha > 0 && px > right) { right = px; rightY = py; }
            }
            if (right < 0) { Check(f == 27, "Unexpected empty water frame"); continue; }
            foreach (float scale in new float[] { .65f, .9f, 1.1f })
            foreach (float facing in new float[] { -1f, 1f })
            {
                Reset();
                float edgeX = (right + 1 - 64) * scale * facing;
                float cy = (112f - rightY - .5f) * scale;
                Unit near = Target(edgeX + facing * 4.999f, cy);
                Unit far = Target(edgeX + facing * 5.01f, cy);
                owner.SkillSpringBlast(0, 0, facing, scale, f, new SpringDamageState(), 0f);
                Check(f < 18 ? Map.TotalHits == 1 && Map.Hits.ContainsKey(near) && !Map.Hits.ContainsKey(far) : Map.TotalHits == 0,
                    "Damage phase/boundary/scale/mirror mismatch frame " + f);
            }
            Check(SpringPixelMask.Mask.Contains(f, right + 6f, rightY + .5f, 5f), "Inclusive exact boundary failed");
        }
        Check(!SpringPixelMask.Mask.Contains(27, 64, 64, 200), "Final frame has lingering damage");
        Check(!SpringPixelMask.Mask.Contains(0, 64, 20, 5), "Startup damages empty upper column");

        Reset();
        int sampleX = 0, sampleY = 0;
        for (int py = 15; py < 95; py++) for (int px = 32; px < 96; px++)
            if (atlas.GetPixel(10 * 128 + px, py).A > 0) { sampleX = px; sampleY = py; }
        Vector2 point = PixelWorld(sampleX, sampleY, .65f, 1);
        Unit center = Target(point.x, point.y), late = Target(500, point.y);
        Unit friendly = Target(point.x, point.y); friendly.playerNum = owner.playerNum;
        Unit immune = Target(point.x, point.y); immune.invulnerable = true;
        Unit dead = Target(point.x, point.y); dead.health = 0;
        SpringDamageState hits = new SpringDamageState();
        owner.SkillSpringBlast(0, 0, 1, .65f, 10, hits, 0f);
        Check(Map.TotalHits == 2 && Map.Hits[center] == 4 && Map.Hits[dead] == 4 && Map.LastUpForce == 650, "Damage/corpse/filter failed");
        owner.SkillSpringBlast(0, 0, 1, .65f, 10, hits, 0f);
        Check(Map.TotalHits == 2, "Repeated damage");
        late.X = point.x;
        owner.SkillSpringBlast(0, 0, 1, .65f, 10, hits, 0f);
        Check(Map.TotalHits == 3 && Map.Hits.ContainsKey(late), "Late entry missed");
        Target(point.x + 2, point.y); Physics.Blocked = true;
        owner.SkillSpringBlast(0, 0, 1, .65f, 10, hits, 0f);
        Check(Map.TotalHits == 3, "Damage through wall");
        Physics.Blocked = false; owner.IsMine = false;
        owner.SkillSpringBlast(0, 0, 1, .65f, 10, hits, 0f);
        Check(Map.TotalHits == 3, "Remote owner applied damage"); owner.IsMine = true;

        Reset(); Target(point.x, point.y);
        hits = new SpringDamageState();
        AquabroSkillVisual effect = new AquabroSkillVisual(owner, 3, 0, 0, 1, 1.4f, .14f, .65f, null, hits);
        Check(effect.Tick(.1f) && Map.TotalHits == 0, "Delayed column damages before appearing");
        effect.Tick(.55f);
        Check(Map.TotalHits == 1 && SpriteSM.LastFrame == 10, "Visible frame and damage disagree");
        AquabroSkillVisual overlap = new AquabroSkillVisual(owner, 3, 0, 0, 1, 1.4f, 0, .65f, null, hits);
        overlap.Tick(.51f); Check(Map.TotalHits == 1, "Overlapping columns multiply cast damage");
        Reset();
        effect.Tick(.85f); Target(0, 10); effect.Tick(.02f);
        Check(SpriteSM.LastFrame == 27 && Map.TotalHits == 0, "Empty ending frame damages");
        Check(!effect.Tick(.1f), "Expired visual alive"); effect.Dispose(); overlap.Dispose();

        // Real release method: center column at feet, three on each side, staggered.
        Reset(); owner.X = 230; owner.Y = 80;
        typeof(Aquabro).GetField("castingSkill", Private).SetValue(owner, 3);
        typeof(Aquabro).GetMethod("UseSpecial", Private).Invoke(owner, null);
        List<AquabroSkillVisual> columns = (List<AquabroSkillVisual>)typeof(Aquabro).GetField("skillVisuals", Private).GetValue(owner);
        Check(columns.Count == 7, "Expected seven columns");
        object shared = null;
        for (int i = 0; i < 7; i++)
        {
            int offset = i - 3;
            Check((int)typeof(AquabroSkillVisual).GetField("kind", Private).GetValue(columns[i]) == 3, "Wrong asset");
            float x = (float)typeof(AquabroSkillVisual).GetField("x", Private).GetValue(columns[i]);
            float y = (float)typeof(AquabroSkillVisual).GetField("y", Private).GetValue(columns[i]);
            float elapsed = (float)typeof(AquabroSkillVisual).GetField("elapsed", Private).GetValue(columns[i]);
            Check(x == owner.X + offset * 30 && y == owner.Y, "Columns not rooted at character feet");
            Check(Math.Abs(elapsed + Math.Abs(offset) * .14f) < .0001f, "Wrong eruption order");
            object columnHits = typeof(AquabroSkillVisual).GetField("springDamage", Private).GetValue(columns[i]);
            if (shared == null) shared = columnHits;
            Check(System.Object.ReferenceEquals(shared, columnHits), "Cast does not share hit history");
        }
        typeof(Aquabro).GetMethod("ClearSkillPreview", Private).Invoke(owner, null);
        owner.specialResolved = false; owner.actionState = ActionState.Jumping;
        owner.yI = 75f;
        typeof(Aquabro).GetMethod("UseSpecial", Private).Invoke(owner, null);
        Check(columns.Count == 7 && owner.yI == 75f && owner.Y == 80,
            "Air spring must release immediately without forced descent or position change");
        owner.actionState = ActionState.Idle;
        typeof(Aquabro).GetMethod("TickSkillPreview", Private).Invoke(owner, new object[] { .01f });
        Check(columns.Count == 7, "Landing duplicated spring release");
        typeof(Aquabro).GetMethod("ClearSkillPreview", Private).Invoke(owner, null);
        CheckTraversalSettlement();
        CheckRopeSettlement();
        CheckDissolve(atlas);
        CheckBarriers(owner);
        Console.WriteLine("PASS: four-skill cycle/HUD; 458752 mask pixels match PNG; every-frame 5px boundary at three scales/two facings; water gaps and empty end; filters/walls/ownership; delayed animation; shared periodic damage budget; seven columns; immediate air cast without forced descent");
    }

    private static void CheckSkillHud()
    {
        HeroController.players=new Player[4];
        Aquabro first=new Aquabro(),second=new Aquabro();second.playerNum=1;
        HeroController.players[0]=new Player {character=first,hud=new PlayerHUD()};
        HeroController.players[1]=new Player {character=second,hud=new PlayerHUD()};
        foreach (Aquabro owner in new Aquabro[] {first,second})
        {
            for(int i=0;i<4;i++)
            {
                owner.SetGestureAnimation(GestureElement.Gestures.Flex);
                string expected="special-"+Aquabro.SkillAssets[owner.SelectedSkill]+".png";
                foreach(var icon in HeroController.players[owner.playerNum].hud.grenadeIcons)
                    Check(icon.meshRender.sharedMaterial.name==expected,"HUD failed to follow selected skill/player");
                Check(owner.CurrentSpecialMaterials.Count==1 && owner.CurrentSpecialMaterials[0].name==expected,
                    "BroMaker later HUD refresh would restore wrong icon");
                Check(HeroController.players[owner.playerNum].hud.grenadeIcons[0].TestOffset.x==(owner.playerNum==0?3f:-3f),
                    "HUD avatar gap did not mirror with player side");
            }
        }
        Material other=second.CurrentSpecialMaterials[0];
        first.SetGestureAnimation(GestureElement.Gestures.Flex);
        Check(second.CurrentSpecialMaterials[0]==other,"Player one's switch changed player two's icon");
        first.SpecialAmmo=0;var saved=first.CurrentSpecialMaterials;
        var icon0=HeroController.players[0].hud.grenadeIcons[0];icon0.meshRender.enabled=false;
        typeof(Aquabro).GetMethod("RefreshSkillHud",Private).Invoke(first,null);
        Check(first.SpecialAmmo==0 && !icon0.meshRender.enabled && first.CurrentSpecialMaterials==saved,
            "HUD refresh changed ammo/visibility or recreated cached material list");
        HeroController.players[0].hud=new PlayerHUD();
        typeof(Aquabro).GetMethod("RefreshSkillHud",Private).Invoke(first,null);
        Check(HeroController.players[0].hud.grenadeIcons[0].meshRender.sharedMaterial==saved[0],"Respawn/replaced HUD lost selection");
        first.IsMine=false;HeroController.players[0].hud=new PlayerHUD();
        typeof(Aquabro).GetMethod("RefreshSkillHud",Private).Invoke(first,null);
        Check(HeroController.players[0].hud.grenadeIcons[0].meshRender.sharedMaterial==null,"Remote hero changed local HUD");
        HeroController.players=null;
        Console.WriteLine("PASS skill HUD: four icons; per-player switch; BroMaker material source; empty ammo/visibility preserved; cached refresh; replacement HUD; remote guard");
    }

    private static Collider TerrainBox(float left,float bottom,float right,float top)
    {
        Collider c=new Collider((left+right)*.5f,(bottom+top)*.5f,true);
        Bounds b=new Bounds();b.min=new Vector3(left,bottom,0);b.max=new Vector3(right,top,0);
        b.center=new Vector3((left+right)*.5f,(bottom+top)*.5f,0);c.bounds=b;return c;
    }

    private static List<AquabroSkillVisual> SpringCast(Aquabro owner)
    {
        typeof(Aquabro).GetMethod("ClearSkillPreview",Private).Invoke(owner,null);
        owner.SpecialAmmo=3;owner.specialResolved=false;
        typeof(Aquabro).GetField("castingSkill",Private).SetValue(owner,3);
        typeof(Aquabro).GetMethod("UseSpecial",Private).Invoke(owner,null);
        Check(owner.SpecialAmmo==2,"Terrain placement spent more than one spring charge");
        return (List<AquabroSkillVisual>)typeof(Aquabro).GetField("skillVisuals",Private).GetValue(owner);
    }

    private static void CheckSpringTerrainPlacement()
    {
        Reset();Aquabro owner=new Aquabro();
        try
        {
            // Real scene relative to (3022,232): a one-tile depression with
            // 16px lips on both sides, then a rising staircase on the right.
            Physics.SceneTerrain=new Collider[]{TerrainBox(-6,-80,10,0),TerrainBox(-22,-80,-6,16),
                TerrainBox(10,-80,26,16),TerrainBox(26,-80,42,0),TerrainBox(42,-80,58,16),
                TerrainBox(58,-80,110,32)};
            List<AquabroSkillVisual> columns=SpringCast(owner);
            Check(columns.Count==7,"Single-tile lips cancelled spring side columns: "+columns.Count);
            for(int i=0;i<columns.Count;i++)
            {
                float x=(float)typeof(AquabroSkillVisual).GetField("x",Private).GetValue(columns[i]);
                float y=(float)typeof(AquabroSkillVisual).GetField("y",Private).GetValue(columns[i]);
                Check(x==(i-3)*30,"Terrain columns lost intended spacing");
                if(x>=60)Check(y==32,"Spring failed to follow rising steps");
                if(x==30 || x==0)Check(y==0,"Spring failed to return to lower floor");
            }
            foreach(float side in new float[]{-1,1})
            {
                Physics.SceneTerrain=new Collider[]{TerrainBox(-120,-32,120,0),
                    TerrainBox(side>0 ? 10 : -26,0,side>0 ? 26 : -10,64)};
                columns=SpringCast(owner);
                Check(columns.Count==4,"Real tall wall should stop the three columns beyond it");
                foreach(var c in columns)
                    Check((float)typeof(AquabroSkillVisual).GetField("x",Private).GetValue(c)*side<=0,"Spring crossed a tall wall");
            }
            Collider prop=TerrainBox(10,0,26,64);prop.ParentComponent=new BarrelBlock();
            Physics.SceneTerrain=new Collider[]{TerrainBox(-120,-32,120,0),prop};
            Check(SpringCast(owner).Count==7,"Prop became a spring terrain wall");
            prop.ParentComponent=null;prop.isTrigger=true;
            Check(SpringCast(owner).Count==7,"Trigger became a spring terrain wall");
            foreach(int cageHealth in new int[]{20,0})
            {
                Collider cage=TerrainBox(-70,0,-38,32);
                cage.ParentComponent=new Cage {health=cageHealth};
                Collider bar=TerrainBox(-39,0,-38,32);bar.gameObject.SetComponent(new CageBar());
                Physics.SceneTerrain=new Collider[]{TerrainBox(-120,-32,120,0),cage,bar};
                Check(SpringCast(owner).Count==7,"Cage/bar cancelled columns before its destruction");
                Physics.SceneTerrain=new Collider[]{TerrainBox(-120,-32,120,0),cage,bar,TerrainBox(-86,0,-70,64)};
                Check(SpringCast(owner).Count==6,"Ignoring cage also bypassed genuine wall behind it");
            }
        }
        finally
        {
            typeof(Aquabro).GetMethod("ClearSkillPreview",Private).Invoke(owner,null);
            Physics.SceneTerrain=new Collider[0];Reset();
        }
        Console.WriteLine("PASS spring terrain placement: live-scene 16px lips and stairs; seven intended offsets; lower floor; tall walls both sides; props/triggers; one charge");
    }

    private static void CheckTidalWave(System.Drawing.Bitmap atlas)
    {
        Check(atlas.Width == 16 * 128 && atlas.Height == 128, "Reference wave layout mismatch");
        for (int f = 0; f < 16; f++) for (int py = 0; py < 128; py++) for (int px = 0; px < 128; px++)
            Check(TidalWavePixelMask.Mask.Contains(f, px + .5f, py + .5f, 0) == (atlas.GetPixel(f * 128 + px, py).A > 0), "Wave mask differs from reference PNG");
        CheckWaveTerrainBoundary(atlas);
        CheckWaveSupportFloor();
        CheckWaveLargeEnemies(atlas);
        CheckWaveCloseCast();
        Aquabro owner = new Aquabro();
        foreach (float facing in new float[] { -1, 1 })
        {
            Reset();
            AquabroSkillVisual wave = new AquabroSkillVisual(owner, 0, 0, 0, facing, 2f, 0, .85f);
            for (int i = 0; i < 15; i++)
            {
                Check(wave.Tick(.1f), "Wave ended before full travel");
                if (i==0) Check(Math.Abs((float)typeof(AquabroSkillVisual).GetField("x", Private).GetValue(wave)-facing*16f)<.001f,
                    "Wave speed is not 160px/s");
            }
            float x = (float)typeof(AquabroSkillVisual).GetField("x", Private).GetValue(wave);
            Check(Math.Abs(x - facing * 240f) < .01f, "Wave travel is not 240px");
            Check(SpriteSM.LastFrame >= 6, "Wave did not enter dissolve");
            wave.Tick(.1f);
            Check((float)typeof(AquabroSkillVisual).GetField("x", Private).GetValue(wave) == x, "Dissolving wave still travels");
            wave.Dispose();
        }
        int sx = 64, sy = 64;
        for (int py = 35; py < 105; py++) for (int px = 40; px < 85; px++)
            if (atlas.GetPixel(5 * 128 + px, py).A > 0) { sx = px; sy = py; }
        Vector2 point = PixelWorld(sx, sy, 1, 1);
        Reset();
        Unit enemy = Target(point.x, point.y);
        HashSet<object> hits = new HashSet<object>();
        HashSet<GameObject> objects = new HashSet<GameObject>();
        Collider crate = new Collider(point.x, point.y, false);
        Physics.Contacts = new Collider[] { crate };
        Map.destroyableDoodads.Add(new Doodad(point.x,point.y));
        Check(owner.SkillWaveContact(0, 0, 1, 1, 5, hits, objects), "Breakable terrain stopped wave");
        Check(!crate.enabled && MapController.ObjectHits == 1 && Map.TotalHits == 1 && Map.Hits[enemy] == 26,
            "Wave failed to break terrain/items or damage enemy");
        owner.SkillWaveContact(0, 0, 1, 1, 5, hits, objects);
        Check(Map.TotalHits == 1 && MapController.ObjectHits == 1 && Map.DoodadHits > 0, "Wave repeats damage or misses doodads");
        Collider wall = new Collider(point.x, point.y + 12, true);
        Physics.Contacts = new Collider[] { wall };
        Check(!owner.SkillWaveContact(0, 0, 1, 1, 5, hits, objects) && wall.enabled, "Indestructible wall did not stop wave");
        Physics.Contacts = new Collider[0];
        WaterProjectileBarrier water = new WaterProjectileBarrier(owner, 0, 0, 0, 1, 1);
        water.SetFrame(5);
        water.MoveTo(100, 0);
        Projectile slow = new Projectile(); slow.X = 50; slow.Y = point.y;
        Check(WaterProjectileBarrier.TryBlock(slow, .02f), "Moving wave crossed stationary projectile");
        for (int frame = 6; frame < 16; frame++)
        {
            water.SetFrame(frame);
            Projectile shot = new Rocket(); shot.X = 100 + point.x; shot.Y = point.y;
            Check(!WaterProjectileBarrier.TryBlock(shot, .02f), "Dissolving wave intercepts rockets");
        }
        water.Dispose();
        Reset();
        Console.WriteLine("PASS tidal reference: 16-frame PNG/mask equality; mirrored 240px travel; breakable terrain/items/doodads; solid wall; single enemy hit; moving interception; dissolve passthrough");
    }

    private static void CheckWaveCloseCast()
    {
        foreach (float facing in new float[] {-1f,1f})
        foreach (int fps in new int[] {15,30,60,120})
        foreach (float gap in new float[] {0f,4f,8f,12f,16f,24f})
        {
            Reset();Aquabro owner=new Aquabro();owner.X=100;owner.Y=80;
            owner.transform.localScale=new Vector3(facing,1,1);
            typeof(Aquabro).GetField("castingSkill",Private).SetValue(owner,0);
            Unit close=new Unit();close.X=owner.X+facing*gap;close.Y=owner.Y;
            close.width=10;close.height=close.headHeight=20;close.playerNum=-1;close.health=25;
            Map.Targets.Add(close);Map.DamageObserver=delegate(Unit victim,int damage){victim.health-=damage;};
            typeof(Aquabro).GetMethod("UseSpecial",Private).Invoke(owner,null);
            var visuals=(List<AquabroSkillVisual>)typeof(Aquabro).GetField("skillVisuals",Private).GetValue(owner);
            for (int i=0;i<fps*.5f;i++) typeof(Aquabro).GetMethod("TickSkillPreview",Private).Invoke(owner,new object[]{1f/fps});
            Check(close.health==-1 && Map.TotalHits==1,"Real wave cast missed/repeated close enemy: facing="+facing+" fps="+fps+" gap="+gap);
            Check(owner.SpecialAmmo==2,"Close wave spent wrong ammo");
            typeof(Aquabro).GetMethod("ClearSkillPreview",Private).Invoke(owner,null);
        }
        Reset();
        Console.WriteLine("PASS wave close cast: real release and ticks; 0/4/8/12/16/24px; both facings; 15/30/60/120 FPS; 26 damage once");
    }

    private sealed class WaveHeavyVariant : MookBigGuy {}

    private static void CheckWaveLargeEnemies(System.Drawing.Bitmap atlas)
    {
        Aquabro owner=new Aquabro();
        for(int frame=0;frame<6;frame++)
        {
            int right=-1,edgeY=0;
            for(int py=0;py<111;py++)for(int px=0;px<128;px++)
                if(atlas.GetPixel(frame*128+px,py).A>0 && px>right){right=px;edgeY=py;}
            Check(right>=0,"Missing active wave edge");
            foreach(float scale in new float[]{.65f,.85f,1f})
            foreach(float facing in new float[]{-1,1})
            foreach(Unit target in new Unit[]{new MookBigGuy(),new WaveHeavyVariant(),new Unit()})
            {
                Reset();target.width=24;target.headHeight=target.height=26;target.playerNum=-1;
                int initialHealth=target is WaveHeavyVariant?40:target is MookBigGuy?25:48;target.health=initialHealth;
                target.X=(right+1-64)*scale*facing+facing*(12f+4.99f);
                target.Y=Mathf.Max(0,(112-edgeY-.5f)*scale-13);
                Check(!TidalWavePixelMask.Mask.Contains(frame,64+target.X*facing/scale,112-(target.Y+6)/scale,5/scale),
                    "Large enemy fixture not outside old foot-point detection");
                Map.Targets.Add(target);
                Map.DamageObserver=delegate(Unit victim,int damage){victim.health-=damage;};
                HashSet<object> hits=new HashSet<object>();HashSet<GameObject> objects=new HashSet<GameObject>();
                // A unit collider returned by the ground query must not cause a
                // terrain hit or stop the wave before the unit damage pass.
                Collider body=new Collider((right+.5f-64)*scale*facing,(112-edgeY-.5f)*scale,true);
                body.ParentComponent=target;Physics.Contacts=new Collider[]{body};
                bool passed = owner.SkillWaveContact(0,0,facing,scale,frame,hits,objects);
                Check(passed == (!(target is MookBigGuy) || target.health <= 0),"Heavy enemy blocking did not follow surviving body");
                Check(target.health==initialHealth-26 && Map.Hits[target]==26 && Map.TotalHits==1 && MapController.ObjectHits==0,
                    "Body contact did not deal exactly 26 damage");
                owner.SkillWaveContact(0,0,facing,scale,frame,hits,objects);
                Check(Map.TotalHits==1,"Wave repeated enemy damage");
                target.health=200;Map.Hits.Clear();Map.TotalHits=0;hits.Clear();
                owner.SkillWaveContact(0,0,facing,scale,frame,hits,objects);
                Check(target.health==174,"High-health enemy bypasses fixed wave damage");
                foreach(int blockedCase in new int[]{0,1,2,3,4,5})
                {
                    target.health=100;target.invulnerable=blockedCase==0;
                    target.playerNum=blockedCase==1?owner.playerNum:-1;
                    owner.IsMine=blockedCase!=2;Physics.Blocked=blockedCase==3;
                    if(blockedCase==4)target.Y=-30;
                    int before=Map.TotalHits;
                    owner.SkillWaveContact(0,0,facing,scale,blockedCase==5?6:frame,new HashSet<object>(),objects);
                    Check(Map.TotalHits==before,"Wave ignores immunity/ally/ownership/wall/waterline/dissolve filter");
                }
                owner.IsMine=true;Physics.Blocked=false;
            }
        }
        Reset();Console.WriteLine("PASS wave large enemies: all 6 active frames; both facings; 3 scales; body-only contact; 25/40/48 HP targets; unit colliders not walls; 26 fixed damage; single hit; filters and support waterline");
    }

    private static void CheckWaveTerrainBoundary(System.Drawing.Bitmap atlas)
    {
        for (int frame=0;frame<6;frame++)
        {
            int right=-1,edgeY=0;
            for (int py=0;py<128;py++) for (int px=0;px<128;px++)
                if (atlas.GetPixel(frame*128+px,py).A>0 && px>right) {right=px;edgeY=py;}
            foreach (float scale in new float[] {.65f,.85f,1f})
            foreach (float facing in new float[] {-1,1})
            {
                Reset();
                float edge=(right+1-64)*scale*facing, y=(112-edgeY-.5f)*scale;
                Collider near=new Collider(edge+facing*5.999f,y,false);
                Collider far=new Collider(edge+facing*6.01f,y,false);
                Doodad nearItem=new Doodad(near.bounds.center.x,y),farItem=new Doodad(far.bounds.center.x,y);
                Physics.Contacts=new Collider[] {near,far};
                Map.destroyableDoodads.Add(nearItem);Map.destroyableDoodads.Add(farItem);
                Aquabro owner=new Aquabro();
                owner.SkillWaveContact(0,0,facing,scale,frame,new HashSet<object>(),new HashSet<GameObject>());
                Check(!near.enabled && far.enabled && nearItem.Damaged && !farItem.Damaged && MapController.ObjectHits==1,
                    "Terrain/item five-pixel boundary failed: frame "+frame+" scale "+scale+" facing "+facing);
                near.enabled=true;nearItem.Damaged=false;MapController.ObjectHits=0;
                owner.SkillWaveContact(0,0,facing,scale,6,new HashSet<object>(),new HashSet<GameObject>());
                Check(near.enabled && !nearItem.Damaged && MapController.ObjectHits==0,"Dissolve damages terrain/items");
            }
        }
        Reset();
        Console.WriteLine("PASS wave terrain/items: every active frame; both facings; three scales; 4.999px hit / 5.01px excluded; dissolve inactive");
    }

    private static void CheckWaveSupportFloor()
    {
        Aquabro owner=new Aquabro();
        foreach (float waterline in new float[] {0f,248f})
        foreach (float scale in new float[] {.65f,.85f,1f})
        foreach (float facing in new float[] {-1f,1f})
        for (int frame=0;frame<16;frame++)
        {
            Reset();
            Collider floor=new Collider(0,waterline-8f,false);
            Bounds bounds=new Bounds();bounds.min=new Vector3(-128,waterline-16f,0);bounds.max=new Vector3(128,waterline,0);
            bounds.center=new Vector3(0,waterline-8f,0);floor.bounds=bounds;
            Doodad below=new Doodad(0,waterline-1f);Map.destroyableDoodads.Add(below);
            Physics.Contacts=new Collider[] {floor};
            Check(owner.SkillWaveContact(0,waterline,facing,scale,frame,new HashSet<object>(),new HashSet<GameObject>()),
                "Supporting floor stopped wave");
            Check(floor.enabled && MapController.ObjectHits==0 && !below.Damaged,"Wave destroyed supporting floor/below-waterline object");
        }
        Reset();
        Console.WriteLine("PASS wave support: all frames, both facings, three scales, elevated floors; below-waterline terrain/items preserved");
    }

    private static void CheckRopeSettlement()
    {
        foreach (bool zipline in new bool[] { false, true })
        foreach (bool alreadyAttached in new bool[] { false, true })
        {
            Aquabro owner = new Aquabro();
            typeof(Aquabro).GetField("castingSkill", Private).SetValue(owner, 3);
            object rope = new object();
            owner.actionState = alreadyAttached && !zipline ? ActionState.ClimbingLadder : ActionState.Jumping;
            owner.attachedToZipline = alreadyAttached && zipline ? rope : null;
            owner.yI = 45f;
            typeof(Aquabro).GetMethod("UseSpecial", Private).Invoke(owner, null);
            List<AquabroSkillVisual> columns = (List<AquabroSkillVisual>)typeof(Aquabro).GetField("skillVisuals", Private).GetValue(owner);
            if (!alreadyAttached)
            {
                Check(columns.Count == 7 && owner.yI == 45f, "Air skill waited for rope or changed velocity");
                owner.actionState = zipline ? ActionState.Jumping : ActionState.ClimbingLadder;
                owner.attachedToZipline = zipline ? rope : null;
                owner.yI = 45f;
                typeof(Aquabro).GetMethod("TickSkillPreview", Private).Invoke(owner, new object[] { .01f });
            }
            Check(columns.Count == 7 && owner.SpecialAmmo == 2, "Rope cast failed to release");
            Check(owner.yI == 45f && owner.actionState == (zipline ? ActionState.Jumping : ActionState.ClimbingLadder) &&
                (!zipline || object.ReferenceEquals(owner.attachedToZipline, rope)), "Skill changed rope movement/attachment");
            typeof(Aquabro).GetMethod("TickSkillPreview", Private).Invoke(owner, new object[] { .01f });
            Check(columns.Count == 7, "Rope skill released twice");
            typeof(Aquabro).GetMethod("ClearSkillPreview", Private).Invoke(owner, null);
        }
        Console.WriteLine("PASS ropes: immediate cast before/while attached to ladder/zipline; original movement/attachment preserved; no duplicate release");
    }

    private static void CheckDissolve(System.Drawing.Bitmap atlas)
    {
        Aquabro owner = new Aquabro();
        for (int frame = 17; frame < 28; frame++)
        {
            Reset();
            AquabroSkillVisual effect = new AquabroSkillVisual(owner, 3, 0, 0, 1, 1.4f, 0, 1);
            // Put a new enemy and both shot types on a visible pixel of this frame.
            int px = 64, py = 100;
            bool found = false;
            for (int y = 0; y < 128 && !found; y++) for (int x = 0; x < 128 && !found; x++)
                if (atlas.GetPixel(frame * 128 + x, y).A > 0) { px = x; py = y; found = true; }
            Vector2 point = PixelWorld(px, py, 1, 1);
            Target(point.x, point.y);
            Check(effect.Tick(frame * .05f + .001f) && SpriteSM.LastFrame == frame, "Dissolve visual ended too early");
            Check(Map.TotalHits == (frame == 17 ? 1 : 0), "Dissolve frame damages new enemies: " + frame);
            foreach (bool rocket in new bool[] { false, true })
            {
                Projectile shot = rocket ? new Rocket() : new Projectile();
                shot.X = point.x; shot.Y = point.y;
                Check(WaterProjectileBarrier.TryBlock(shot, .02f) == (frame == 17), "Dissolve frame intercepts projectiles: " + frame);
            }
            effect.Dispose();
        }
        Reset();
        Console.WriteLine("PASS spring dissolve: final active frame 17; frames 18-27 remain visible with no damage or bullet/rocket interception");
    }

    private static void CheckTraversalSettlement()
    {
        foreach (string contact in new string[] { "WallDrag", "wallClimbing", "ledgeGrapple" })
        foreach (bool alreadyGrabbing in new bool[] { false, true })
        {
            Aquabro owner = new Aquabro();
            typeof(Aquabro).GetField("castingSkill", Private).SetValue(owner, 3);
            owner.actionState = ActionState.Jumping;
            FieldInfo traversal = typeof(TestHero).GetField(contact);
            traversal.SetValue(owner, alreadyGrabbing);
            owner.yI = -40f;
            typeof(Aquabro).GetMethod("UseSpecial", Private).Invoke(owner, null);
            List<AquabroSkillVisual> columns = (List<AquabroSkillVisual>)typeof(Aquabro).GetField("skillVisuals", Private).GetValue(owner);
            if (!alreadyGrabbing)
            {
                Check(columns.Count == 7 && owner.yI == -40f, "Air cast waited for wall contact or changed velocity");
                traversal.SetValue(owner, true);
                owner.yI = -40f; // Original traversal has taken over movement.
                typeof(Aquabro).GetMethod("TickSkillPreview", Private).Invoke(owner, new object[] { 0f });
                Check(columns.Count == 7, "Paused traversal changed column count");
                typeof(Aquabro).GetMethod("TickSkillPreview", Private).Invoke(owner, new object[] { .01f });
            }
            Check(columns.Count == 7 && owner.SpecialAmmo == 2, "Traversal failed to release exactly one cast: " + contact);
            Check((bool)traversal.GetValue(owner) && owner.yI == -40f && owner.canWallClimb && owner.canLedgeGrapple,
                "Skill changed original traversal: " + contact);
            owner.actionState = ActionState.Idle;
            typeof(Aquabro).GetMethod("TickSkillPreview", Private).Invoke(owner, new object[] { .01f });
            Check(columns.Count == 7 && owner.SpecialAmmo == 2, "Landing released traversal cast twice");
            typeof(Aquabro).GetMethod("ClearSkillPreview", Private).Invoke(owner, null);
        }
        Console.WriteLine("PASS traversal: immediate spring before/while grabbing wall/climb/ledge; original movement preserved; pause; no duplicate landing cast");
    }

    private static void CheckBarriers(Aquabro owner)
    {
        owner.X=owner.Y=0;
        foreach (int kind in new int[] { 2,3 })
        {
            WaterProjectileBarrier water=new WaterProjectileBarrier(owner,kind,0,0,1,.9f);
            Projectile pending=new Projectile(); pending.X=0;pending.Y=25;
            Check(!WaterProjectileBarrier.TryBlock(pending,.02f),"Delayed water blocks before visible");
            water.SetFrame(10);
            foreach (bool rocket in new bool[] { false,true })
            {
                Projectile shot=rocket ? new Rocket() : new Projectile();
                shot.X=-110;shot.Y=25;shot.xI=7000;shot.projectileSize=1;shot.Syncronize=rocket;
                Check(WaterProjectileBarrier.TryBlock(shot,.033f),"Fast bullet/rocket crossed water kind "+kind);
                Check(!shot.gameObject.activeInHierarchy && !shot.DeathCalled,"Intercepted rocket explodes");
                Check(shot.NetworkDestroyed==rocket,"Network destroy mismatch");
            }
            Projectile own=new Projectile();own.X=0;own.Y=25;own.playerNum=owner.playerNum;
            Check(!WaterProjectileBarrier.TryBlock(own,.02f),"Water blocks own/allied projectile");
            Projectile high=new Projectile();high.X=-110;high.Y=180;high.xI=7000;
            Check(!WaterProjectileBarrier.TryBlock(high,.033f),"Blocks shot above water");
            Projectile paused=new Projectile();paused.X=0;paused.Y=25;
            Check(!WaterProjectileBarrier.TryBlock(paused,0),"Paused projectile changed");
            owner.IsMine=false; Check(!WaterProjectileBarrier.TryBlock(paused,.02f),"Remote owner intercepts");owner.IsMine=true;
            water.SetFrame(kind==2 ? 31 : 27);
            Check(!WaterProjectileBarrier.TryBlock(paused,.02f),"Empty frame still blocks");
            water.SetFrame(10);water.Dispose();
            Check(!WaterProjectileBarrier.TryBlock(paused,.02f),"Disposed water still blocks");
        }
        Console.WriteLine("PASS water barriers: both skills; swept fast bullets/rockets; friendly passthrough; out-of-range; pause/ownership; delayed/empty/disposed; no rocket explosion; network destruction");
    }
}

// 仅用于离线测试的游戏接口，不进入正式 Aquabro DLL。
namespace UnityEngine
{
    public class MonoBehaviour { }
    public struct Color32 { public byte r,g,b,a;public Color32(byte r,byte g,byte b,byte a){this.r=r;this.g=g;this.b=b;this.a=a;} }
    public enum TextureFormat { ARGB32 } public enum FilterMode { Point } public enum TextureWrapMode { Clamp }
    public class Texture2D : Object {
        public int width,height;public FilterMode filterMode;public TextureWrapMode wrapMode;
        public Texture2D(int w,int h,TextureFormat f,bool mip){width=w;height=h;}
        private Color32[] data;
        public bool LoadImage(byte[] bytes){
            using(System.IO.MemoryStream stream=new System.IO.MemoryStream(bytes))
            using(System.Drawing.Bitmap png=new System.Drawing.Bitmap(stream)){
                width=png.Width;height=png.Height;data=new Color32[width*height];
                for(int y=0;y<height;y++)for(int x=0;x<width;x++){
                    System.Drawing.Color c=png.GetPixel(x,height-1-y);data[y*width+x]=new Color32(c.R,c.G,c.B,c.A);
                }
            }
            return true;
        }
        public Color32[] GetPixels32(){return data ?? new Color32[width*height];}
        public void SetPixels32(Color32[] pixels){data=(Color32[])pixels.Clone();} public void Apply(bool mip,bool unreadable){}
    }
    public class Object { public static void Destroy(Object value) {} public static T[] FindObjectsOfType<T>() { return new T[0]; } }
    public struct Vector3
    {
        public float x,y,z; public Vector3(float x,float y,float z) { this.x=x;this.y=y;this.z=z; }
        public float magnitude { get { return (float)Math.Sqrt(x*x+y*y+z*z); } }
        public static Vector3 down {get {return new Vector3(0,-1,0);}}
        public Vector3 normalized { get { float m=magnitude;return new Vector3(x/m,y/m,z/m); } }
        public static Vector3 operator -(Vector3 a,Vector3 b) { return new Vector3(a.x-b.x,a.y-b.y,a.z-b.z); }
    }
    public struct Vector2
    {
        public float x,y;public Vector2(float x,float y) { this.x=x;this.y=y; }
        public static float Distance(Vector2 a,Vector2 b) { return new Vector3(a.x-b.x,a.y-b.y,0).magnitude; }
    }
    public class Transform { public Transform parent;public Vector3 position;public Vector3 localScale=new Vector3(1,1,1); }
    public class GameObject : Object
    {
        private readonly Dictionary<Type,object> components=new Dictionary<Type,object>();
        public Collider Contact;
        public Transform transform=new Transform();public bool activeInHierarchy=true;
        public GameObject(string name) {} public void SetActive(bool value) { activeInHierarchy=value; }
        public T AddComponent<T>() where T:new() {T value=new T();components[typeof(T)]=value;return value;}
        public void AddComponent(object value) {components[value.GetType()]=value;}
        public void SetComponent(object value) {components[value.GetType()]=value;}
        public object[] GetComponents(Type type) {return new List<object>(components.Values).ToArray();}
        public T GetComponent<T>() where T:class {object value;if(components.TryGetValue(typeof(T),out value))return value as T;foreach(object component in components.Values)if(component is T)return component as T;return Contact==null?null:Contact.Relay as T;}
    }
    public struct RaycastHit { public Vector3 point;public float distance;public Collider collider; }
    public struct Bounds
    {
        public Vector3 center, min, max;
        public Bounds(float x, float y) { center=new Vector3(x,y,0); min=new Vector3(x-1,y-1,0); max=new Vector3(x+1,y+1,0); }
    }
    public class Collider
    {
        public bool isTrigger;public object ParentComponent;public DamageRelay Relay;
        public int TerrainMask=Map.groundLayerStrict;
        public T GetComponent<T>() where T:class {return Relay as T ?? gameObject.GetComponent<T>();}
        public T GetComponentInParent<T>() where T:class {return ParentComponent as T;}
        public Bounds bounds;public bool enabled=true,indestructible;public GameObject gameObject=new GameObject("Object");
        public Transform transform {get {return gameObject.transform;}}
        public Collider(float x,float y,bool solid) {bounds=new Bounds(x,y);indestructible=solid;gameObject.Contact=this;}
    }
    public static class Physics
    {
        public static bool SharkTerrainEnabled,SharkFloor=true;
        public static float SharkWallX=float.PositiveInfinity,SharkFloorY;
        public static RaycastHit[] SharkPropHits=new RaycastHit[0];
        public static Collider[] Contacts=new Collider[0];
        public static Collider[] LadderContacts=new Collider[0];
        public static Collider[] SceneTerrain=new Collider[0];
        private static bool ClipSceneAxis(float origin,float direction,float min,float max,ref float near,ref float far)
        {
            if(Math.Abs(direction)<.000001f)return origin>=min && origin<=max;
            float a=(min-origin)/direction,b=(max-origin)/direction;
            near=Math.Max(near,Math.Min(a,b));far=Math.Min(far,Math.Max(a,b));return near<=far;
        }
        private static RaycastHit[] SceneHits(Vector3 origin,Vector3 direction,float range,int layer)
        {
            List<RaycastHit> result=new List<RaycastHit>();
            foreach(Collider c in SceneTerrain)
            {
                if(!c.enabled || !c.gameObject.activeInHierarchy || (c.TerrainMask & layer)==0)continue;
                if(origin.x>c.bounds.min.x && origin.x<c.bounds.max.x && origin.y>c.bounds.min.y && origin.y<c.bounds.max.y)continue;
                float near=0,far=range;
                if(!ClipSceneAxis(origin.x,direction.x,c.bounds.min.x,c.bounds.max.x,ref near,ref far) ||
                    !ClipSceneAxis(origin.y,direction.y,c.bounds.min.y,c.bounds.max.y,ref near,ref far))continue;
                RaycastHit hit=new RaycastHit();hit.collider=c;hit.distance=near;
                hit.point=new Vector3(origin.x+direction.x*near,origin.y+direction.y*near,0);result.Add(hit);
            }
            return result.ToArray();
        }
        public static Collider[] OverlapSphere(Vector3 point,float range,int layer)
        {
            if (layer!=4) return Contacts;
            return Array.FindAll(LadderContacts,delegate(Collider c) {
                return c.enabled && point.x+range>=c.bounds.min.x && point.x-range<=c.bounds.max.x &&
                    point.y+range>=c.bounds.min.y && point.y-range<=c.bounds.max.y;
            });
        }
        public static bool Blocked;
        public static float BlockedBelowY=float.NegativeInfinity;
        public static bool Raycast(Vector3 origin,Vector3 direction,out RaycastHit hit,float range,int layer)
        {
            hit=new RaycastHit();
            if(SceneTerrain.Length>0)
            {
                bool found=false;float best=float.PositiveInfinity;
                foreach(RaycastHit c in SceneHits(origin,direction,range,layer))
                    if(c.distance<best){best=c.distance;hit=c;found=true;}
                return found;
            }
            if (SharkTerrainEnabled && (layer & Map.groundLayerStrict)!=0)
            {
                hit.collider=new Collider(0,0,true);
                float floor=direction.y<0 && SharkFloor ? (SharkFloorY-origin.y)/direction.y : float.PositiveInfinity;
                float wall=direction.x!=0 ? (SharkWallX-origin.x)/direction.x : float.PositiveInfinity;
                float distance=float.PositiveInfinity;
                if (floor>=0 && floor<=range) distance=floor;
                if (wall>=0 && wall<=range && wall<distance) distance=wall;
                if (float.IsPositiveInfinity(distance)) return false;
                hit.distance=distance;hit.point=new Vector3(origin.x+direction.x*distance,origin.y+direction.y*distance,0);
                return true;
            }
            if (Blocked || origin.y<BlockedBelowY) {hit.collider=new Collider(0,0,true);hit.distance=range*.5f;return true;}
            return false;
        }
        public static bool Raycast(Vector3 origin,Vector3 direction,float range,int layer) {return Blocked || origin.y<BlockedBelowY;}
        public static RaycastHit[] RaycastAll(Vector3 origin,Vector3 direction,float range,int layer)
        {
            if(SceneTerrain.Length>0)return SceneHits(origin,direction,range,layer);
            List<RaycastHit> result=new List<RaycastHit>();RaycastHit terrain;
            if (Raycast(origin,direction,out terrain,range,layer)) result.Add(terrain);
            if (SharkTerrainEnabled && (layer & Map.groundLayerStrict)!=0)
            foreach (RaycastHit prop in SharkPropHits)
            {
                if (!prop.collider.enabled || !prop.collider.gameObject.activeInHierarchy || (prop.collider.TerrainMask & layer)==0) continue;
                RaycastHit hit=prop;
                if (direction.y<0)
                {
                    if (origin.x<prop.collider.bounds.min.x || origin.x>prop.collider.bounds.max.x) continue;
                    hit.distance=origin.y-prop.point.y;
                }
                else
                {
                    if (origin.y<prop.collider.bounds.min.y || origin.y>prop.collider.bounds.max.y) continue;
                    hit.distance=(prop.point.x-origin.x)/direction.x;
                }
                if (hit.distance>=0 && hit.distance<=range) result.Add(hit);
            }
            return result.ToArray();
        }
    }
    public static class Mathf
    {
    public static float Sin(float a) {return (float)Math.Sin(a);}
    public static float Min(float a,float b) {return Math.Min(a,b);}
        public static float Max(float a,float b) {return Math.Max(a,b);}
        public static int Clamp(int n,int a,int b) { return Math.Max(a,Math.Min(b,n)); }
        public static float Clamp(float n,float a,float b) { return Math.Max(a,Math.Min(b,n)); }
        public static float Abs(float n) { return Math.Abs(n); }
        public static int FloorToInt(float n) { return (int)Math.Floor(n); }
        public static float MoveTowards(float a,float b,float step) { return a+Clamp(b-a,-step,step); }
    }
    public class Font { public static Font CreateDynamicFontFromOSFont(string[] names,int size) { return new Font(); } }
    public class GUIStyle { public GUIStyle normal;public Font font;public int fontSize;public Color textColor;
        public GUIStyle() {normal=this;}public GUIStyle(GUIStyle other) {normal=this;} }
    public struct Color { public Color(float r,float g,float b) {} }
    public struct Rect { public Rect(float x,float y,float w,float h) {} }
    public static class GUI
    {
        public static GUIStyleContainer skin=new GUIStyleContainer();public static string LastLabel;
        public static void Box(Rect r,string label,GUIStyle style) {LastLabel=label;}
    }
    public class GUIStyleContainer { public GUIStyle box=new GUIStyle(); }
}
public class Unit
{
    public float X,Y,xI,yI,width=8,height=8,headHeight=18;public int health=100,playerNum;public bool invulnerable;
    public int KnockCalls;public bool ForcedTumble;
    public virtual void Knock(DamageType type,float vx,float vy,bool tumble) {xI+=vx*.5f;yI+=vy;KnockCalls++;ForcedTumble=tumble;}
    public int PanicCalls,PanicDirection;public float PanicDuration;
    public virtual void Panic(int direction,float time,bool forgetPlayer) {PanicCalls++;PanicDirection=direction;PanicDuration=time;}
    public virtual bool HasBeenCoveredInAcid() { return false; }
    public GameObject gameObject=new GameObject("Test unit");
}
public class TestVanDammeAnim : Unit
{
    public bool canBeCoveredInAcid=true, hasBeenCoverInAcid;
    public override bool HasBeenCoveredInAcid() { return hasBeenCoverInAcid; }
}
public class Alien : TestVanDammeAnim {}
public class AlienMosquito : Mook {}
public class HellLostSoul : AlienMosquito {}
public class MookSuicide : Mook {}
public class MookSuicideUndead : Mook {}
public class MookRiotShield : Mook { public int Facing=1; }
public class Mook : TestVanDammeAnim
{
    public bool canTumble,fire,left,right,doingMelee;
    public float stunTime;public int StunCalls;public ActionState actionState;
    public bool IsStunned {get {return stunTime>0;}}
    public void Stun(float time) {stunTime=time;StunCalls++;xI=0;actionState=ActionState.Idle;doingMelee=false;left=right=false;}
    public void ClearAllInput() {fire=left=right=false;}
}
public class MookBigGuy : Mook
{
    public override void Knock(DamageType type,float vx,float vy,bool tumble)
    {xI=Mathf.Clamp(xI+vx/20f,-200f,200f);yI=Mathf.Clamp(yI+vy/5f,-20000f,300f);KnockCalls++;}
}
public class FallingBlock : Block {}
public class Cage : Block {}
public class CageBar {}
public class CrateBlock : FallingBlock {}
public class BarrelBlock : FallingBlock {}
public class DamageObject
{
    public int damage;
    public DamageObject(int damage,DamageType type,float fx,float fy,float x,float y,object sender) {this.damage=damage;}
}
public class Doodad
{
    public GameObject gameObject=new GameObject("Doodad");
    public float centerX,centerY,width=2,height=2;
    public bool immuneToHeroDamage,CanBeDamagedByMooks=true,isImpenetrable,Damaged;
    public Doodad(float x,float y) {centerX=x;centerY=y;}
    public void DamageOptional(DamageObject damage,ref bool hit) {Damaged=true;hit=true;Map.DoodadHits++;}
}
public class DoodadBloodPool : Doodad { public static List<DoodadBloodPool> bloodDoodads=new List<DoodadBloodPool>(); public float X,Y; public DoodadBloodPool():base(0,0){} }
public class DoodadAcidPool : DoodadBloodPool {}
public enum BloodColor { Red,Green }
public enum DirectionEnum { None,Up,Down,Left,Right }
public class SlimeGroundCover : MonoBehaviour
{
    public GameObject gameObject=new GameObject("Death slime");public Transform transform {get{return gameObject.transform;}}
    public BloodColor slimeColor=BloodColor.Green;public int damage=15;public DirectionEnum direction;
    public void Setup(Block block,int amount,DirectionEnum side) {direction=side;damage=amount;}
}
public class Elevator : Doodad { public Elevator() : base(0,0) {} }
public class GiantElevator : Elevator {}
public class Projectile : Unit
{
    public DamageType damageType;
    public float projectileSize=1,life=2;public bool Syncronize,NetworkDestroyed,DeathCalled;
    public Projectile() {playerNum=-1;}
    public void DestroyNetworked() {NetworkDestroyed=true;}
    public virtual void Death() {DeathCalled=true;}
}
public class Rocket : Projectile {}
public enum ActionState { Idle,Running,Jumping,ClimbingLadder,Dead }
public class Block { public int health=100; }
public enum DamageType { Acid,Explosion,Crush,Drill,Unholy,None }
public class DamageRelay
{
    protected bool isDestroyed;
    public void MarkDestroyed(bool value) {isDestroyed=value;}
    public Unit unit;public DamageType immunity=DamageType.Drill;
    public DamageType forceDamageType=DamageType.None;
    public Doodad doodad;public BossBlockWeapon bossWeapon;public GameObject damageGameObject;
    public Collider ignoreDamageWhileColliderActive,ignoreDamageWhileOtherColliderActive;
}
public static class GestureElement { public enum Gestures { None,Flex } }
public enum HeroType {BroDredd}
public class BroDredd {public Projectile specialRocketPrefab=new Projectile();}
public class Player {public Unit character;public PlayerHUD hud;}
public class PlayerHUD {public SpriteSM[] grenadeIcons={new SpriteSM(),new SpriteSM(),new SpriteSM(),new SpriteSM(),new SpriteSM(),new SpriteSM()};}
public static class HeroController
{
    public static Player[] players;
    public static BroDredd Dredd=new BroDredd();public static Transform CameraTarget;public static int CameraAdds,CameraRemoves;
    public static object GetHeroPrefab(HeroType type) {return Dredd;}
    public static void AddTemporaryPlayerTarget(int player,Transform target) {CameraTarget=target;CameraAdds++;}
    public static void RemoveTemporaryPlayerTarget(int player) {CameraTarget=null;CameraRemoves++;}
    public static void FlashSpecialAmmo(int n) {}
}
public static class GameModeController { public static bool DoesPlayerNumDamage(int a,int b) {return a!=b;} }
public static class SortOfFollow { public static void Shake(float amount,Vector3 point) {} }
public static class MapController
{
    public static int ObjectHits;
    public static void Damage_Networked(object owner,GameObject receiver,int damage,DamageType type,float fx,float fy,float x,float y)
    {
        ObjectHits++;
        if(BossDamageTestStubs.Submit(receiver,damage,type,fx,fy))return;
        DamageRelay relay=receiver.Contact.Relay;
        if(relay!=null)
        {
            Map.LastDamageType=type;
            TestVanDammeAnim character=relay.unit as TestVanDammeAnim;
            if(type==DamageType.Acid && character!=null && character.canBeCoveredInAcid)character.hasBeenCoverInAcid=true;
            Map.Hits[relay.unit]=damage;Map.TotalHits++;Map.LastUpForce=fy;if(Map.DamageObserver!=null)Map.DamageObserver(relay.unit,damage);return;
        }
        if (!receiver.Contact.indestructible) receiver.Contact.enabled=false;
    }
}
public static class Map
{
    public static int groundLayer=3,groundLayerStrict=2,groundAndDamageableObjects=7,DoodadHits;public static List<Unit> Targets=new List<Unit>();
    public static List<Doodad> destroyableDoodads=new List<Doodad>();
    public static Dictionary<Unit,int> Hits=new Dictionary<Unit,int>();public static int TotalHits;public static float LastUpForce;
    public static Action<Unit,int> DamageObserver;
    public static DamageType LastDamageType;
    public static List<Unit> GetUnitsInRange(float xr,float yr,float x,float y,bool dead)
    {
        List<Unit> result=new List<Unit>();
        foreach(Unit unit in Targets)
            if((dead || unit.health>0) && Math.Abs(unit.X-x)<=xr && Math.Abs(unit.Y+6-y)<=yr)result.Add(unit);
        return result;
    }
    public static void KnockAndDamageUnit(Unit source,Unit target,int damage,DamageType kind,float fx,float fy,int direction,bool knock,float x,float y,bool remote)
    { // Native riot-shield Damage rejects frontal directional hits, including Acid.
      if(target is MookRiotShield && direction!=0 && direction!=((MookRiotShield)target).Facing)return;
      Hits[target]=damage;TotalHits++;LastUpForce=fy;LastDamageType=kind;
      TestVanDammeAnim character=target as TestVanDammeAnim;
      if(kind==DamageType.Acid && character!=null && character.canBeCoveredInAcid)character.hasBeenCoverInAcid=true;
      if(DamageObserver!=null)DamageObserver(target,damage); }
    public static void HitProjectiles(int p,int n,DamageType t,float r,float x,float y,float fx,float fy,float time) {}
}
public class SpriteSM
{
    public Vector3 TestOffset;public void SetOffset(Vector3 offset) {TestOffset=offset;}
    public MeshRenderer meshRender=new MeshRenderer();public static int LastFrame;public int TestFrame;
    public void SetPixelDimensions(int w,int h) {}public void SetSize(float w,float h) {}
    public void SetLowerLeftPixel(int x,int y) {TestFrame=LastFrame=x/128;}
    public void RecalcTexture() {}public void CalcUVs() {}public void UpdateUVs() {}
}
public class MeshRenderer { public object material; public Material sharedMaterial {get{return material as Material;}set{material=value;}} public bool enabled=true; }
public class Material : UnityEngine.Object {
    public string name;public UnityEngine.Texture2D mainTexture=new UnityEngine.Texture2D(4096,128,UnityEngine.TextureFormat.ARGB32,false);
    public Material() {} public Material(Material source) { name=source.name;mainTexture=source.mainTexture; }
}
namespace Rogueforce { public static class ValueOrchestrator {public static int GetModifiedDamage(int n,int p) {return n;}} }
namespace BroMakerLib { public static class ResourcesController {
    private static Dictionary<string,Material> materials=new Dictionary<string,Material>();
    public static Material GetMaterial(string p,string f) {
        if(!materials.ContainsKey(f)) {
            Material material=new Material{name=f};
            if(f.StartsWith("skill-")) {
                int frames=f=="skill-shark-hunt-body.png"?59:f.Contains("tidal-wave")?16:f.Contains("maelstrom")?32:28;
                material.mainTexture=new UnityEngine.Texture2D(frames*128,128,UnityEngine.TextureFormat.ARGB32,false);
                UnityEngine.Color32[] colors=new UnityEngine.Color32[frames*128*128];
                // Reproduce the live BroMaker cache: GPU texture is valid but CPU reads are 0xCD.
                UnityEngine.Color32 color=new UnityEngine.Color32(205,205,205,205);
                for(int i=0;i<colors.Length;i++)colors[i]=color;
                material.mainTexture.SetPixels32(colors);
            }
            materials[f]=material;
        }
        return materials[f];
    }
} }
namespace AquabroMod
{
    public class TestHero : Unit
    {
        public bool WallDrag,wallClimbing,ledgeGrapple,canWallClimb=true,canLedgeGrapple=true;
        public object attachedToZipline;
        public bool usingSpecial,usingPockettedSpecial,doingMelee,IsMine=true,specialResolved;
        public bool TestGunVisible=true;public int TestBodyCell;public Vector2 TestGunPosition;
        public bool up,down,buttonJump,left,right,canCeilingHang=true,wallClimbAnticipation,Grounded=true,Incapacitated;
        public bool fire,highFive,buttonGesture;
        protected int ladderLayer=4;
        public bool MovementSawTraversal,MovementSawJump,LadderContact,LadderTopSupport;
        public float speed=130,jumpTime;
        protected float controllingProjectileDelay;
        public SpriteSM sprite=new SpriteSM();
        public float chargeTime,throwDelay,frameRate;public int SpecialAmmo=3,originalSpecialAmmo=3,frame,pressSpecialFacingDirection;
        public ActionState actionState;
        public Transform transform=new Transform();public TestInfo Info=new TestInfo();
        public List<Material> CurrentSpecialMaterials {get;set;}
        public Vector2 CurrentSpecialMaterialOffset {get;set;}
        public virtual void SetGestureAnimation(GestureElement.Gestures g) {}
        protected virtual void PressSpecial() {usingSpecial=true;frame=0;}protected virtual void UseSpecial() {}protected virtual void AnimateSpecial() {}
        protected virtual void CalculateMovement() {}
        protected virtual void CheckInput() {}
        protected virtual void RunMovement() {MovementSawTraversal=canWallClimb || canLedgeGrapple || canCeilingHang || up || down;MovementSawJump=buttonJump;X+=xI*.05f;}
        protected bool IsGroundBelow() {return Grounded;}
        protected bool IsGroundBelowAtXOffset(float offset,bool includeLadders) {return Grounded || (includeLadders && LadderTopSupport);}
        protected virtual bool IsNearbyLadder(float offsetX,float offsetY) {return LadderContact;}
        protected bool IsIncapacitated() {return Incapacitated;}protected void RestoreTridentGunSize() {}
        protected void SetSpriteOffset(float x,float y) {}protected void SetGunPosition(float x,float y) {TestGunPosition=new Vector2(x,y);}
        protected void TriggerBroSpecialEvent() {}protected void PlayAttackSound(float v) {}
        protected void DeactivateGun() {TestGunVisible=false;}protected void ActivateGun() {TestGunVisible=true;}protected void SetBodyFrame(int n) {TestBodyCell=n;}protected void ChangeFrame() {}
    }
    public class TestInfo {public string path="";}
    public partial class Aquabro : TestHero {}
}

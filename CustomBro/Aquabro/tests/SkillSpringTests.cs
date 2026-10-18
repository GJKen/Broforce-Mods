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
            CheckCastStart();
            using (System.Drawing.Bitmap atlas = new System.Drawing.Bitmap(args[0])) Run(atlas);
            using (System.Drawing.Bitmap atlas = new System.Drawing.Bitmap(args[1])) CheckTidalWave(atlas);
            using (System.Drawing.Bitmap atlas = new System.Drawing.Bitmap(args[2])) SkillSharkTests.Run(atlas);
            using (System.Drawing.Bitmap atlas = new System.Drawing.Bitmap(args[3])) SkillMaelstromTests.Run(atlas);
            SkillDamageTargetTests.Run();
            CheckSkillHud();
        }
        catch (Exception error) { Console.WriteLine("FAIL: " + error); Environment.Exit(1); }
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
    private static void Run(System.Drawing.Bitmap atlas)
    {
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
                Check(SpringPixelMask.Contains(f, px + .5f, py + .5f, 0) == (alpha > 0), "Mask/PNG mismatch");
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
            Check(SpringPixelMask.Contains(f, right + 6f, rightY + .5f, 5f), "Inclusive exact boundary failed");
        }
        Check(!SpringPixelMask.Contains(27, 64, 64, 200), "Final frame has lingering damage");
        Check(!SpringPixelMask.Contains(0, 64, 20, 5), "Startup damages empty upper column");

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

    private static void CheckTidalWave(System.Drawing.Bitmap atlas)
    {
        Check(atlas.Width == 16 * 128 && atlas.Height == 128, "Reference wave layout mismatch");
        for (int f = 0; f < 16; f++) for (int py = 0; py < 128; py++) for (int px = 0; px < 128; px++)
            Check(TidalWavePixelMask.Contains(f, px + .5f, py + .5f, 0) == (atlas.GetPixel(f * 128 + px, py).A > 0), "Wave mask differs from reference PNG");
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
        HashSet<Unit> hits = new HashSet<Unit>();
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
                Check(!TidalWavePixelMask.Contains(frame,64+target.X*facing/scale,112-(target.Y+6)/scale,5/scale),
                    "Large enemy fixture not outside old foot-point detection");
                Map.Targets.Add(target);
                Map.DamageObserver=delegate(Unit victim,int damage){victim.health-=damage;};
                HashSet<Unit> hits=new HashSet<Unit>();HashSet<GameObject> objects=new HashSet<GameObject>();
                // A unit collider returned by the ground query must not cause a
                // terrain hit or stop the wave before the unit damage pass.
                Collider body=new Collider((right+.5f-64)*scale*facing,(112-edgeY-.5f)*scale,true);
                body.ParentComponent=target;Physics.Contacts=new Collider[]{body};
                Check(owner.SkillWaveContact(0,0,facing,scale,frame,hits,objects),"Large enemy collider stopped wave");
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
                    owner.SkillWaveContact(0,0,facing,scale,blockedCase==5?6:frame,new HashSet<Unit>(),objects);
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
                owner.SkillWaveContact(0,0,facing,scale,frame,new HashSet<Unit>(),new HashSet<GameObject>());
                Check(!near.enabled && far.enabled && nearItem.Damaged && !farItem.Damaged && MapController.ObjectHits==1,
                    "Terrain/item five-pixel boundary failed: frame "+frame+" scale "+scale+" facing "+facing);
                near.enabled=true;nearItem.Damaged=false;MapController.ObjectHits=0;
                owner.SkillWaveContact(0,0,facing,scale,6,new HashSet<Unit>(),new HashSet<GameObject>());
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
            Check(owner.SkillWaveContact(0,waterline,facing,scale,frame,new HashSet<Unit>(),new HashSet<GameObject>()),
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
    public class Object { public static void Destroy(Object value) {} }
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
        public Collider Contact;
        public Transform transform=new Transform();public bool activeInHierarchy=true;
        public GameObject(string name) {} public void SetActive(bool value) { activeInHierarchy=value; }
        public T AddComponent<T>() where T:new() { return new T(); }
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
        public T GetComponent<T>() where T:class {return Relay as T;}
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
            if (SharkTerrainEnabled && layer==Map.groundLayerStrict)
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
            List<RaycastHit> result=new List<RaycastHit>();RaycastHit terrain;
            if (Raycast(origin,direction,out terrain,range,layer)) result.Add(terrain);
            if (SharkTerrainEnabled && layer==Map.groundLayerStrict)
            foreach (RaycastHit prop in SharkPropHits)
            {
                if (!prop.collider.enabled || !prop.collider.gameObject.activeInHierarchy) continue;
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
    public GameObject gameObject=new GameObject("Test unit");
}
public class Mook : Unit
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
public class FallingBlock {}
public class CrateBlock : FallingBlock {}
public class BarrelBlock : FallingBlock {}
public class DamageObject
{
    public DamageObject(int damage,DamageType type,float fx,float fy,float x,float y,object sender) {}
}
public class Doodad
{
    public GameObject gameObject=new GameObject("Doodad");
    public float centerX,centerY,width=2,height=2;
    public bool immuneToHeroDamage,CanBeDamagedByMooks=true,isImpenetrable,Damaged;
    public Doodad(float x,float y) {centerX=x;centerY=y;}
    public void DamageOptional(DamageObject damage,ref bool hit) {Damaged=true;hit=true;Map.DoodadHits++;}
}
public class Elevator : Doodad { public Elevator() : base(0,0) {} }
public class GiantElevator : Elevator {}
public class Projectile : Unit
{
    public float projectileSize=1,life=2;public bool Syncronize,NetworkDestroyed,DeathCalled;
    public Projectile() {playerNum=-1;}
    public void DestroyNetworked() {NetworkDestroyed=true;}
    public virtual void Death() {DeathCalled=true;}
}
public class Rocket : Projectile {}
public enum ActionState { Idle,Running,Jumping,ClimbingLadder }
public enum DamageType { Explosion,Crush,Drill }
public class DamageRelay
{
    public Unit unit;public DamageType immunity=DamageType.Drill;
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
        DamageRelay relay=receiver.Contact.Relay;
        if(relay!=null)
        {
            if(type!=DamageType.Explosion)throw new Exception("Boss relay damage type changed");
            Map.Hits[relay.unit]=damage;Map.TotalHits++;Map.LastUpForce=fy;if(Map.DamageObserver!=null)Map.DamageObserver(relay.unit,damage);return;
        }
        if (!receiver.Contact.indestructible) receiver.Contact.enabled=false;
    }
}
public static class Map
{
    public static int groundLayer=1,groundLayerStrict=2,groundAndDamageableObjects=3,DoodadHits;public static List<Unit> Targets=new List<Unit>();
    public static List<Doodad> destroyableDoodads=new List<Doodad>();
    public static Dictionary<Unit,int> Hits=new Dictionary<Unit,int>();public static int TotalHits;public static float LastUpForce;
    public static Action<Unit,int> DamageObserver;
    public static List<Unit> GetUnitsInRange(float xr,float yr,float x,float y,bool dead)
    {
        List<Unit> result=new List<Unit>();
        foreach(Unit unit in Targets)
            if((dead || unit.health>0) && Math.Abs(unit.X-x)<=xr && Math.Abs(unit.Y+6-y)<=yr)result.Add(unit);
        return result;
    }
    public static void KnockAndDamageUnit(Unit source,Unit target,int damage,DamageType kind,float fx,float fy,int direction,bool knock,float x,float y,bool remote)
    { Hits[target]=damage;TotalHits++;LastUpForce=fy;if(DamageObserver!=null)DamageObserver(target,damage); }
    public static void HitProjectiles(int p,int n,DamageType t,float r,float x,float y,float fx,float fy,float time) {}
}
public class SpriteSM
{
    public Vector3 TestOffset;public void SetOffset(Vector3 offset) {TestOffset=offset;}
    public MeshRenderer meshRender=new MeshRenderer();public static int LastFrame;
    public void SetPixelDimensions(int w,int h) {}public void SetSize(float w,float h) {}
    public void SetLowerLeftPixel(int x,int y) {LastFrame=x/128;}
    public void RecalcTexture() {}public void CalcUVs() {}public void UpdateUVs() {}
}
public class MeshRenderer { public object material; public Material sharedMaterial {get{return material as Material;}set{material=value;}} public bool enabled=true; }
public class Material { public string name; }
namespace Rogueforce { public static class ValueOrchestrator {public static int GetModifiedDamage(int n,int p) {return n;}} }
namespace BroMakerLib { public static class ResourcesController {
    private static Dictionary<string,Material> materials=new Dictionary<string,Material>();
    public static Material GetMaterial(string p,string f) {if(!materials.ContainsKey(f))materials[f]=new Material{name=f};return materials[f];}
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

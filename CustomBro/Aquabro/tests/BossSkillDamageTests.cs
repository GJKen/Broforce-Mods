using System;
using System.Collections.Generic;
using AquabroMod;
using UnityEngine;

internal static class BossSkillDamageTests
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static void Reset()
    {
        BossDamageTestStubs.Reset(); Map.Targets.Clear(); Map.Hits.Clear(); Map.TotalHits = 0;
        Map.destroyableDoodads.Clear(); Map.DamageObserver = null; MapController.ObjectHits = 0;
        Physics.Contacts = new Collider[0]; Physics.SharkPropHits = new RaycastHit[0];
        Physics.Blocked = Physics.SharkTerrainEnabled = false; Physics.BlockedBelowY = float.NegativeInfinity;
    }
    private static int Frame(int kind) { return kind == 0 ? 4 : kind == 1 ? 12 : kind == 2 ? 22 : 10; }
    private static Vector2 Point(int kind, float facing, float scale)
    {
        PixelMask mask = kind == 0 ? TidalWavePixelMask.Mask : kind == 1 ? SharkPixelMask.Mask :
            kind == 2 ? MaelstromPixelMask.Mask : SpringPixelMask.Mask;
        for (int py = 20; py < 100; py++) for (int px = 0; px < 128; px++)
            if (Math.Abs(px - 64) > 18 && mask.Contains(Frame(kind), px + .5f, py + .5f, 0))
                return new Vector2((px + .5f - 64) * facing * scale, (112 - py - .5f) * scale);
        throw new Exception("No skill pixel");
    }
    private static Collider Part(object piece, Vector2 point)
    {
        Collider collider = new Collider(point.x, point.y, true);
        collider.gameObject.SetComponent(piece); return collider;
    }
    private static void Cast(Aquabro owner, int kind, float facing, float scale, HashSet<object> hits, SpringDamageState spring)
    {
        if (kind == 0) owner.SkillWaveContact(0, 0, facing, scale, Frame(kind), hits, new HashSet<GameObject>());
        if (kind == 1)
        {
            owner.SkillSharkObjects(0, 0, facing, scale, Frame(kind), new HashSet<GameObject>());
            owner.SkillSharkBlast(0, 0, facing, scale, Frame(kind), hits);
        }
        if (kind == 2) owner.SkillMaelstromPull(0, 0, facing, scale, Frame(kind), .016f, true, new HashSet<object>());
        if (kind == 3) owner.SkillSpringBlast(0, 0, facing, scale, Frame(kind), spring, 0);
    }
    internal static void Run()
    {
        CheckWaveBossContact();
        CheckWaveResume();
        CheckWaveBodyVehicles();
        CheckSawBlades();
        CheckGenericReceivers();
        Aquabro owner = new Aquabro();
        for (int kind = 0; kind < 4; kind++)
        foreach (float facing in new float[] { -1, 1 })
        foreach (float scale in new float[] { .65f, 1f })
        for (int family = 0; family < 11; family++)
        {
            Reset(); Vector2 point = Point(kind, facing, scale);
            object receiver = family >= 9 ? (object)new BossSatanArm() : family >= 7 ? (object)new BossSatanFloatingHead() : family >= 5 ? (object)new BossSlugFace() :
                family == 1 ? (object)new BossBlockPiece() : family == 2 ? (object)new BossBlockWeapon() : new BossPiece();
            Collider a = Part(receiver, point), b = Part(receiver, point);
            if (family == 3 || family == 4 || family == 6 || family == 8 || family == 10)
            {
                a = Part(new object(), point); a.Relay = new DamageRelay();
                if (family != 4)
                {
                    a.Relay.damageGameObject = b.gameObject;
                    if (family >= 6)
                    {
                        Collider other = Part(new object(), point);
                        other.Relay = new DamageRelay(); other.Relay.damageGameObject = b.gameObject;
                        b = other;
                    }
                }
                else
                { receiver = new BossBlockWeapon(); a.Relay.bossWeapon = (BossBlockWeapon)receiver; b = a; }
            }
            Physics.Contacts = new Collider[] { a, b };
            HashSet<object> hits = new HashSet<object>(); SpringDamageState spring = new SpringDamageState();
            Cast(owner, kind, facing, scale, hits, spring);
            int expected = kind == 0 ? 26 : kind == 1 ? 28 : kind == 2 ? 3 : 4;
            Check(BossDamageTestStubs.Requests == 1 && BossDamageTestStubs.Damage[receiver] == expected,
                "Non-Unit family missed/wrong damage/duplicate scene branch: kind=" + kind + " family=" + family);
            Check(BossDamageTestStubs.LastForceX == 0 && BossDamageTestStubs.LastForceY == 0, "Boss was launched like infantry");
            if (kind == 2)
            {
                owner.SkillMaelstromFinish(0, 0, facing, scale, Frame(kind), new HashSet<object>(), hits);
                owner.SkillMaelstromFinish(0, 0, facing, scale, Frame(kind), new HashSet<object>(), hits);
                Check(BossDamageTestStubs.Requests == 2 && BossDamageTestStubs.Damage[receiver] == 12, "Boss finisher missing/repeated");
            }
            else
            {
                Cast(owner, kind, facing, scale, hits, spring);
                Check(BossDamageTestStubs.Requests == 1, "Boss recounted on repeated tick");
            }

            Reset(); BossPiece left = new BossPiece(), right = new BossPiece(), independent = new BossPiece();
            left.connectedPieces.Add(right); right.connectedPieces.Add(left);
            Physics.Contacts = new Collider[] { Part(left, point), Part(right, point), Part(independent, point) };
            Cast(owner, kind, facing, scale, new HashSet<object>(), new SpringDamageState());
            Check(BossDamageTestStubs.Requests == 2 && BossDamageTestStubs.Hits[left] == 1 &&
                BossDamageTestStubs.Hits[right] == 1 && BossDamageTestStubs.Hits[independent] == 1, "Connected/independent groups confused");

            for (int filter = 0; filter < 6; filter++)
            {
                Reset(); BossPiece boss = new BossPiece(); Collider part = Part(boss, point);
                if (filter == 0) boss.immortal = true;
                if (filter == 1) boss.playerNum = owner.playerNum;
                if (filter == 2) Physics.Blocked = true;
                if (filter == 3) owner.IsMine = false;
                if (filter == 4) { part.Relay = new DamageRelay(); part.Relay.damageGameObject = Part(boss, point).gameObject;
                    part.Relay.ignoreDamageWhileColliderActive = new Collider(0, 0, true); }
                if (filter == 5) { part.Relay = new DamageRelay(); part.Relay.damageGameObject = Part(boss, point).gameObject;
                    part.Relay.MarkDestroyed(true); }
                Physics.Contacts = new Collider[] { part }; hits = new HashSet<object>(); spring = new SpringDamageState();
                Cast(owner, kind, facing, scale, hits, spring);
                Check(BossDamageTestStubs.Requests == 0, "Boss phase/friendly/wall/owner/relay bypass " + filter);
                boss.immortal = false; boss.playerNum = -1; Physics.Blocked = false; owner.IsMine = true;
                if (part.Relay != null) { part.Relay.MarkDestroyed(false);
                    if (part.Relay.ignoreDamageWhileColliderActive != null) part.Relay.ignoreDamageWhileColliderActive.enabled = false; }
                Cast(owner, kind, facing, scale, hits, spring);
                Check(BossDamageTestStubs.Requests == 1, "Rejected boss contact consumed budget");
            }
            Reset(); BossBlockPiece blocker = new BossBlockPiece(), weak = new BossBlockPiece();
            weak.ignoreDamageWhileOtherPieceAlive = blocker;
            Physics.Contacts = new Collider[] { Part(weak, point) }; hits = new HashSet<object>(); spring = new SpringDamageState();
            Cast(owner, kind, facing, scale, hits, spring);
            Check(BossDamageTestStubs.Requests == 0, "Closed native weak point bypassed");
            blocker.BossBlockHealth = 0;
            Cast(owner, kind, facing, scale, hits, spring);
            Check(BossDamageTestStubs.Requests == 1, "Opening weak point lost its budget");
        }
        for (int kind = 0; kind < 4; kind++)
        for (int filter = 0; filter < 6; filter++)
        for (int family = 0; family < 3; family++)
        {
            Reset(); BossSlugFace face = new BossSlugFace();
            BossSatanFloatingHead head = new BossSatanFloatingHead(); BossSatanArm arm = new BossSatanArm();
            object key = family == 0 ? (object)face : family == 1 ? (object)head : arm;
            Collider receiver = Part(key, Point(kind, 1, 1));
            Collider contact = Part(new object(), Point(kind, 1, 1));
            contact.Relay = new DamageRelay(); contact.Relay.damageGameObject = receiver.gameObject;
            if (filter == 0) { face.MarkDead(true); head.MarkDead(true); arm.MarkDead(true); }
            if (filter == 1) { face.health = head.health = arm.health = 0; }
            if (filter == 2) contact.Relay.MarkDestroyed(true);
            if (filter == 3) contact.Relay.ignoreDamageWhileOtherColliderActive = new Collider(0, 0, true);
            if (filter == 4) contact.enabled = false;
            if (filter == 5) contact.Relay.ignoreDamageWhileColliderActive = new Collider(0, 0, true);
            Physics.Contacts = new Collider[] { contact };
            HashSet<object> hits = new HashSet<object>(); SpringDamageState spring = new SpringDamageState();
            Cast(owner, kind, 1, 1, hits, spring);
            Check(BossDamageTestStubs.Requests == 0 && hits.Count == 0, "Closed/dead boss consumed a hit: " + family + ":" + filter);
            face.MarkDead(false); head.MarkDead(false); arm.MarkDead(false);
            face.health = head.health = arm.health = 1000; contact.Relay.MarkDestroyed(false); contact.enabled = true;
            contact.Relay.ignoreDamageWhileOtherColliderActive = null;
            contact.Relay.ignoreDamageWhileColliderActive = null;
            // damageGameObject forwarding ignores relay.immunity in the native game.
            contact.Relay.immunity = DamageType.Explosion;
            Cast(owner, kind, 1, 1, hits, spring);
            Check(BossDamageTestStubs.Requests == 1, "Reopened boss lost hit or native relay semantics");
            if (family != 0)
            {
                CollisionDamageTarget target = CollisionDamageTarget.Find(contact);
                Check(!target.CanDamage(owner.playerNum, DamageType.Unholy), "Satan Unholy immunity bypassed");
            }
        }
        for (int kind = 0; kind < 4; kind++)
        {
            Reset(); Vector2 point = Point(kind, 1, 1);
            BossSatanFloatingHead head = new BossSatanFloatingHead();
            Physics.Contacts = new Collider[8]; Physics.Contacts[0] = Part(head, point); Physics.Contacts[1] = Part(head, point);
            for (int armIndex = 0; armIndex < 6; armIndex++) Physics.Contacts[armIndex + 2] = Part(new BossSatanArm(), point);
            Cast(owner, kind, 1, 1, new HashSet<object>(), new SpringDamageState());
            Check(BossDamageTestStubs.Requests == 7 && BossDamageTestStubs.Hits[head] == 1,
                "Satan head/arms incorrectly share the whole boss hit budget");
        }
        foreach (int fps in new int[] { 30, 60, 120 })
        foreach (int kind in new int[] { 2, 3 })
        for (int family = 0; family < 4; family++)
        {
            Reset(); BossPiece a = new BossPiece(), b = new BossPiece(); a.connectedPieces.Add(b); b.connectedPieces.Add(a);
            bool single = family != 0;
            object receiver = family == 1 ? (object)new BossSlugFace() : family == 2 ? (object)new BossSatanFloatingHead() : family == 3 ? (object)new BossSatanArm() : a;
            Collider part = Part(receiver, new Vector2(0, 48));
            Bounds bounds = new Bounds(); bounds.min = new Vector3(-256, 1, 0); bounds.max = new Vector3(256, 128, 0);
            part.bounds = bounds; Collider other = Part(single ? receiver : b, new Vector2(0, 48)); other.bounds = bounds;
            Physics.Contacts = new Collider[] { part, other };
            SpringDamageState spring = new SpringDamageState(); List<AquabroSkillVisual> effects = new List<AquabroSkillVisual>();
            for (int column = 0; column < (kind == 3 ? 7 : 1); column++)
                effects.Add(new AquabroSkillVisual(owner, kind, 0, 0, 1, kind == 3 ? 1.4f : 4.2f, column * .07f, 1, null, spring));
            for (int tick = 0; tick < fps * 4.3f; tick++)
                foreach (AquabroSkillVisual effect in effects) { effect.Tick(1f / fps); effect.Tick(0); }
            Check(BossDamageTestStubs.Damage[receiver] == (kind == 3 ? 28 : 36) &&
                BossDamageTestStubs.Hits[receiver] == (kind == 3 ? 7 : 10) &&
                (single || BossDamageTestStubs.Damage[b] == BossDamageTestStubs.Damage[a]),
                "Periodic boss budget mismatch: family=" + family + " kind=" + kind + " fps=" + fps);
            foreach (AquabroSkillVisual effect in effects) effect.Dispose();
        }
        Reset();
        Console.WriteLine("PASS non-Unit boss skills: four skills; direct/relayed BossPiece, BossBlockPiece, BossBlockWeapon, BossSlugFace, BossSatanFloatingHead, BossSatanArm; independent head and six arms; armour/stages/walls/owner/dead/Unholy; no scene duplicates or launch; spring 28 and maelstrom 36 at 30/60/120 FPS");
    }

    private static void CheckWaveBossContact()
    {
        WaveDamageState cooldown = new WaveDamageState();
        object firstTarget = new object(), otherTarget = new object();
        Check(cooldown.TakeDamage(firstTarget, 0f) == 2 && cooldown.TakeDamage(firstTarget, .119f) == 0 &&
            cooldown.TakeDamage(firstTarget, .12f) == 2, "Wave actual-time cooldown boundary changed");
        Check(cooldown.TryHit(otherTarget, 0f) && cooldown.TakeDamage(otherTarget, .01f) == 0 &&
            cooldown.TakeDamage(otherTarget, .12f) == 2, "Wave object cooldown no longer shares damage gate");
        WaveDamageState budget = new WaveDamageState();
        BossPiece linked = new BossPiece(), other = new BossPiece();
        linked.connectedPieces.Add(other); other.connectedPieces.Add(linked);
        int sum = 0;
        for (int i = 0; i < 100; i++) sum += budget.TakeDamage(i % 2 == 0 ? linked : other, i * .2f);
        Check(sum == 26 && budget.TakeDamage(other, 100f) == 0, "Linked boss exceeded entire-cast 26 damage budget");
        foreach (int fps in new int[] { 30, 60, 120 })
        foreach (float facing in new float[] { -1, 1 })
        for (int family = 0; family < 12; family++)
        {
            Reset();
            Aquabro owner = new Aquabro();
            object boss = family == 0 ? (object)new BossPiece() : family == 1 ? (object)new BossBlockPiece() :
                family == 2 ? (object)new BossBlockWeapon() : family == 3 ? (object)new BossSlugFace() :
                family == 4 ? (object)new BossSatanFloatingHead() : family == 5 ? (object)new BossSatanArm() :
                family == 6 ? (object)new Tank() : family == 7 ? (object)new TankWeapon() :
                family == 8 ? (object)new Mech() : family == 9 ? (object)new MechWeapon() : family == 10 ? (object)new AlienSandWorm() : new SatanMiniboss();
            Unit unit = boss as Unit;
            if (unit != null) { unit.health=1000; unit.playerNum=-1; Map.Targets.Add(unit); }
            // A tank-sized solid face, present from the first step.
            Collider a = Part(boss, new Vector2(facing * 32, 48));
            a.bounds = new Bounds();
            a.bounds.min = new Vector3(facing > 0 ? 12 : -80, 1, 0);
            a.bounds.max = new Vector3(facing > 0 ? 80 : -12, 100, 0);
            a.bounds.center = new Vector3(facing * 46, 50, 0);
            Collider b = Part(boss, new Vector2(facing * 32, 48)); b.bounds = a.bounds;
            Physics.Contacts = new Collider[] { a, b };
            AquabroSkillVisual wave = new AquabroSkillVisual(owner, 0, 0, 0, facing, 2f, 0, .85f);
            float stoppedX = 0; bool stopped = false; int previous = 0; float lastHit = -1;
            for (int frame = 0; frame < fps * 2 + 2; frame++)
            {
                float time = (frame + 1f) / fps;
                bool alive = wave.Tick(1f / fps);
                bool blocked = (bool)typeof(AquabroSkillVisual).GetField("tidalBlocked",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(wave);
                float x = (float)typeof(AquabroSkillVisual).GetField("x",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(wave);
                if (blocked && !stopped) { stopped = true; stoppedX = x; }
                if (stopped) Check(Math.Abs(x - stoppedX) < .0001f, "Blocked wave passed through tank");
                if (BossDamageTestStubs.Requests != previous)
                {
                    Check(BossDamageTestStubs.Requests == previous + 1, "Multi-collider/substep duplicated wave pulse");
                    Check(lastHit < 0 || time - lastHit >= .1199f, "Boss pulse depends on FPS");
                    Check(time < 1.501f, "Dissolving wave still damages boss");
                    previous = BossDamageTestStubs.Requests; lastHit = time;
                    Check(BossDamageTestStubs.Damage[boss] <= 26, "Wave exceeded 26 total damage: " + family);
                }
                if (time < 1.99f) Check(alive, "Contact shortened wave lifetime");
            }
            Check(stopped && previous >= 9 && lastHit > 1.3f, "Wave did not attack through remaining dash duration");
            Check(BossDamageTestStubs.Damage[boss] == previous * 2 && Map.TotalHits == 0,
                "Body pass bypassed periodic boss budget: " + family);
            wave.Dispose();
        }
        Console.WriteLine("PASS wave boss contact: 12 boss/vehicle receiver families; both directions; 30/60/120 FPS; pinned position; 2 damage pulses; <=26 total incl linked parts; body/collider dedup; full dash and dissolve");
        Reset();
    }

    private static void CheckSawBlades()
    {
        Aquabro owner=new Aquabro();
        for(int kind=0;kind<4;kind++)
        foreach(float facing in new float[]{-1,1})
        foreach(float scale in new float[]{.65f,1f})
        {
            Reset();
            SawBlade saw=new SawBlade();Collider contact=Part(saw,Point(kind,facing,scale));
            Physics.Contacts=new Collider[]{contact};
            HashSet<object> hits=new HashSet<object>();SpringDamageState spring=new SpringDamageState();
            Cast(owner,kind,facing,scale,hits,spring);
            Check(BossDamageTestStubs.Requests==1 && BossDamageTestStubs.Damage[saw]==(kind==0?26:kind==1?28:kind==2?3:4),
                "Skill omitted unregistered SawBlade "+kind);
            if(kind<2)Check(saw.Detached && !saw.Dead,"Fixed blade skipped native detach stage");

            Reset();saw=new SawBlade {Detached=true};contact=Part(saw,Point(kind,facing,scale));
            Physics.Contacts=new Collider[]{contact};hits=new HashSet<object>();spring=new SpringDamageState();
            Cast(owner,kind,facing,scale,hits,spring);
            Check(BossDamageTestStubs.Requests==0,"Detached blade invulnerability bypassed");
            saw.invulnerabilityTime=-.01f;
            Cast(owner,kind,facing,scale,hits,spring);
            Check(BossDamageTestStubs.Requests==1,"Invulnerable saw consumed skill budget");
            if(kind==0 || kind==1)Check(saw.Dead,"Vulnerable detached saw not destroyed");

            foreach(int filter in new int[]{0,1,2,3,4,5})
            {
                Reset();saw=new SawBlade();contact=Part(saw,Point(kind,facing,scale));
                if(filter==0)saw.hidden=true;
                if(filter==1)saw.immuneToHeroDamage=true;
                if(filter==2)contact.enabled=false;
                if(filter==3)contact.isTrigger=true;
                if(filter==4)Physics.Blocked=true;
                if(filter==5)owner.IsMine=false;
                Physics.Contacts=new Collider[]{contact};
                Cast(owner,kind,facing,scale,new HashSet<object>(),new SpringDamageState());
                Check(BossDamageTestStubs.Requests==0,"Saw filter bypass "+kind+":"+filter);
                owner.IsMine=true;
            }
        }
        Reset();
        Console.WriteLine("PASS four skills SawBlade: unregistered Doodad/native Damage; fixed detach; detached invulnerability then damage; filters/walls/owner; facing/scale");
    }

    private static void CheckWaveBodyVehicles()
    {
        foreach (int fps in new int[] { 30, 60, 120 })
        foreach (float facing in new float[] { -1, 1 })
        foreach (bool kill in new bool[] { false, true })
        foreach (Unit target in new Unit[] { new MookTruck(), new MookAPC(), new DrillCarrier(), new MookDumpTruck(),
            new MookArtilleryTruck(), new Mookopter(), new MammothKopter(), new MookVehicleDigger(), new MookBigGuy(), new MookGrenadier(), new Mech() })
        {
            Reset();
            // Live truck body: Y=168, top=190, while the wave's waterline is 184.
            // Its Platform child is deliberately absent from the terrain query.
            target.X = facing * 60; target.Y=-16; target.width=16; target.height=target.headHeight=22;
            bool fixedDamageMook = target is MookBigGuy || target is MookGrenadier;
            int initialHealth = kill ? 12 : 1000;
            target.playerNum=-1; target.health=initialHealth; Map.Targets.Add(target);
            int total=0; float last=-1; float time=0;
            Map.DamageObserver=delegate(Unit victim,int damage)
            {
                if (fixedDamageMook) Check(last < 0 && damage==26, "Heavy Mook did not receive one fixed 26-damage hit");
                else Check(damage==2 && (last<0 || time-last>=.1199f), "Body vehicle pulse duplicated");
                total+=damage; last=time; victim.health-=damage;
            };
            AquabroSkillVisual wave=new AquabroSkillVisual(new Aquabro(),0,0,0,facing,2f,0,.85f);
            System.Reflection.FieldInfo position=typeof(AquabroSkillVisual).GetField("x",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            bool blocked=false;
            for(int tick=0;tick<fps*2+1;tick++)
            {
                time=(tick+1f)/fps; wave.Tick(1f/fps);
                if((bool)typeof(AquabroSkillVisual).GetField("tidalBlocked",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(wave))blocked=true;
            }
            float end=(float)position.GetValue(wave)*facing;
            if (fixedDamageMook)
                Check(total==26 && target.health==initialHealth-26 && end>60,
                    "Water wall did not pass through after its fixed Mook hit: "+target.GetType().Name);
            else
            {
                Check(blocked && total>0 && total<=26, "Platform-only vehicle missed or over budget: "+target.GetType().Name);
                if(kill)Check(target.health<=0 && end>60, "Killed body vehicle failed to release wave");
                else Check(end<60, "Living body vehicle let wave through: "+target.GetType().Name);
            }
            wave.Dispose();
        }
        Reset();
        Console.WriteLine("PASS wave body vehicles: live truck geometry; vehicles keep periodic damage; Big Guy and Grenadier take one 26-damage hit and never block; both directions; 30/60/120 FPS");
    }

    private static void CheckWaveResume()
    {
        foreach (int fps in new int[] { 30, 60, 120 })
        foreach (float facing in new float[] { -1, 1 })
        foreach (bool removeCollider in new bool[] { false, true })
        {
            Reset();
            BossPiece boss = new BossPiece();
            Collider contact = Part(boss, new Vector2(facing * 32, 48));
            contact.bounds = new Bounds();
            contact.bounds.min = new Vector3(facing > 0 ? 12 : -80, 1, 0);
            contact.bounds.max = new Vector3(facing > 0 ? 80 : -12, 100, 0);
            Physics.Contacts = new Collider[] { contact };
            AquabroSkillVisual wave = new AquabroSkillVisual(new Aquabro(), 0, 0, 0, facing, 2f, 0, .85f);
            System.Reflection.FieldInfo position = typeof(AquabroSkillVisual).GetField("x",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            for (int tick = 0; tick < fps / 2; tick++) wave.Tick(1f / fps);
            float stopped = (float)position.GetValue(wave);
            if (removeCollider) contact.enabled = false;
            else boss.health = 0; // A killed boss may retain its collider temporarily.
            wave.Tick(0);
            Check(Math.Abs((float)position.GetValue(wave) - stopped) < .0001f, "Paused wave moved after kill");
            wave.Tick(1f / fps);
            float resumed = (float)position.GetValue(wave);
            Check(Math.Abs(resumed - stopped - facing * 160f / fps) < .001f, "Wave failed to resume at original speed");
            for (int tick = 0; tick < fps * 1.1f; tick++) wave.Tick(1f / fps);
            float end = (float)position.GetValue(wave);
            wave.Tick(.1f);
            Check(Math.Abs((float)position.GetValue(wave) - end) < .0001f, "Resume extended dash into dissolve");
            Check(!wave.Tick(.5f), "Resume restarted skill lifetime");
            Check(BossDamageTestStubs.Damage[boss] <= 26, "Resume reset damage budget");
            wave.Dispose();
        }
        Reset();
        Console.WriteLine("PASS wave resume: kill with live collider / collider removal; both directions; 30/60/120 FPS; pause; original speed; unchanged dash/lifetime/budget");
    }

    private static void CheckGenericReceivers()
    {
        Aquabro owner=new Aquabro();
        for(int kind=0;kind<4;kind++)
        foreach(float facing in new float[]{-1,1})
        for(int family=0;family<3;family++)
        {
            Reset();Vector2 point=Point(kind,facing,1);
            UnlistedDamageReceiver unknown=family==2 ? new UnlistedDerivedReceiver() : new UnlistedDamageReceiver();
            Collider receiver=Part(unknown,point),a=Part(new object(),point),b=Part(new object(),point);
            if(family==0) {a.gameObject=receiver.gameObject;b.gameObject=receiver.gameObject;}
            else
            {
                a.Relay=new DamageRelay {damageGameObject=receiver.gameObject};
                b.Relay=new DamageRelay {damageGameObject=family==2 ? a.gameObject : receiver.gameObject};
            }
            Physics.Contacts=new Collider[]{b,a,receiver};
            HashSet<object> hits=new HashSet<object>();SpringDamageState spring=new SpringDamageState();
            Cast(owner,kind,facing,1,hits,spring);
            int expected=kind==0 ? 26 : kind==1 ? 28 : kind==2 ? 3 : 4;
            Check(BossDamageTestStubs.Requests==1 && unknown.health==1000-expected &&
                BossDamageTestStubs.Damage[receiver.gameObject]==expected,"Unlisted receiver missed/duplicated "+kind+":"+family);
            Check(BossDamageTestStubs.LastForceX==0 && BossDamageTestStubs.LastForceY==0,"Generic scene receiver launched like infantry");
            Cast(owner,kind,facing,1,hits,spring);
            Check(BossDamageTestStubs.Requests==(kind==2 ? 2 : 1),"Generic once/pulse ledger changed");

            Reset();receiver=Part(new UnlistedDamageReceiver(),point);
            a=Part(new object(),point);a.Relay=new DamageRelay {damageGameObject=receiver.gameObject};
            b=Part(new object(),point);b.Relay=new DamageRelay {damageGameObject=a.gameObject};
            Collider gate=new Collider(0,0,true);a.Relay.ignoreDamageWhileOtherColliderActive=gate;
            Physics.Contacts=new Collider[]{b};hits=new HashSet<object>();spring=new SpringDamageState();
            Cast(owner,kind,facing,1,hits,spring);
            Check(BossDamageTestStubs.Requests==0 && hits.Count==0,"Nested relay gate consumed budget");
            gate.enabled=false;Cast(owner,kind,facing,1,hits,spring);
            Check(BossDamageTestStubs.Requests==1,"Opening nested relay lost hit");

            Reset();a=Part(new object(),point);b=Part(new object(),point);
            a.Relay=new DamageRelay {damageGameObject=b.gameObject};b.Relay=new DamageRelay {damageGameObject=a.gameObject};
            Physics.Contacts=new Collider[]{a,b};Cast(owner,kind,facing,1,new HashSet<object>(),new SpringDamageState());
            Check(BossDamageTestStubs.Requests==0 && MapController.ObjectHits==0,"Cyclic relay reached scenery/native damage");

            Reset();unknown=new UnlistedDamageReceiver {closed=true};receiver=Part(unknown,point);
            Physics.Contacts=new Collider[]{receiver};hits=new HashSet<object>();spring=new SpringDamageState();
            Cast(owner,kind,facing,1,hits,spring);
            Check(BossDamageTestStubs.Requests==1 && unknown.health==1000,"Native private phase gate bypassed");
        }
        for(int kind=0;kind<4;kind++)
        for(int filter=0;filter<6;filter++)
        {
            Reset();Collider receiver=Part(new UnlistedDamageReceiver(),Point(kind,1,1));
            if(filter==0)owner.IsMine=false;
            if(filter==1)Physics.Blocked=true;
            if(filter==2)receiver.isTrigger=true;
            if(filter==3)receiver.enabled=false;
            if(filter==4)receiver.gameObject.activeInHierarchy=false;
            if(filter==5)receiver.bounds=new Bounds(0,-8);
            Physics.Contacts=new Collider[]{receiver};
            Cast(owner,kind,1,1,new HashSet<object>(),new SpringDamageState());
            Check(BossDamageTestStubs.Requests==0,"Generic receiver filter bypass "+kind+":"+filter);
            owner.IsMine=true;
        }
        foreach(int fps in new int[]{30,60,120})
        foreach(int kind in new int[]{2,3})
        {
            Reset();Collider receiver=Part(new UnlistedDerivedReceiver(),new Vector2(0,48));
            Bounds bounds=new Bounds();bounds.min=new Vector3(-256,1,0);bounds.max=new Vector3(256,128,0);receiver.bounds=bounds;
            Collider relay=Part(new object(),new Vector2(0,48));relay.bounds=bounds;
            relay.Relay=new DamageRelay {damageGameObject=receiver.gameObject};Physics.Contacts=new Collider[]{relay,receiver};
            SpringDamageState state=new SpringDamageState();List<AquabroSkillVisual> effects=new List<AquabroSkillVisual>();
            for(int column=0;column<(kind==3 ? 7 : 1);column++)
                effects.Add(new AquabroSkillVisual(owner,kind,0,0,1,kind==3 ? 1.4f : 4.2f,column*.07f,1,null,state));
            for(int tick=0;tick<fps*4.3f;tick++)foreach(AquabroSkillVisual effect in effects){effect.Tick(1f/fps);effect.Tick(0);}
            Check(BossDamageTestStubs.Damage[receiver.gameObject]==(kind==3 ? 28 : 36),"Unlisted periodic receiver budget changed");
            foreach(AquabroSkillVisual effect in effects)effect.Dispose();
        }
        Reset();
        Console.WriteLine("PASS generic collision skills: unlisted/private inherited Damage; direct/multi-collider/multi-relay/chains; native phase gates; nested protection; cycles; no scenery duplicates; spring 28/maelstrom 36 at 30/60/120 FPS");
    }
}

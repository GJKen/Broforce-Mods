using System;
using System.Collections.Generic;

internal static class BossWaterDamageTests
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static void Reset() { Physics.Walls.Clear(); Map.units.Clear(); Map.damageableProjectiles.Clear(); BossDamageTestStubs.Reset(); }
    private static Collider Part(object piece, float x, float y)
    {
        Collider collider = new Collider(x - 1, x + 1); collider.bottom = y - 1; collider.top = y + 1;
        collider.gameObject.SetComponent(piece); return collider;
    }
    internal static void Run()
    {
        CheckGenericReceivers();
        foreach (bool charged in new bool[] { false, true })
        foreach (int direction in new int[] { -1, 1 })
        for (int family = 0; family < 4; family++)
        {
            Reset(); BossPiece a = new BossPiece(), b = new BossPiece(); a.connectedPieces.Add(b); b.connectedPieces.Add(a);
            bool slug = family != 0;
            object receiver = family == 1 ? (object)new BossSlugFace() : family == 2 ? (object)new BossSatanFloatingHead() : family == 3 ? (object)new BossSatanArm() : a;
            Collider contact = Part(receiver, 0, 0), second = Part(slug ? receiver : b, 0, 0);
            if (slug)
            {
                Collider relay = Part(new object(), 0, 0);
                relay.gameObject.SetComponent(new DamageRelay { damageGameObject = contact.gameObject });
                Collider other = Part(new object(), 0, 0);
                other.gameObject.SetComponent(new DamageRelay { damageGameObject = contact.gameObject });
                contact = relay; second = other;
            }
            Physics.Walls.Add(contact); Physics.Walls.Add(second);
            Aquabro owner = new Aquabro(); owner.X = -40 * direction;
            PredabroSpear shot = new PredabroSpear { X = -16 * direction, Y = 0, xI = (charged ? 664 : 400) * direction,
                playerNum = 0, damageInternal = charged ? 20 : 13 };
            Aquabro.SweepImpact(shot, owner);
            Check(shot.GetComponent<AquabroWaterImpact>() != null && BossDamageTestStubs.Requests == 1 &&
                BossDamageTestStubs.Damage[receiver] == (charged ? 20 : 13) &&
                BossDamageTestStubs.Hits[slug ? receiver : b] == 1,
                "Actual spear sweep missed boss, used terrain damage, or counted direct/ground/splash twice");
            if (charged) for (int frame = 0; frame < 6; frame++) AquabroWaterEffect.Last.Tick(.1f);
            Check(BossDamageTestStubs.Requests == 1, "Animated splash repeats connected boss damage");
            Check(BossDamageTestStubs.LastForceX == 0 && BossDamageTestStubs.LastForceY == 0, "Spear launches boss");

            Reset(); BossPiece behind = new BossPiece(); Physics.Walls.Add(Part(behind, 0, 0));
            Collider wall = new Collider(-25 * direction, -20 * direction); Physics.Walls.Add(wall);
            shot = new PredabroSpear { X = -16 * direction, xI = (charged ? 664 : 400) * direction, playerNum = 0 };
            Aquabro.SweepImpact(shot, owner);
            Check(BossDamageTestStubs.Requests == 0, "Spear or splash crossed a real wall to reach boss");
        }
        for (int frame = 0; frame < 6; frame++)
        {
            Reset(); float px = 0, py = 0, nearest = float.PositiveInfinity;
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                float wx = (x + .5f - 32) * 72f / 64f, wy = (32 - y - .5f) * 72f / 64f;
                float squared = wx * wx + wy * wy;
                if (squared >= nearest || !AquabroMod.ChargedWaterPixelMask.IsVisible(frame, x, y)) continue;
                px = wx; py = wy; nearest = squared;
            }
            BossPiece boss = new BossPiece(); Collider sample = Part(boss, px, py);
            sample.left = px - .01f; sample.right = px + .01f; sample.bottom = py - .01f; sample.top = py + .01f;
            Physics.Walls.Add(sample);
            HashSet<object> affected = new HashSet<object>(); Aquabro owner = new Aquabro();
            Aquabro.ApplyWaterImpactUnits(owner, true, 0, 0, 664, 0, null, affected, frame);
            Aquabro.ApplyWaterImpactUnits(owner, true, 0, 0, 664, 0, null, affected, frame);
            bool damaging = nearest <= 25.2f * 25.2f;
            Check(BossDamageTestStubs.Requests == (damaging ? 1 : 0) &&
                (!damaging || BossDamageTestStubs.Damage[boss] == 14), "Current visible water frame missed/doubled boss or damaged outer ring");
        }
        Reset(); BossPiece phase = new BossPiece(); phase.immortal = true; Collider phasePart = Part(phase, 3, 0);
        Physics.Walls.Add(phasePart); HashSet<object> hits = new HashSet<object>(); Aquabro hero = new Aquabro();
        Aquabro.ApplyWaterImpactUnits(hero, false, 0, 0, 400, 13, phasePart, hits, 0);
        Check(BossDamageTestStubs.Requests == 0 && hits.Count == 0, "Invulnerable cockpit consumed water hit");
        phase.immortal = false;
        Aquabro.ApplyWaterImpactUnits(hero, false, 0, 0, 400, 13, phasePart, hits, 0);
        Check(BossDamageTestStubs.Damage[phase] == 13, "Opening cockpit lost direct damage");

        Reset(); BossPiece large = new BossPiece(); Collider edge = Part(large, 0, 0);
        edge.left = 10; edge.right = 500; edge.bottom = -4; edge.top = 4; Physics.Walls.Add(edge);
        Aquabro.ApplyWaterImpactUnits(hero, false, 0, 0, 400, 0, null, new HashSet<object>(), 0);
        Check(BossDamageTestStubs.Damage[large] == 8, "Large boss edge ignored because its center is outside splash");
        Reset(); BossPiece remote = new BossPiece(); Collider remotePart = Part(remote, 0, 0); Physics.Walls.Add(remotePart);
        Aquabro.ApplyWaterImpactUnits(new Aquabro { IsMine = false }, true, 0, 0, 664, 20, remotePart, new HashSet<object>(), 0);
        Check(BossDamageTestStubs.Requests == 0, "Remote client repeated boss water damage");
        Reset();
        Console.WriteLine("PASS non-Unit boss water: normal/charged full spear sweeps; both directions; direct max damage; connected parts and multiple slug/Satan head/arm relays; no terrain/splash duplicates; six current pixel masks; native immortality; large edges; walls; ownership");
    }

    private static void CheckGenericReceivers()
    {
        foreach(bool charged in new bool[]{false,true})
        foreach(bool chain in new bool[]{false,true})
        {
            Reset();Unit unit=new Unit {X=0,Y=-6};Map.units.Add(unit);
            Collider inner=Part(new object(),0,0);inner.relay=new DamageRelay {unit=unit};
            Collider outer=Part(new object(),0,0);
            outer.relay=chain ? new DamageRelay {damageGameObject=inner.gameObject} : new DamageRelay {unit=unit};
            Collider gate=new Collider(0,1);
            if(chain)inner.relay.ignoreDamageWhileColliderActive=gate;
            else outer.relay.ignoreDamageWhileColliderActive=gate;
            Physics.Walls.Add(outer);HashSet<object> affected=new HashSet<object>();Aquabro hero=new Aquabro();
            Aquabro.ApplyWaterImpactUnits(hero,charged,0,0,400,0,outer,affected,0);
            Check(unit.hits==0 && affected.Count==0,"Closed unit relay bypassed by water body pass");
            gate.enabled=false;
            Aquabro.ApplyWaterImpactUnits(hero,charged,0,0,400,0,outer,affected,0);
            Aquabro.ApplyWaterImpactUnits(hero,charged,0,0,400,0,outer,affected,0);
            Check(unit.hits==1 && unit.health==(charged ? 86 : 92),"Unit relay/body shared ledger missed or doubled");
            Check(unit.forceY==(charged ? 260 : 90),"Relayed unit lost original water launch");
        }
        foreach(bool charged in new bool[]{false,true})
        foreach(int direction in new int[]{-1,1})
        for(int family=0;family<3;family++)
        {
            Reset();UnlistedDamageReceiver unknown=family==2 ? new UnlistedDerivedReceiver() : new UnlistedDamageReceiver();
            Collider receiver=Part(unknown,0,0),a=Part(new object(),0,0),b=Part(new object(),0,0);
            if(family==0){a.gameObject=receiver.gameObject;b.gameObject=receiver.gameObject;}
            else
            {
                a.relay=new DamageRelay {damageGameObject=receiver.gameObject};
                b.relay=new DamageRelay {damageGameObject=family==2 ? a.gameObject : receiver.gameObject};
            }
            Physics.Walls.Add(b);Physics.Walls.Add(a);Physics.Walls.Add(receiver);
            Aquabro owner=new Aquabro();owner.X=-40*direction;
            PredabroSpear shot=new PredabroSpear {X=-16*direction,Y=0,xI=(charged ? 664 : 400)*direction,
                playerNum=0,damageInternal=charged ? 20 : 13};
            Aquabro.SweepImpact(shot,owner);
            Check(BossDamageTestStubs.Requests==1 && unknown.health==1000-(charged ? 20 : 13),
                "Generic spear direct/splash/ground double hit or missed receiver");
            AquabroWaterEffect.Last.Tick(.02f);
            Check(BossDamageTestStubs.Requests==1,"Generic receiver repeated during splash animation");

            Reset();unknown=new UnlistedDamageReceiver {closed=true};receiver=Part(unknown,0,0);Physics.Walls.Add(receiver);
            Aquabro.ApplyWaterImpactUnits(owner,charged,0,0,400*direction,20,receiver,new HashSet<object>(),0);
            Check(BossDamageTestStubs.Requests==1 && unknown.health==1000,"Generic water bypassed native phase gate");

            Reset();receiver=Part(new UnlistedDamageReceiver(),0,0);a=Part(new object(),0,0);b=Part(new object(),0,0);
            a.relay=new DamageRelay {damageGameObject=receiver.gameObject,ignoreDamageWhileOtherColliderActive=new Collider(0,1)};
            b.relay=new DamageRelay {damageGameObject=a.gameObject};Physics.Walls.Add(b);
            HashSet<object> affected=new HashSet<object>();
            Aquabro.ApplyWaterImpactUnits(owner,charged,0,0,400,20,b,affected,0);
            Check(BossDamageTestStubs.Requests==0 && affected.Count==0,"Nested relay water gate consumed hit");
            a.relay.ignoreDamageWhileOtherColliderActive.enabled=false;
            Aquabro.ApplyWaterImpactUnits(owner,charged,0,0,400,20,b,affected,0);
            Check(BossDamageTestStubs.Requests==1,"Opening nested water relay lost hit");

            Reset();a=Part(new object(),0,0);b=Part(new object(),0,0);
            a.relay=new DamageRelay {damageGameObject=b.gameObject};b.relay=new DamageRelay {damageGameObject=a.gameObject};
            Physics.Walls.Add(a);Physics.Walls.Add(b);
            shot=new PredabroSpear {X=-16*direction,Y=0,xI=(charged ? 664 : 400)*direction,playerNum=0};
            Aquabro.SweepImpact(shot,owner);
            Check(BossDamageTestStubs.Requests==0,"Cyclic water relay reached native damage");
        }
        Reset();
        Console.WriteLine("PASS generic collision water: unlisted/private inherited Damage; normal/charged swept direct; multi-collider/relay/chains; no ground/splash duplicates; native phase/nested gates; cycles");
    }
}

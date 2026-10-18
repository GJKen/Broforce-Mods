using System;
using System.Collections.Generic;
using AquabroMod;
using UnityEngine;

internal static class SkillDamageTargetTests
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static SpringDamageState spring;
    private static void Reset()
    {
        spring = new SpringDamageState();
        Map.Targets.Clear(); Map.Hits.Clear(); Map.TotalHits = MapController.ObjectHits = 0;
        Map.destroyableDoodads.Clear(); Map.DamageObserver = null;
        Physics.Contacts = new Collider[0]; Physics.SharkPropHits = new RaycastHit[0];
        Physics.Blocked = Physics.SharkTerrainEnabled = false;
        Physics.BlockedBelowY = float.NegativeInfinity;
    }
    private static int Frame(int kind) { return kind == 0 ? 4 : kind == 1 ? 12 : kind == 2 ? 22 : 10; }
    private static bool Contains(int kind, int frame, float x, float y)
    {
        if (kind == 0) return TidalWavePixelMask.Contains(frame, x, y, 0);
        if (kind == 1) return SharkPixelMask.Contains(frame, x, y, 0);
        if (kind == 2) return MaelstromPixelMask.Contains(frame, x, y, 0);
        return SpringPixelMask.Contains(frame, x, y, 0);
    }
    private static Vector2 Point(int kind, float facing, float scale)
    {
        for (int py = 10; py < 100; py++) for (int px = 0; px < 128; px++)
            if (Math.Abs(px - 64) > 16 && Contains(kind, Frame(kind), px + .5f, py + .5f))
                return new Vector2((px + .5f - 64) * facing * scale, (112 - py - .5f) * scale);
        throw new Exception("Missing skill contact sample " + kind);
    }
    private static bool Cast(Aquabro owner, int kind, float facing, float scale, HashSet<Unit> hits, bool finish)
    {
        int frame = Frame(kind);
        if (kind == 0) return owner.SkillWaveContact(0, 0, facing, scale, frame, hits, new HashSet<GameObject>());
        if (kind == 1)
        {
            owner.SkillSharkObjects(0, 0, facing, scale, frame, new HashSet<GameObject>());
            owner.SkillSharkBlast(0, 0, facing, scale, frame, hits);
        }
        if (kind == 2)
        {
            if (finish) owner.SkillMaelstromFinish(0, 0, facing, scale, new HashSet<Unit>(), hits);
            else owner.SkillMaelstromPull(0, 0, facing, scale, frame, .016f, true, new HashSet<Unit>());
        }
        if (kind == 3) owner.SkillSpringBlast(0, 0, facing, scale, frame, spring, 0f);
        return true;
    }
    private static Collider Part(Unit target, Vector2 point)
    {
        Collider part = new Collider(point.x, point.y, true);
        part.Relay = new DamageRelay(); part.Relay.unit = target;
        return part;
    }
    internal static void Run()
    {
        Aquabro owner = new Aquabro();
        for (int kind = 0; kind < 4; kind++)
        foreach (float facing in new float[] { -1, 1 })
        foreach (float scale in new float[] { .65f, 1f })
        {
            Vector2 point = Point(kind, facing, scale);
            Reset();
            Mook corpse = new Mook(); corpse.X = point.x; corpse.Y = point.y - 6;
            corpse.health = -1; corpse.playerNum = -1; Map.Targets.Add(corpse);
            HashSet<Unit> hits = new HashSet<Unit>();
            Cast(owner, kind, facing, scale, hits, false);
            Check(Map.TotalHits == 1 && Map.Hits.ContainsKey(corpse), "Corpse missed by skill " + kind);
            if (kind == 2)
            {
                Check(corpse.KnockCalls == 0, "Dead AI receives continuous suction");
                Cast(owner, kind, facing, scale, hits, true);
                Check(Map.TotalHits == 2 && Map.Hits[corpse] == 9, "Corpse finisher missing");
            }
            Cast(owner, kind, facing, scale, hits, kind == 2);
            Check(Map.TotalHits == (kind == 2 ? 2 : 1), "Corpse damage repeats");

            // Both a remote/unregistered boss and one overlapping the normal
            // body pass must be damaged once through its actual part.
            foreach (bool inMap in new bool[] { false, true })
            {
                Reset();
                Unit boss = new Unit(); boss.playerNum = -1;
                boss.X = inMap ? point.x : 1000; boss.Y = inMap ? point.y - 6 : 1000;
                if (inMap) Map.Targets.Add(boss);
                Collider a = Part(boss, point), b = Part(boss, point);
                Physics.Contacts = new Collider[] { a, b };
                hits = new HashSet<Unit>();
                Check(Cast(owner, kind, facing, scale, hits, false), "Boss part stopped wave");
                Check(Map.TotalHits == 1 && MapController.ObjectHits == 1 &&
                    Map.Hits[boss] == (kind == 0 ? 26 : kind == 3 ? 4 : kind == 1 ? 28 : 3),
                    "Boss part missed or body/parts double damage, skill " + kind);
                Cast(owner, kind, facing, scale, hits, kind == 2);
                Check(Map.TotalHits == (kind == 2 ? 2 : 1), "Boss hit ledger/finisher mismatch");
                Cast(owner, kind, facing, scale, hits, kind == 2);
                Check(Map.TotalHits == (kind == 2 ? 2 : 1), "Boss hit repeats");
            }

            // Closed or immune armour cannot consume an exposed part's hit,
            // and a body-coordinate fallback cannot bypass that armour.
            for (int filter = 0; filter < 7; filter++)
            {
                Reset(); Unit boss = new Unit(); boss.playerNum = -1;
                boss.X = point.x; boss.Y = point.y - 6; Map.Targets.Add(boss);
                Collider armour = Part(boss, point);
                if (filter == 0) armour.Relay.immunity = DamageType.Explosion;
                if (filter == 1) armour.Relay.ignoreDamageWhileColliderActive = new Collider(0, 0, true);
                if (filter == 2) armour.Relay.ignoreDamageWhileOtherColliderActive = new Collider(0, 0, true);
                if (filter == 3) boss.invulnerable = true;
                if (filter == 4) boss.playerNum = owner.playerNum;
                if (filter == 5) Physics.Blocked = true;
                if (filter == 6) owner.IsMine = false;
                Physics.Contacts = new Collider[] { armour }; hits = new HashSet<Unit>();
                Cast(owner, kind, facing, scale, hits, false);
                Check(Map.TotalHits == 0, "Boss armour/ally/immunity/wall/ownership bypass " + filter);
                if (filter < 3)
                {
                    Physics.Contacts = new Collider[] { armour, Part(boss, point) };
                    Cast(owner, kind, facing, scale, hits, false);
                    Check(Map.TotalHits == 1, "Armour consumed weak point hit");
                }
                owner.IsMine = true;
            }
        }
        Reset();
        CheckSpringPeriodic(owner);
        Console.WriteLine("PASS skill targets: four skills; corpses; remote/unregistered boss parts; body/part dedup; vortex finisher; armour/closed weak points; walls/allies/immunity/ownership; facing/scale");
    }

    private static void CheckSpringPeriodic(Aquabro owner)
    {
        foreach (int fps in new int[] { 30, 50, 60, 120 })
        foreach (bool bossPart in new bool[] { false, true })
        {
            Reset();
            Unit target = new Unit(); target.playerNum = -1; target.X = target.Y = 1000;
            if (bossPart)
            {
                Collider a = Part(target, new Vector2(0, 48));
                Bounds bounds = new Bounds(); bounds.min = new Vector3(-128, 1, 0);
                bounds.max = new Vector3(128, 112, 0); bounds.center = new Vector3(0, 56, 0);
                a.bounds = bounds;
                Collider b = Part(target, new Vector2(0, 48)); b.bounds = bounds;
                Physics.Contacts = new Collider[] { a, b };
            }
            else Map.Targets.Add(target);
            float clock = 0, previousHit = -1;
            int total = 0, count = 0;
            Map.DamageObserver = delegate(Unit victim, int damage)
            {
                Check(victim == target && damage == 4, "Spring pulse is not four damage");
                Check(previousHit < 0 || clock - previousHit >= .11998f, "Overlapping columns compressed pulse interval");
                Check(Map.LastUpForce == (count == 0 ? 650 : 0), "Spring repeats launch force");
                previousHit = clock; total += damage; count++;
            };
            SpringDamageState state = new SpringDamageState();
            List<AquabroSkillVisual> columns = new List<AquabroSkillVisual>();
            for (int column = 0; column < 7; column++)
                columns.Add(new AquabroSkillVisual(owner, 3, 0, 0, 1, 1.4f, column * .07f, 1, null, state));
            for (int tick = 1; tick <= fps * 2; tick++)
            {
                clock = tick / (float)fps;
                // Keep an ordinary enemy in visible water; use the real visual
                // clock and frame selection rather than calling the budget alone.
                int frame = Math.Min(17, (int)Math.Floor(clock / .05f));
                bool found = false;
                for (int py = 0; py < 112 && !found; py++) for (int px = 0; px < 128 && !found; px++)
                    if (SpringPixelMask.Contains(frame, px + .5f, py + .5f, 0))
                    { target.X = px + .5f - 64; target.Y = 112 - py - .5f - 6; found = true; }
                foreach (AquabroSkillVisual column in columns) column.Tick(1f / fps);
                int beforePause = count;
                foreach (AquabroSkillVisual column in columns) column.Tick(0);
                Check(count == beforePause, "Paused spring repeats damage");
            }
            Check(total == 28 && count == 7, "Spring total differs from 28 at " + fps + " FPS, boss=" + bossPart + ", total=" + total);
            foreach (AquabroSkillVisual column in columns) column.Dispose();

            // Re-entry retains the old budget; a new cast gets its own budget.
            Reset(); Vector2 point = Point(3, 1, 1);
            target.X = point.x; target.Y = point.y - 6; Map.Targets.Add(target);
            state = new SpringDamageState();
            owner.SkillSpringBlast(0, 0, 1, 1, 10, state, .1f);
            Physics.Blocked = true;
            owner.SkillSpringBlast(0, 0, 1, 1, 10, state, .3f);
            Check(Map.TotalHits == 1, "Spring damages after wall blocks contact");
            Physics.Blocked = false;
            owner.SkillSpringBlast(0, 0, 1, 1, 10, state, .4f);
            Check(Map.TotalHits == 2, "Re-entry did not resume spring damage");
            owner.SkillSpringBlast(0, 0, 1, 1, 18, state, .6f);
            Check(Map.TotalHits == 2, "Spring dissolve damages");
            owner.SkillSpringBlast(0, 0, 1, 1, 10, new SpringDamageState(), .4f);
            Check(Map.TotalHits == 3, "Fresh cast inherited previous budget");
        }
        Reset();
        Console.WriteLine("PASS spring periodic: 4 x 7 = 28 at 30/50/60/120 FPS; seven staggered overlapping columns; normal/boss parts; >=0.12s intervals; first launch only; pause/re-entry/walls/dissolve/new cast");
    }
}

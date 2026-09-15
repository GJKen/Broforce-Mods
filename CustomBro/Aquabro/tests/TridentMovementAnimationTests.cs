using System;
using System.Collections.Generic;
using Aquabro;

internal static class TridentMovementAnimationTests
{
    private static int checks;

    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }

    private static void Mapping()
    {
        int[] bodies = { 0, 6, 32, 33, 34, 35, 36, 37, 38, 39,
            40, 41, 42, 43, 44, 45, 46, 47,
            64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75,
            48, 49, 50, 104, 105, 106 };
        int[] weapons = { 9, 64, 10, 11, 12, 13, 14, 15, 25, 26,
            65, 66, 67, 68, 69, 70, 71, 72,
            27, 28, 29, 30, 31, 41, 42, 43, 44, 45, 46, 47,
            57, 58, 59, 60, 61, 62 };
        HashSet<int> used = new HashSet<int>();
        for (int i = 0; i < bodies.Length; i++)
        {
            int weapon = TridentMovementAnimation.WeaponCellForBody(bodies[i]);
            Check(weapon == weapons[i], "Body/weapon atlas mismatch: " + bodies[i]);
            Check(used.Add(weapon), "Two different movement poses share a weapon cell");
            Check(weapon >= 0 && weapon < 128 && (weapon >= 64 || weapon % 16 >= 9),
                "Movement overwrites an existing attack pose");
        }
        for (int i = 0; i < 8; i++)
            Check(TridentMovementAnimation.WeaponCellForBody(96 + i) ==
                TridentMovementAnimation.WeaponCellForBody(32 + i),
                "Dash must reuse the corresponding running weapon frame");
        foreach (int body in new int[] { -1, 1, 5, 7, 8, 31, 51, 63, 125, 159, 174, 191, 198, 511, 530 })
            Check(TridentMovementAnimation.WeaponCellForBody(body) == -1,
                "An unrelated body action selected a paired movement weapon: " + body);
        Check(TridentMovementAnimation.IsGroundBodyFrame(0), "Standing not recognized");
        Check(!TridentMovementAnimation.IsGroundBodyFrame(64), "Airborne frame treated as ground");
        Check(!TridentMovementAnimation.IsLandingBodyFrame(107), "Zipline treated as landing");
    }

    private static IEnumerable<int> TraversalBodies()
    {
        // 来自原版动画方法的实际输出区间，以及独立滑索扩展区间。
        int[] starts = { 76, 107, 160, 192, 512 };
        int[] ends = { 95, 124, 173, 197, 529 };
        for (int group = 0; group < starts.Length; group++)
            for (int body = starts[group]; body <= ends[group]; body++) yield return body;
    }

    private static void TraversalCoverageAndIsolation()
    {
        HashSet<int> weapons = new HashSet<int>();
        int bodies = 0;
        foreach (int body in TraversalBodies())
        {
            bodies++;
            Check(TridentMovementAnimation.IsTraversalBodyFrame(body), "Native traversal frame missing: " + body);
            Check(!TridentMovementAnimation.IsLandingBodyFrame(body), "Traversal confused with landing");
            bool unarmed = body < 512;
            Check(TridentMovementAnimation.IsUnarmedTraversalBodyFrame(body) == unarmed,
                "Zipline and empty-handed traversal were confused");
            for (int pose = 0; pose < 9; pose++)
            {
                int weapon = TridentMovementAnimation.WeaponCellForBody(body, pose);
                if (unarmed)
                    Check(weapon == -1, "An empty-handed action selected a held or attack weapon");
                else
                {
                    Check(weapon >= 595 && weapon <= 756, "Zipline escaped its allocated atlas region");
                    Check(weapons.Add(weapon), "Different zipline poses alias the same weapon cell");
                }
            }
            Check(TridentMovementAnimation.WeaponCellForBody(body) ==
                TridentMovementAnimation.WeaponCellForBody(body, 0), "Held pose selected an attack frame");
            Check(TridentMovementAnimation.WeaponCellForBody(body, -1) == -1, "Negative pose was accepted");
            Check(TridentMovementAnimation.WeaponCellForBody(body, 9) == -1, "Invalid pose was accepted");
        }
        Check(bodies == 76 && weapons.Count == 162, "Incomplete native traversal coverage");
        foreach (int body in new int[] { 75, 96, 106, 125, 159, 174, 191, 198, 511, 530 })
            Check(!TridentMovementAnimation.IsTraversalBodyFrame(body), "Traversal swallowed an adjacent action");
        foreach (int body in new int[] { 0, 6, 32, 40, 48, 64, 70, 96, 104 })
            Check(TridentMovementAnimation.WeaponCellForBody(body, 1) == -1,
                "Existing ground/air attacks must retain their original pose selection");
    }

    private static void TraversalTransitions()
    {
        for (int original = 107; original <= 124; original++)
        {
            int zipline = TridentMovementAnimation.ZiplineBodyFrame(original);
            Check(zipline == 512 + original - 107, "Zipline lost the native movement/settling phase");
            Check(TridentMovementAnimation.WeaponCellForBody(zipline) !=
                TridentMovementAnimation.WeaponCellForBody(original), "Zipline reused the hanging weapon pose");
            Check(TridentMovementAnimation.ZiplineBodyFrame(zipline) == zipline, "Zipline mapping applied twice");
        }
        foreach (int body in new int[] { -1, 0, 48, 76, 104, 105, 106, 125, 160, 192 })
            Check(TridentMovementAnimation.ZiplineBodyFrame(body) == body, "Zipline changed an unrelated native frame");
        for (int frame = -1; frame < 24; frame++)
            for (int original = 0; original < 4; original++)
                Check(TridentMovementAnimation.LadderRestBodyFrame(original, frame) ==
                    171 + ((frame % 3 + 3) % 3), "Ladder rest fell back to old running frames");
        foreach (int body in new int[] { -1, 6, 32, 160, 168, 171, 192, 193, 197, 512 })
            Check(TridentMovementAnimation.LadderRestBodyFrame(body, 4) == body,
                "Ladder rest interrupted an entry/exit or non-ladder action");
    }

    private static void TraversalAttackRecovery()
    {
        for (int body = 512; body <= 529; body++)
        {
            TridentAttackState state = new TridentAttackState();
            int held = TridentMovementAnimation.WeaponCellForBody(body);
            state.Step(0f, true, true);
            Check(state.Pose == TridentPose.Raise, "Traversal charge did not start with raise");
            Check(TridentMovementAnimation.WeaponCellForBody(body, (int)state.Pose) == held + 1,
                "Raise lost its current traversal body");
            state.Step(0.5f, true, true);
            Check(state.Pose == TridentPose.Charged, "Traversal charge did not complete");
            state.Step(0f, false, true);
            state.Step(0.05f, false, true);
            Check(state.ThrowNow && state.Pose == TridentPose.Throw, "Traversal release frame lost its throw event");
            Check(TridentMovementAnimation.WeaponCellForBody(body, (int)state.Pose) == held + 4,
                "Release failed to select the empty-weapon arm pose");
            state.Step(0.10f, false, true);
            Check(state.Pose == TridentPose.Recover, "Traversal did not show the returning hand");
            state.Step(0.071f, false, true);
            Check(state.Pose == TridentPose.Held &&
                TridentMovementAnimation.WeaponCellForBody(body, (int)state.Pose) == held,
                "Traversal attack recovery did not restore the matched grip");
            state.Step(0f, true, true);
            state.Cancel(true);
            Check(state.Pose == TridentPose.Held, "An interrupted traversal attack kept the windup pose");
        }
    }

    private static void LandingTimeline()
    {
        TridentMovementAnimation animation = new TridentMovementAnimation();
        Check(!animation.IsLanding && animation.LandingBodyFrame == -1, "Landing started on spawn");
        animation.StartLanding(false);
        Check(animation.LandingBodyFrame == 104, "Wrong stationary contact frame");
        animation.AdvanceLanding(0.059f, true, false);
        Check(animation.LandingBodyFrame == 104, "Contact ended before 60 ms");
        animation.AdvanceLanding(0.001f, true, false);
        Check(animation.LandingBodyFrame == 105, "Absorption did not start at 60 ms");
        animation.AdvanceLanding(0.08f, true, false);
        Check(animation.LandingBodyFrame == 106, "Recovery did not start at 140 ms");
        Check(animation.AdvanceLanding(0.10f, true, false), "Landing did not finish at 240 ms");
        Check(!animation.IsLanding && animation.LandingBodyFrame == -1, "Landing remained active");
        Check(!animation.AdvanceLanding(0.10f, true, false), "Completion emitted twice");
    }

    private static void CancellationAndLongFrames()
    {
        TridentMovementAnimation animation = new TridentMovementAnimation();
        animation.StartLanding(true);
        Check(animation.LandingBodyFrame == 48, "Wrong moving contact frame");
        animation.AdvanceLanding(0f, true, true);
        animation.AdvanceLanding(-1f, true, true);
        Check(animation.LandingBodyFrame == 48, "Paused/negative time changed the pose");
        animation.AdvanceLanding(0.1f, true, true);
        Check(animation.LandingBodyFrame == 49, "Wrong moving absorption frame");
        Check(!animation.AdvanceLanding(0.01f, false, true), "Interrupted landing reported completion");
        Check(!animation.IsLanding, "Another action could not interrupt landing");
        animation.StartLanding(false);
        Check(!animation.AdvanceLanding(0.01f, true, true), "Movement-mode change reported completion");
        Check(!animation.IsLanding, "Stationary landing survived a new movement input");
        animation.StartLanding(true);
        Check(animation.AdvanceLanding(0.5f, true, true), "Long frame left landing stuck");
        animation.StartLanding(false);
        Check(animation.LandingBodyFrame == 104, "A new landing reused the previous elapsed time");
        animation.CancelLanding();
        Check(!animation.IsLanding, "Explicit cancellation failed");
    }

    private static void FrameRates()
    {
        foreach (int fps in new int[] { 30, 60, 144 })
        {
            TridentMovementAnimation animation = new TridentMovementAnimation();
            animation.StartLanding(true);
            HashSet<int> phases = new HashSet<int>();
            int previous = 48;
            int ticks = 0;
            while (animation.IsLanding && ticks < fps)
            {
                int body = animation.LandingBodyFrame;
                Check(body >= previous && body <= 50, "Landing phases went backwards at " + fps + " FPS");
                phases.Add(body);
                previous = body;
                animation.AdvanceLanding(1f / fps, true, true);
                ticks++;
            }
            Check(!animation.IsLanding && phases.Count == 3, "Missing landing phase at " + fps + " FPS");
            Check(Math.Abs((double)ticks / fps - 0.24) <= 1.0 / fps + 0.00001,
                "Landing duration depends on frame rate");
        }
    }

    public static void Main()
    {
        Mapping();
        TraversalCoverageAndIsolation();
        TraversalTransitions();
        TraversalAttackRecovery();
        LandingTimeline();
        CancellationAndLongFrames();
        FrameRates();
        Console.WriteLine("Trident movement animation: 7 groups, " + checks + " checks passed.");
    }
}

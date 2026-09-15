using System;
using Aquabro;

internal static class TridentAttackStateTests
{
    private static int assertions;

    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition)
        {
            Console.Error.WriteLine("FAIL: " + message);
            Environment.Exit(1);
        }
    }

    private static void Hold(TridentAttackState state, int ticks, float delta)
    {
        for (int i = 0; i < ticks; i++)
        {
            state.Step(delta, true, true);
            Check(!state.ThrowNow, "Holding must never auto-fire.");
        }
    }

    private static void QuickThrowAndRecovery()
    {
        TridentAttackState state = new TridentAttackState();
        state.Step(0.016f, true, true);
        state.Step(0.016f, false, true);
        Check(!state.ThrowNow, "Release has an animation windup.");
        state.Step(0.049f, false, true);
        Check(!state.ThrowNow, "Cannot emit before the release frame.");
        state.Step(0.001f, false, true);
        Check(state.ThrowNow && !state.IsChargedThrow, "Tap produces one normal throw.");
        Check(state.Pose == TridentPose.Throw, "Projectile release matches the empty hand.");
        state.Step(0.1f, false, true);
        Check(!state.ThrowNow, "Throw event must not repeat.");
        state.Step(0.13f, false, true);
        Check(state.RestoredNow && state.Pose == TridentPose.Held, "Weapon returns after recovery.");
        state.Step(0.1f, false, true);
        Check(!state.RestoredNow, "Restore effect fires once.");
    }

    private static void ChargeThreshold()
    {
        TridentAttackState below = new TridentAttackState();
        below.Step(0.499f, true, true);
        below.Step(0f, false, true);
        below.Step(0.05f, false, true);
        Check(below.ThrowNow && !below.IsChargedThrow, "Below 0.5 seconds is a normal throw.");
        TridentAttackState exact = new TridentAttackState();
        exact.Step(0.5f, true, true);
        Check(exact.ChargedNow && exact.Pose == TridentPose.Charged, "Exactly 0.5 seconds charges.");
        exact.Step(0.1f, true, true);
        Check(!exact.ChargedNow, "Charge cue fires only once.");
        exact.Step(0f, false, true);
        exact.Step(0.05f, false, true);
        Check(exact.ThrowNow && exact.IsChargedThrow, "Charged status survives release windup.");
    }

    private static void CancelAndDisable()
    {
        TridentAttackState state = new TridentAttackState();
        Hold(state, 30, 1f / 60f);
        state.Cancel(true);
        Hold(state, 30, 1f / 60f);
        state.Step(0.016f, false, true);
        state.Step(1f, false, true);
        Check(!state.ThrowNow && state.Pose == TridentPose.Held, "Cancelled charge cannot fire on release.");
        state.Step(0.02f, true, true);
        Check(state.IsCharging, "A fresh press after cancellation works.");
        state.Step(0.02f, false, false);
        state.Step(1f, false, true);
        Check(!state.ThrowNow, "A release while stunned, frozen or dead cannot fire later.");
        state.Step(0.02f, true, true);
        state.Step(0.02f, false, true);
        state.Step(0.049f, false, true);
        state.Cancel(false);
        state.Step(0.1f, false, true);
        Check(!state.ThrowNow, "Interrupting the release windup cancels its projectile.");
    }

    private static void NoCooldownInputBuffer()
    {
        TridentAttackState state = new TridentAttackState();
        state.Step(0.02f, true, true);
        state.Step(0.02f, false, true);
        state.Step(0.05f, true, true);
        Check(state.ThrowNow, "Pressing during recovery does not suppress the pending throw.");
        Hold(state, 60, 1f / 60f);
        Check(!state.IsCharging, "Holding through recovery must not start a second charge.");
        state.Step(0.02f, false, true);
        state.Step(0.1f, false, true);
        Check(!state.ThrowNow, "Releasing a cooldown press must not fire.");
        state.Step(0.02f, true, true);
        Check(state.IsCharging, "Fresh press after recovery charges normally.");
    }

    private static void MeleeInterruptsCharge()
    {
        TridentAttackState state = new TridentAttackState();
        state.Step(0.5f, true, true);
        Check(state.StartMelee(true), "Melee can replace a charge.");
        Check(!state.StartMelee(true), "Repeated melee input cannot restart the attack.");
        state.Step(0.079f, false, true);
        Check(!state.MeleeNow && !state.ThrowNow, "Melee windup cannot hit or throw.");
        state.Step(0.001f, false, true);
        Check(state.MeleeNow && !state.ThrowNow, "Thrust hits once at 0.08 seconds.");
        state.Step(0.08f, false, true);
        Check(!state.MeleeNow, "Thrust cannot damage again during its visible active pose.");
        state.Step(0.09f, false, true);
        Check(!state.IsMelee && state.CanMelee, "Melee completes at 0.25 seconds.");
        Check(state.StartMelee(false), "Next melee can start after recovery.");
        state.Step(0.1f, false, false);
        state.Step(1f, false, true);
        Check(!state.MeleeNow && !state.IsMelee, "Stun also cancels an unlanded thrust.");
    }

    private static void FrameRateAndLongFrames()
    {
        foreach (int fps in new int[] { 30, 60, 144 })
        {
            TridentAttackState state = new TridentAttackState();
            Hold(state, fps, 1f / fps);
            state.Step(1f / fps, false, true);
            int shots = 0;
            for (int i = 0; i < fps; i++)
            {
                state.Step(1f / fps, false, true);
                if (state.ThrowNow) shots++;
            }
            Check(shots == 1 && state.IsChargedThrow, "One charged throw at each tested frame rate.");
        }
        TridentAttackState longFrame = new TridentAttackState();
        longFrame.Step(0.01f, true, true);
        longFrame.Step(0f, false, true);
        longFrame.Step(1f, false, true);
        Check(longFrame.ThrowNow && longFrame.RestoredNow, "A long frame cannot skip the attack event.");
        longFrame.Step(1f, false, true);
        Check(!longFrame.ThrowNow, "Long-frame event cannot repeat.");
        longFrame.Step(-1f, true, true);
        Check(!longFrame.ChargedNow, "Negative elapsed time does not charge.");
    }

    private static void StrikeGeometry()
    {
        Check(TridentStrikeGeometry.IsInEnvelope(0f, 1f, 24f, 5f),
            "An overlapping target is inside the strike envelope.");
        Check(TridentStrikeGeometry.IsInEnvelope(24f, 1f, 24f, 5f),
            "A target at the trident tip is inside the strike envelope.");
        Check(!TridentStrikeGeometry.IsInEnvelope(-7f, 1f, 24f, 5f),
            "A target fully behind the hero is outside the strike envelope.");
    }

    public static int Main()
    {
        QuickThrowAndRecovery();
        ChargeThreshold();
        CancelAndDisable();
        NoCooldownInputBuffer();
        MeleeInterruptsCharge();
        FrameRateAndLongFrames();
        StrikeGeometry();
        Console.WriteLine("PASS: 7 attack scenarios, " + assertions + " assertions.");
        return 0;
    }
}

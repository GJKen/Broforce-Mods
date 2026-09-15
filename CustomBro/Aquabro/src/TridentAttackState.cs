namespace Aquabro
{
    internal enum TridentPose
    {
        Held, Raise, Charge, Charged, Throw, Recover,
        ThrustWindup, Thrust, ThrustRecover
    }

    // 与 Unity 无关的输入时序，事件只在跨过有效帧时发出一次。
    internal sealed class TridentAttackState
    {
        internal const float ChargeDuration = 0.5f;
        internal const float ThrowReleaseTime = 0.05f;
        internal const float ThrowDuration = 0.22f;
        internal const float MeleeHitTime = 0.08f;
        internal const float MeleeDuration = 0.25f;

        private enum Phase { Idle, Charging, Throwing, Melee }
        private Phase phase;
        private bool previousFire;
        private bool suppressUntilRelease;
        private bool attackEmitted;
        private bool chargedThrow;
        private float elapsed;

        internal bool ThrowNow { get; private set; }
        internal bool MeleeNow { get; private set; }
        internal bool ChargedNow { get; private set; }
        internal bool RestoredNow { get; private set; }
        internal bool IsChargedThrow { get { return chargedThrow; } }
        internal bool IsMelee { get { return phase == Phase.Melee; } }
        internal bool IsCharging { get { return phase == Phase.Charging; } }
        internal bool CanMelee { get { return phase == Phase.Idle || phase == Phase.Charging; } }

        internal TridentPose Pose
        {
            get
            {
                if (phase == Phase.Charging)
                    return elapsed >= ChargeDuration ? TridentPose.Charged :
                        (elapsed < 0.10f ? TridentPose.Raise : TridentPose.Charge);
                if (phase == Phase.Throwing)
                    return elapsed + 0.00001f < ThrowReleaseTime ? TridentPose.Charge :
                        (elapsed < 0.15f ? TridentPose.Throw : TridentPose.Recover);
                if (phase == Phase.Melee)
                    return elapsed + 0.00001f < MeleeHitTime ? TridentPose.ThrustWindup :
                        (elapsed < 0.16f ? TridentPose.Thrust : TridentPose.ThrustRecover);
                return TridentPose.Held;
            }
        }

        internal void Step(float deltaTime, bool fireHeld, bool canAttack)
        {
            ClearEvents();
            if (!canAttack)
            {
                Cancel(fireHeld);
                return;
            }

            if (deltaTime < 0f) deltaTime = 0f;
            bool pressed = fireHeld && !previousFire;
            bool released = !fireHeld && previousFire;
            previousFire = fireHeld;
            if (suppressUntilRelease)
            {
                if (!fireHeld) suppressUntilRelease = false;
                pressed = false;
                released = false;
            }

            if (phase == Phase.Throwing || phase == Phase.Melee)
            {
                // 收招期间按下的攻击键不缓存，必须重新按下才能出下一招。
                if (fireHeld) suppressUntilRelease = true;
                elapsed += deltaTime;
                float hitTime = phase == Phase.Melee ? MeleeHitTime : ThrowReleaseTime;
                if (!attackEmitted && elapsed + 0.00001f >= hitTime)
                {
                    attackEmitted = true;
                    ThrowNow = phase == Phase.Throwing;
                    MeleeNow = phase == Phase.Melee;
                }
                float duration = phase == Phase.Melee ? MeleeDuration : ThrowDuration;
                if (elapsed + 0.00001f >= duration)
                {
                    RestoredNow = phase == Phase.Throwing;
                    phase = Phase.Idle;
                    elapsed = 0f;
                }
                return;
            }

            if (phase == Phase.Idle && pressed)
            {
                phase = Phase.Charging;
                elapsed = 0f;
            }
            if (phase != Phase.Charging) return;

            if (released)
            {
                chargedThrow = elapsed + 0.00001f >= ChargeDuration;
                phase = Phase.Throwing;
                elapsed = 0f;
                attackEmitted = false;
            }
            else if (fireHeld)
            {
                float before = elapsed;
                elapsed += deltaTime;
                if (elapsed + 0.00001f >= ChargeDuration)
                {
                    elapsed = ChargeDuration;
                    ChargedNow = before < ChargeDuration;
                }
            }
        }

        internal bool StartMelee(bool fireHeld)
        {
            if (!CanMelee) return false;
            Cancel(fireHeld);
            phase = Phase.Melee;
            return true;
        }

        internal void Cancel(bool fireHeld)
        {
            phase = Phase.Idle;
            elapsed = 0f;
            attackEmitted = false;
            chargedThrow = false;
            previousFire = fireHeld;
            suppressUntilRelease = fireHeld;
            ClearEvents();
        }

        private void ClearEvents()
        {
            ThrowNow = false;
            MeleeNow = false;
            ChargedNow = false;
            RestoredNow = false;
        }
    }
}

namespace Aquabro
{
    // 原有站跑跳蹲格号保留；地形动作仅滑索配九种武器姿态。
    internal sealed class TridentMovementAnimation
    {
        internal const int TraversalWeaponStart = 73;
        internal const int TraversalPoseCount = 9;
        internal const int TraversalBodyCount = 76;

        private bool landing;
        private bool movingLanding;
        private float landingElapsed;

        internal bool IsLanding { get { return landing; } }

        internal static bool IsGroundBodyFrame(int frame)
        {
            return frame == 0 || (frame >= 32 && frame <= 39) ||
                (frame >= 96 && frame <= 103);
        }

        internal static bool IsLandingBodyFrame(int frame)
        {
            return (frame >= 48 && frame <= 50) || (frame >= 104 && frame <= 106);
        }

        internal static int WeaponCellForBody(int bodyFrame)
        {
            return WeaponCellForBody(bodyFrame, 0);
        }

        internal static int TraversalIndexForBody(int bodyFrame)
        {
            if (bodyFrame >= 76 && bodyFrame <= 95) return bodyFrame - 76;
            if (bodyFrame >= 107 && bodyFrame <= 124) return 20 + bodyFrame - 107;
            if (bodyFrame >= 160 && bodyFrame <= 173) return 38 + bodyFrame - 160;
            if (bodyFrame >= 192 && bodyFrame <= 197) return 52 + bodyFrame - 192;
            if (bodyFrame >= 512 && bodyFrame <= 529) return 58 + bodyFrame - 512;
            return -1;
        }

        internal static bool IsTraversalBodyFrame(int bodyFrame)
        {
            return TraversalIndexForBody(bodyFrame) >= 0;
        }

        internal static bool IsUnarmedTraversalBodyFrame(int bodyFrame)
        {
            return IsTraversalBodyFrame(bodyFrame) && bodyFrame < 512;
        }

        // 悬挂和滑索原本共用 107–124；滑索重定向到扩展行，保留原版取帧与火花。
        internal static int ZiplineBodyFrame(int originalBodyFrame)
        {
            return originalBodyFrame >= 107 && originalBodyFrame <= 124
                ? 512 + originalBodyFrame - 107 : originalBodyFrame;
        }

        // 原版爬梯持火或侧移回退到旧跑步 0–3；仍在梯上时保持新版空手停驻。
        internal static int LadderRestBodyFrame(int originalBodyFrame, int animationFrame)
        {
            if (originalBodyFrame < 0 || originalBodyFrame > 3) return originalBodyFrame;
            return 171 + ((animationFrame % 3 + 3) % 3);
        }

        internal static int WeaponCellForBody(int bodyFrame, int pose)
        {
            if (pose < 0 || pose >= TraversalPoseCount) return -1;
            if (IsUnarmedTraversalBodyFrame(bodyFrame)) return -1;
            int traversal = TraversalIndexForBody(bodyFrame);
            if (traversal >= 0) return TraversalWeaponStart + traversal * TraversalPoseCount + pose;
            if (pose != 0) return -1;
            if (bodyFrame == 6) return 64;
            if (bodyFrame >= 40 && bodyFrame <= 47) return 65 + bodyFrame - 40;
            int index;
            if (bodyFrame == 0) index = 0;
            else if (bodyFrame >= 32 && bodyFrame <= 39) index = 1 + bodyFrame - 32;
            else if (bodyFrame >= 96 && bodyFrame <= 103) index = 1 + bodyFrame - 96;
            else if (bodyFrame >= 64 && bodyFrame <= 75) index = 9 + bodyFrame - 64;
            else if (bodyFrame >= 48 && bodyFrame <= 50) index = 21 + bodyFrame - 48;
            else if (bodyFrame >= 104 && bodyFrame <= 106) index = 24 + bodyFrame - 104;
            else return -1;
            return 9 + (index / 7) * 16 + index % 7;
        }

        internal int LandingBodyFrame
        {
            get
            {
                if (!landing) return -1;
                int phase = landingElapsed + 0.000001f < 0.06f ? 0 :
                    (landingElapsed + 0.000001f < 0.14f ? 1 : 2);
                return (movingLanding ? 48 : 104) + phase;
            }
        }

        internal void StartLanding(bool moving)
        {
            landing = true;
            movingLanding = moving;
            landingElapsed = 0f;
        }

        // 返回 true 仅表示正常播完；攻击、再次起跳等取消不会重置其他动作的帧号。
        internal bool AdvanceLanding(float deltaTime, bool canPlay, bool moving)
        {
            if (!landing) return false;
            if (!canPlay || moving != movingLanding)
            {
                CancelLanding();
                return false;
            }
            if (deltaTime > 0f) landingElapsed += deltaTime;
            if (landingElapsed + 0.000001f < 0.24f) return false;
            CancelLanding();
            return true;
        }

        internal void CancelLanding()
        {
            landing = false;
            landingElapsed = 0f;
        }
    }
}

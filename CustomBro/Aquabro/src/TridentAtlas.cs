// 由 tools/build_runtime_atlases.lua 生成，与贴图一起重建。
namespace AquabroMod
{
    internal static class TridentAtlas
    {
        internal static readonly short[] StandingLegs = DecodeShort("\u0264\u0264\u0264\u0264\u0264\u0264\u0264\u0264\u0264\u0264\u0264\u0264\u0264\u0264\u0264\u0264\u0264\u0264\u0265\u0266\u0264");
        internal static readonly short[] RunningLegs = DecodeShort("\u0267\u0268\u0269\u026a\u026b\u026c\u026d\u026e");
        internal static readonly short[] RunningBob = DecodeShort("\u0000\u0000\uffff\uffff\u0000\u0000\uffff\u0000");
        internal static readonly short[] JumpingLegs = DecodeShort("\u026f\u0270\u0271\u0272");
        internal static readonly short[] JumpingRecovery = DecodeShort("\u0060\u0063\u0069\u006c");
        internal static readonly short[] LadderLegs = DecodeShort("\u0273\u0274\u0275\u0276");
        internal static readonly short[] LadderBob = DecodeShort("\u0000\u0001\u0000\u0001");
        internal static readonly short[] ZiplineLegs = DecodeShort("\u0278\u0279\u027a\u027b\u027c\u0277");
        private static short[] DecodeShort(string data)
        {
            short[] values = new short[data.Length];
            for (int i = 0; i < values.Length; i++) values[i] = (short)data[i];
            return values;
        }
        // 普通待机、跑步、跳跃及落地：身体格、手臂/武器格、裁剪X、裁剪Y。
        internal static readonly byte[,] MovementFrames = {
            { 0, 36, 9, 2 },
            { 32, 37, 10, 5 },
            { 33, 38, 10, 5 },
            { 34, 39, 9, 5 },
            { 35, 40, 9, 5 },
            { 36, 41, 9, 6 },
            { 37, 42, 9, 5 },
            { 38, 43, 10, 3 },
            { 39, 44, 10, 4 },
            { 96, 44, 10, 4 },
            { 97, 45, 10, 4 },
            { 98, 46, 10, 4 },
            { 99, 47, 10, 3 },
            { 100, 48, 10, 3 },
            { 101, 47, 10, 3 },
            { 102, 45, 10, 5 },
            { 103, 49, 10, 5 },
            { 104, 37, 10, 5 },
            { 105, 50, 9, 2 },
            { 106, 51, 9, 2 },
            { 107, 52, 9, 2 },
            { 108, 53, 9, 1 },
            { 109, 54, 9, 1 },
            { 110, 53, 9, 1 },
            { 111, 50, 9, 3 },
            { 112, 55, 9, 3 },
            { 113, 36, 9, 2 },
            { 114, 56, 10, 2 },
            { 115, 57, 10, 2 },
            { 116, 56, 10, 2 },
            { 117, 58, 9, 0 },
            { 118, 59, 9, 0 },
            { 119, 58, 9, 0 },
        };
        // 每组21个攻击阶段；低8位保存武器格/2+1，0表示-1，高8位各4位保存X、Y。
        internal static readonly short[,] UpperFrames = DecodeUpper("\u7c51\u2c52\u1a53\u1a54\u1a55\u1b56\u1b57\u1b58\u1a59\u1b5a\u1a5b\u1a5c\u1a5d\u1a5e\u1a5f\u1a60\u1a61\u1a62\uee63\uec64\ua265\u7c51\u2c52\u1b66\u1b67\u1b68\u1c69\u1b6a\u1b6b\u1a6c\u1b6d\u1b6e\u1b6f\u1b70\u1b71\u1a72\u1a73\u1a74\u1a75\ucf63\ucd64\ua365\u7b51\u2b52\u1966\u1967\u1968\u1a69\u1a6a\u1a6b\u196c\u1a6d\u196e\u196f\u1970\u1971\u1972\u1973\u1974\u1975\ucd63\ucb64\u0000\u6b51\u1b52\u0953\u0954\u0955\u0a56\u0a57\u0a58\u0959\u0a5a\u095b\u095c\u095d\u095e\u095f\u0960\u0961\u0962\udd63\udb64\u9165\u8876\u1877\u0878\u0879\u087a\u087b\u087c\u087d\u087e\u087f\u0880\u0881\u0882\u0883\u0884\u0885\u0886\u0887\ub888\ub889\u8876");

        private static short[,] DecodeUpper(string data)
        {
            short[,] frames = new short[data.Length, 3];
            for (int i = 0; i < data.Length; i++)
            {
                int value = data[i], cell = value & 255;
                frames[i, 0] = (short)(cell == 0 ? -1 : (cell - 1) * 2);
                frames[i, 1] = (short)(value >> 12);
                frames[i, 2] = (short)((value >> 8) & 15);
            }
            return frames;
        }
    }
}

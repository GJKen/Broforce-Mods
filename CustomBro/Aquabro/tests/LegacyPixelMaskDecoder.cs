// Historical codec regressions only; excluded from Aquabro.dll.
namespace AquabroMod
{
    internal sealed partial class PixelMask
    {
        // Generated raw DEFLATE data is expanded once by the mask's static initializer.
        // Keep the same per-frame arrays; no decoding is performed by either query.
        internal static byte[][] DecodeFrames(int count, int bytesPerFrame, string data)
        {
            return DecodeFrames(count, bytesPerFrame, System.Convert.FromBase64String(data));
        }

        internal static byte[][] DecodeFrames(int count, int bytesPerFrame, int compressedLength, string data)
        {
            return DecodeFrames(count, bytesPerFrame, DecodePacked(data, compressedLength));
        }

        // Each frame starts with a zero-bit run, then alternates zero/one runs.
        // Run lengths are unsigned 7-bit integers; expand only during initialization.
        internal static byte[][] DecodeRunFrames(int count, int bytesPerFrame, int compressedLength, int runLength, string data)
        {
            if (count < 0 || bytesPerFrame <= 0 || bytesPerFrame > int.MaxValue / 8)
                throw new System.IO.InvalidDataException("Invalid pixel mask dimensions");
            byte[] runs = DecodeFrames(1, runLength, compressedLength, data)[0];
            byte[][] result = new byte[count][];
            int offset = 0;
            for (int frame = 0; frame < count; frame++)
            {
                byte[] bytes = result[frame] = new byte[bytesPerFrame];
                int bit = 0;
                bool filled = false;
                while (bit < bytesPerFrame * 8)
                {
                    int run = 0, shift = 0, value;
                    do
                    {
                        if (offset >= runs.Length) throw new System.IO.InvalidDataException("Truncated pixel mask run");
                        value = runs[offset++];
                        if (shift > 28 || (shift == 28 && (value & 248) != 0))
                            throw new System.IO.InvalidDataException("Invalid pixel mask run length");
                        run |= (value & 127) << shift;
                        shift += 7;
                    } while ((value & 128) != 0);
                    if (run > bytesPerFrame * 8 - bit || (run == 0 && (bit != 0 || filled)))
                        throw new System.IO.InvalidDataException("Invalid pixel mask run");
                    if (filled)
                    {
                        while (run > 0)
                        {
                            int length = System.Math.Min(run, 8 - bit % 8);
                            bytes[bit / 8] |= (byte)(((1 << length) - 1) << (bit % 8));
                            bit += length;
                            run -= length;
                        }
                    }
                    else bit += run;
                    filled = !filled;
                }
            }
            if (offset != runs.Length) throw new System.IO.InvalidDataException("Oversized pixel mask runs");
            return result;
        }

        private static byte[][] DecodeFrames(int count, int bytesPerFrame, byte[] compressed)
        {
            byte[][] result = new byte[count][];
            using (System.IO.MemoryStream input = new System.IO.MemoryStream(compressed, false))
            using (System.IO.Compression.DeflateStream stream = new System.IO.Compression.DeflateStream(input, System.IO.Compression.CompressionMode.Decompress))
            {
                for (int frame = 0; frame < count; frame++)
                {
                    byte[] bytes = result[frame] = new byte[bytesPerFrame];
                    int offset = 0;
                    while (offset < bytes.Length)
                    {
                        int read = stream.Read(bytes, offset, bytes.Length - offset);
                        if (read == 0) throw new System.IO.InvalidDataException("Truncated pixel mask");
                        offset += read;
                    }
                }
                if (stream.ReadByte() != -1) throw new System.IO.InvalidDataException("Oversized pixel mask");
            }
            return result;
        }

    }
}

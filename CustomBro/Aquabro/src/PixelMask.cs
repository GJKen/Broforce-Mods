namespace AquabroMod
{
    // Shared query implementation; generated files contain only their frame data.
    internal sealed class PixelMask
    {
        private readonly int size;
        private readonly byte[][] frames;

        internal PixelMask(int size, byte[][] frames)
        {
            this.size = size;
            this.frames = frames;
        }

        // Binary arithmetic coding with 32 spatial contexts: two left pixels
        // and three pixels above. Restore the original arrays only once.
        internal static byte[][] DecodeContextFrames(int count, int size, int compressedLength, uint checksum, string data)
        {
            if (count < 0 || size < 8 || size > 46340 || (size & 7) != 0)
                throw new System.IO.InvalidDataException("Invalid pixel mask dimensions");
            byte[] encoded = DecodePacked(data, compressedLength);
            int offset = 0, low = 0, high = 65535, code = 0;
            for (int i = 0; i < 16; i++) code = (code << 1) | ReadContextBit(encoded, ref offset);
            int[] zeros = new int[32], ones = new int[32];
            for (int i = 0; i < 32; i++) zeros[i] = ones[i] = 1;
            byte[][] result = new byte[count][];
            uint hash = 2166136261u;
            for (int frame = 0; frame < count; frame++)
            {
                byte[] bytes = result[frame] = new byte[size * size / 8];
                int bit = 0;
                for (int y = 0; y < size; y++)
                {
                    int left = 0;
                    int top = y == 0 ? 0 : bytes[(bit - size) >> 3];
                    int above = ((top & 1) << 1) | ((top >> 1) & 1);
                    for (int x = 0; x < size; x++, bit++)
                    {
                        int context = ((left & 3) << 3) | above;
                        int a = zeros[context], b = ones[context];
                        int split = low + (high - low + 1) * a / (a + b) - 1;
                        int value = code > split ? 1 : 0;
                        if (value == 0) { high = split; a++; }
                        else { low = split + 1; b++; bytes[bit >> 3] |= (byte)(1 << (bit & 7)); }
                        if (a + b >= 1024) { a = (a + 1) / 2; b = (b + 1) / 2; }
                        zeros[context] = a; ones[context] = b;
                        while (true)
                        {
                            if (high < 32768) { }
                            else if (low >= 32768) { low -= 32768; high -= 32768; code -= 32768; }
                            else if (low >= 16384 && high < 49152) { low -= 16384; high -= 16384; code -= 16384; }
                            else break;
                            low <<= 1; high = (high << 1) | 1;
                            code = (code << 1) | ReadContextBit(encoded, ref offset);
                        }
                        left = (left << 1) | value;
                        int next = bit - size + 2;
                        above = ((above << 1) & 7) | (y > 0 && x + 2 < size ? (bytes[next >> 3] >> (next & 7)) & 1 : 0);
                    }
                }
                foreach (byte value in bytes) unchecked { hash = (hash ^ value) * 16777619u; }
            }
            int remaining = encoded.Length * 8 - offset;
            if (remaining < 2 || remaining > 9 || hash != checksum)
                throw new System.IO.InvalidDataException("Invalid context pixel mask");
            while (offset < encoded.Length * 8)
                if (ReadContextBit(encoded, ref offset) != 0) throw new System.IO.InvalidDataException("Invalid pixel mask padding");
            return result;
        }

        private static int ReadContextBit(byte[] encoded, ref int offset)
        {
            if (offset >= encoded.Length * 8) throw new System.IO.InvalidDataException("Truncated context pixel mask");
            int value = (encoded[offset >> 3] >> (7 - (offset & 7))) & 1;
            offset++;
            return value;
        }

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

        private static byte[] DecodePacked(string data, int compressedLength)
        {
            if (compressedLength < 0) throw new System.IO.InvalidDataException("Invalid compressed pixel mask length");
            int encodedLength = compressedLength / 2 + compressedLength % 2;
            if (data == null || data.Length != encodedLength) throw new System.IO.InvalidDataException("Invalid packed pixel mask data length");

            byte[] result = new byte[compressedLength];
            for (int index = 0; index < compressedLength; index++)
            {
                int value = data[index / 2];
                result[index] = (byte)(index % 2 == 0 ? value >> 8 : value);
            }
            return result;
        }

        internal bool Contains(int frame, float x, float y, float margin)
        {
            return IntersectsRect(frame, x, y, x, y, margin);
        }

        // Exact pixel-square to rectangle distance, including collider edges.
        internal bool IntersectsRect(int frame, float left, float top, float right, float bottom, float margin)
        {
            if (frame < 0 || frame >= frames.Length) return false;
            if (left > right) { float swap = left; left = right; right = swap; }
            if (top > bottom) { float swap = top; top = bottom; bottom = swap; }
            int minX = System.Math.Max(0, (int)System.Math.Floor(left - margin) - 1);
            int maxX = System.Math.Min(size - 1, (int)System.Math.Floor(right + margin));
            int minY = System.Math.Max(0, (int)System.Math.Floor(top - margin) - 1);
            int maxY = System.Math.Min(size - 1, (int)System.Math.Floor(bottom + margin));
            byte[] mask = frames[frame];
            for (int py = minY; py <= maxY; py++)
            {
                float dy = System.Math.Max(0f, System.Math.Max(py - bottom, top - (py + 1)));
                for (int px = minX; px <= maxX; px++)
                {
                    int bit = py * size + px;
                    if ((mask[bit / 8] & (1 << (bit % 8))) == 0) continue;
                    float dx = System.Math.Max(0f, System.Math.Max(px - right, left - (px + 1)));
                    if (dx * dx + dy * dy <= margin * margin) return true;
                }
            }
            return false;
        }
    }
}

namespace StrikeZone.Core
{
    /// <summary>
    /// Deterministic PCG32 random generator. The same seed gives the same sequence on
    /// client and server, which is what makes bullet spread reproducible: the server
    /// recomputes spread from (player, tick, shot index) instead of trusting the client.
    /// Not for anything involving money or loot — those use a CSPRNG on the backend.
    /// </summary>
    public struct SzRandom
    {
        private const ulong Multiplier = 6364136223846793005UL;
        private const ulong Increment = 1442695040888963407UL;

        private ulong _state;

        public SzRandom(ulong seed)
        {
            _state = 0UL;
            NextUInt();
            _state += seed;
            NextUInt();
        }

        public uint NextUInt()
        {
            ulong old = _state;
            _state = unchecked(old * Multiplier + Increment);
            uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
            int rot = (int)(old >> 59);
            return (xorShifted >> rot) | (xorShifted << ((-rot) & 31));
        }

        /// <summary>Uniform float in [0, 1).</summary>
        public float NextFloat()
        {
            return (NextUInt() >> 8) * (1f / 16777216f);
        }

        /// <summary>Uniform float in [-1, 1).</summary>
        public float NextSigned()
        {
            return NextFloat() * 2f - 1f;
        }

        public float Range(float min, float max)
        {
            return min + (max - min) * NextFloat();
        }

        /// <summary>Stable 64-bit hash of three integers (FNV-1a + SplitMix64 finaliser).</summary>
        public static ulong Hash(int a, int b, int c)
        {
            unchecked
            {
                ulong h = 1469598103934665603UL;
                h = (h ^ (uint)a) * 1099511628211UL;
                h = (h ^ (uint)b) * 1099511628211UL;
                h = (h ^ (uint)c) * 1099511628211UL;
                h ^= h >> 30;
                h *= 0xBF58476D1CE4E5B9UL;
                h ^= h >> 27;
                h *= 0x94D049BB133111EBUL;
                h ^= h >> 31;
                return h;
            }
        }
    }
}

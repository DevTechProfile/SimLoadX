using System;

namespace SimLoadX
{
    public class ThreadSafeRandom
    {
        private static readonly Random _global = new Random();
        [ThreadStatic]
        private static Random _local;

        public ThreadSafeRandom()
        {
            if (_local == null)
            {
                int seed;
                lock (_global)
                {
                    seed = _global.Next();
                }
                _local = new Random(seed);
            }
        }

        public int Next()
        {
            return _local.Next();
        }

        public int Next(int range)
        {
            return _local.Next(range);
        }

        public double NextDouble()
        {
            return _local.NextDouble();
        }
    }
}

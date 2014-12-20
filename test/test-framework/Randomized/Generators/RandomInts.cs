﻿using System;
using System.Diagnostics;

namespace Lucene.Net.Randomized.Generators
{
    public static class RandomInts
    {
        public static int RandomIntBetween(Random random, int start, int end)
        {
            return random.NextIntBetween(start, end);
        }

        public static int NextIntBetween(this Random random, int min, int max)
        {
            Debug.Assert(min <= max, String.Format("Min must be less than or equal max int. min: {0}, max: {1}", min, max));
            var range = max - min;
            if (range < Int32.MaxValue)
                return min + random.Next(1 + range);
           
            return min + (int)Math.Round(random.NextDouble() * range);
        }

        public static Boolean NextBoolean(this Random random)
        {
            return random.NextDouble() > 0.5;
        }

        /* .NET has random.Next(max) which negates the need for randomInt(Random random, int max) as  */
    }
}

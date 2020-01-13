
using System;

namespace JNetworking
{
    public class PredictableRandom
    {
        private Random rand;

        public PredictableRandom(int seed)
        {
            rand = new Random(seed);
        }

        public float GetValue()
        {
            const int MAX = int.MaxValue / 2;
            int val = rand.Next(MAX);
            float value = (float)val / MAX;

            return value;
        }

        public float GetRange(float min, float max)
        {
            return UnityEngine.Mathf.Lerp(min, max, GetValue());
        }
    }
}

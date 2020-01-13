
using System.Collections.Generic;

namespace JNetworking.Utils
{
    public class DualMap<K, K2>
    {
        public int Count { get { return forwards.Count; } }

        public K2 this[K k]
        {
            get
            {
                return Get(k);
            }
        }
        public K this[K2 k2]
        {
            get
            {
                return Get(k2);
            }
        }

        private readonly Dictionary<K, K2> forwards;
        private readonly Dictionary<K2, K> backwards;

        public DualMap()
        {
            forwards = new Dictionary<K, K2>();
            backwards = new Dictionary<K2, K>();
        }  
        
        public bool Contains(K value)
        {
            return forwards.ContainsKey(value);
        }

        public bool Contains(K2 value)
        {
            return backwards.ContainsKey(value);
        }

        public DualMap<K, K2> Add(K k, K2 k2)
        {
            if (Contains(k))
            {
                throw new System.Exception($"Duplicate K value {k}");
            }
            if (Contains(k2))
            {
                throw new System.Exception($"Duplicate K2 value {k2}");
            }

            forwards.Add(k, k2);
            backwards.Add(k2, k);

            return this;
        }

        public K2 Get(K k)
        {
            if (Contains(k))
                return forwards[k];
            else
                throw new System.Exception($"Mapping for {k} not found!");
        }

        public K Get(K2 k2)
        {
            if (Contains(k2))
                return backwards[k2];
            else
                throw new System.Exception($"Mapping for {k2} not found!");
        }
    }
}
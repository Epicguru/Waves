using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Mirror
{
    /// <summary>
    /// A component to synchronize Mecanim animation states for networked objects.
    /// Custom implementation by James B.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Network/NetworkAnimator")]
    [RequireComponent(typeof(NetworkIdentity))]
    public class NetworkAnimator : NetworkBehaviour
    {
        public Animator Animator;

        private Dictionary<int, AnimatorControllerParameterType> hashToType;
        private Dictionary<string, int> nameToHash;
        private Dictionary<int, ulong> hashToMask;
        private Dictionary<ulong, int> maskToHash;

        private Dictionary<int, bool> bools;
        private Dictionary<int, float> floats;
        private Dictionary<int, int> ints;

        private bool CanWrite { get { return isServer; } }

        private void Awake()
        {
            if (Animator == null)
                Animator = GetComponentInChildren<Animator>();

            if (Animator == null)
                return;

            hashToType = new Dictionary<int, AnimatorControllerParameterType>();
            nameToHash = new Dictionary<string, int>();
            hashToMask = new Dictionary<int, ulong>();
            maskToHash = new Dictionary<ulong, int>();
            bools = new Dictionary<int, bool>();
            floats = new Dictionary<int, float>();
            ints = new Dictionary<int, int>();

            int count = 0;
            foreach(var p in Animator.parameters)
            {
                // Triggers are handled differently.
                if (p.type == AnimatorControllerParameterType.Trigger)
                    continue;

                if(count == 64)
                {
                    Debug.LogError($"There are more than 64 parameters (not including triggers) on this animator. Only 64 are supported by the networking system. Some parameters will not be sychronized. Consider using less parameters.");
                    break;
                }

                int hash = p.nameHash;
                ulong mask = 1UL << count;

                hashToType.Add(hash, p.type);
                hashToMask.Add(hash, mask);
                maskToHash.Add(mask, hash);
                nameToHash.Add(p.name, hash);

                switch (p.type)
                {
                    case AnimatorControllerParameterType.Float:
                        floats.Add(hash, p.defaultFloat);
                        break;
                    case AnimatorControllerParameterType.Int:
                        ints.Add(hash, p.defaultInt);
                        break;
                    case AnimatorControllerParameterType.Bool:
                        bools.Add(hash, p.defaultBool);
                        break;
                }

                Debug.Log($"Registered parameter {p.name} of type {p.type}");

                count++;
            }

            Debug.Log($"Initialized net animator with {hashToType.Count} parameters, not including triggers.");
        }

        public bool HasParameter(int hash, AnimatorControllerParameterType type)
        {
            return hashToType.ContainsKey(hash) && hashToType[hash] == type;
        }

        public int GetHash(string name)
        {
            return Animator.StringToHash(name);
        }

        private bool GetHash(string name, out int hash)
        {
            if (!nameToHash.ContainsKey(name))
            {
                hash = 0;
                return false;
            }

            hash = nameToHash[name];
            return true;
        }

        public void SetBool(string name, bool value)
        {
            if (!GetHash(name, out int hash))
            {
                Debug.LogWarning($"Failed to find parameter for name {name}.");
                return;
            }

            SetBool(hash, value);
        }

        public void SetBool(int hash, bool value)
        {
            if (!CanWrite)
            {
                Debug.LogWarning("Cannot set animator parameters when not on server!");
                return;
            }            
            if (!HasParameter(hash, AnimatorControllerParameterType.Bool))
            {
                Debug.LogWarning($"Parameter {name} is not a bool!");
                return;
            }

            bool hasChanged = bools[hash] != value;
            if (!hasChanged)
                return;

            ulong mask = hashToMask[hash];

            bools[hash] = value;

            SetDirtyBit(mask);

            Animator.SetBool(hash, value);
        }

        public void SetTrigger(string name)
        {
            SetTrigger(Animator.StringToHash(name));
        }

        public void SetTrigger(int hash)
        {
            if (!CanWrite)
            {
                Debug.LogWarning("Cannot set animator parameters when not on server!");
                return;
            }

            // Note: since triggers aren't tracked, this assumes that the hash is valid. If it isn't valid, Unity will log a warning
            // that no parameter was found for that hash.

            // Play locally on server.
            Animator.SetTrigger(hash);

            // Let all clients know to play it too.
            RpcSetTrigger(hash);
        }

        [ClientRpc]
        private void RpcSetTrigger(int hash)
        {
            if (isServer) // Don't play on server!
                return;

            // Play on local animator!
            Animator.SetTrigger(hash);
        }

        public override bool OnSerialize(NetworkWriter writer, bool initialState)
        {
            if (initialState)
            {
                // Write number of dirty variables, in this case all of them are considered dirty.
                writer.WriteByte((byte)hashToType.Count);

                // Write all.
                foreach (var pair in hashToType)
                {
                    int hash = pair.Key;
                    var type = pair.Value;

                    writer.WriteInt32(hash);

                    switch (type)
                    {
                        case AnimatorControllerParameterType.Float:
                            writer.WriteSingle(floats[hash]);
                            break;
                        case AnimatorControllerParameterType.Int:
                            writer.WriteInt32(ints[hash]);
                            break;
                        case AnimatorControllerParameterType.Bool:
                            writer.WriteBoolean(bools[hash]);
                            break;
                    }
                }
            }
            else
            {
                ulong dirtyMask = this.syncVarDirtyBits;

                // Count the number of dirty variables and write that number to stream.
                byte dirtyCount = 0;
                for (int i = 0; i < 64; i++)
                {
                    ulong mask = 1UL << i;
                    bool isOne = (dirtyMask & mask) == mask;

                    if (isOne)
                        dirtyCount++;
                }
                writer.WriteByte(dirtyCount);

                // Write only changed variables.
                for (int i = 0; i < hashToType.Count; i++)
                {
                    ulong mask = 1UL << i;

                    bool changed = (dirtyMask & mask) == mask;

                    if (changed)
                    {
                        int hash = maskToHash[mask];
                        var type = hashToType[hash];

                        writer.WriteInt32(hash);
                        switch (type)
                        {
                            case AnimatorControllerParameterType.Float:
                                writer.WriteSingle(floats[hash]);
                                break;
                            case AnimatorControllerParameterType.Int:
                                writer.WriteInt32(ints[hash]);
                                break;
                            case AnimatorControllerParameterType.Bool:
                                writer.WriteBoolean(bools[hash]);
                                break;
                        }
                    }
                }

            }

            return true;
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            // Does not matter if it is initial state or not, reading is the same.
            byte count = reader.ReadByte();

            for (int i = 0; i < count; i++)
            {
                int hash = reader.ReadInt32();
                var type = hashToType[hash];

                switch (type)
                {
                    case AnimatorControllerParameterType.Float:
                        float valFloat = reader.ReadSingle();
                        // Write this to the client.
                        Animator.SetFloat(hash, valFloat);
                        break;
                    case AnimatorControllerParameterType.Int:
                        int valInt = reader.ReadInt32();
                        Animator.SetInteger(hash, valInt);
                        break;
                    case AnimatorControllerParameterType.Bool:
                        bool valBool = reader.ReadBoolean();
                        Animator.SetBool(hash, valBool);
                        break;
                }
            }
        }
    }
}

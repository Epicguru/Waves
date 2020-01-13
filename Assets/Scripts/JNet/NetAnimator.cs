
using System.Collections.Generic;
using Lidgren.Network;
using UnityEngine;

namespace JNetworking
{
    public class NetAnimator : NetBehaviour
    {
        public Animator Animator
        {
            get
            {
                if (_anim == null)
                    _anim = GetComponentInChildren<Animator>();
                return _anim;
            }
        }
        private Animator _anim;

        private AnimatorControllerParameter[] parameters;
        private Dictionary<string, byte> paramDict;

        private bool doneInit;

        private void Awake()
        {
            Init();
        }

        private void Init()
        {
            if (doneInit)
                return;

            parameters = Animator.parameters;
            int count = parameters.Length;
            if (parameters.Length > 256)
            {
                count = 256;
                JNet.Error($"There are {parameters.Length} animator parameters on object {gameObject.name}, but only 256 can be networked. Some will not be syncronized. Why do you have that many anyway...");
            }

            paramDict = new Dictionary<string, byte>();
            for (int i = 0; i < count; i++)
            {
                var param = parameters[i];
                paramDict.Add(param.name.Trim(), (byte)i);
            }

            doneInit = true;
        }

        public bool HasParameter(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
             
            return paramDict != null && paramDict.ContainsKey(name);
        }

        public void SetBool(string name, bool value)
        {
            Init();
            if (paramDict.ContainsKey(name))
            {
                byte index = paramDict[name];
                var param = parameters[index];
                int id = param.nameHash;

                bool current = Animator.GetBool(id);
                if(current != value)
                {
                    Animator.SetBool(id, value);

                    // Send to all clients.
                    if (IsServer)
                        InvokeRPC("RpcBool", index, value);
                }
            }
        }

        [Rpc]
        private void RpcBool(byte index, bool value)
        {
            if (IsServer)
                return;

            Init();
            var p = GetParam(index);
            if (p != null)
            {
                Animator.SetBool(p.nameHash, value);
            }
        }

        public void SetFloat(string name, float value)
        {
            Init();
            if (paramDict.ContainsKey(name))
            {
                byte index = paramDict[name];
                var param = parameters[index];
                int id = param.nameHash;

                float current = Animator.GetFloat(id);
                if (current != value)
                {
                    Animator.SetFloat(id, value);

                    // Send to all clients.
                    if (IsServer)
                        InvokeRPC("RpcFloat", index, value);
                }
            }
        }

        [Rpc]
        private void RpcFloat(byte index, float value)
        {
            if (IsServer)
                return;

            Init();
            var p = GetParam(index);
            if (p != null)
            {
                Animator.SetFloat(p.nameHash, value);
            }
        }

        public void SetInteger(string name, int value)
        {
            Init();
            if (paramDict.ContainsKey(name))
            {
                byte index = paramDict[name];
                var param = parameters[index];
                int id = param.nameHash;

                int current = Animator.GetInteger(id);
                if (current != value)
                {
                    Animator.SetInteger(id, value);

                    // Send to all clients.
                    if (IsServer)
                        InvokeRPC("RpcInt", index, value);
                }
            }
        }

        [Rpc]
        private void RpcInt(byte index, int value)
        {
            if (IsServer)
                return;

            Init();
            var p = GetParam(index);
            if (p != null)
            {
                Animator.SetInteger(p.nameHash, value);
            }
        }

        public void SetTrigger(string name)
        {
            Init();
            if (paramDict.ContainsKey(name))
            {
                byte index = paramDict[name];
                var param = parameters[index];
                int id = param.nameHash;

                Animator.SetTrigger(id);

                // Send to all clients.
                if(IsServer)
                    InvokeRPC("RpcTrigger", index);
            }
        }

        [Rpc]
        private void RpcTrigger(byte index)
        {
            if (IsServer)
                return;

            Init();
            var p = GetParam(index);
            if(p != null)
            {
                Animator.SetTrigger(p.nameHash);
            }
        }

        private AnimatorControllerParameter GetParam(byte index)
        {
            if (index < parameters.Length)
            {
                return parameters[index];
            }
            return null;
        }

        public override void Serialize(NetOutgoingMessage msg, bool isForFirst)
        {
            if (!isForFirst)
                return;

            Init();

            foreach (var param in parameters)
            {
                switch (param.type)
                {
                    case AnimatorControllerParameterType.Float:
                        msg.Write(Animator.GetFloat(param.nameHash));
                        break;

                    case AnimatorControllerParameterType.Int:
                        msg.Write(Animator.GetInteger(param.nameHash));
                        break;

                    case AnimatorControllerParameterType.Bool:
                        msg.Write(Animator.GetBool(param.nameHash));
                        break;
                    
                    case AnimatorControllerParameterType.Trigger:
                        break;
                }
            }
        }

        public override void Deserialize(NetIncomingMessage msg, bool first)
        {
            if (!first)
                return;

            Init();

            foreach (var param in parameters)
            {
                switch (param.type)
                {
                    case AnimatorControllerParameterType.Float:
                        Animator.SetFloat(param.nameHash, msg.ReadFloat());
                        break;

                    case AnimatorControllerParameterType.Int:
                        Animator.SetFloat(param.nameHash, msg.ReadInt32());
                        break;

                    case AnimatorControllerParameterType.Bool:
                        Animator.SetBool(param.nameHash, msg.ReadBoolean());
                        break;

                    case AnimatorControllerParameterType.Trigger:
                        break;
                }
            }
        }
    }
}

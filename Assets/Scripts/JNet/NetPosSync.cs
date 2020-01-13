
using JNetworking;
using Lidgren.Network;
using UnityEngine;

namespace JNetworking
{
    public partial class NetPosSync : NetBehaviour
    {
        [Header("Sync Options")]
        [Range(0f, 60f)]
        public float SendRate = 10f;
        public bool SyncRotation = false;

        [Header("Interpolation")]
        public AnimationCurve Curve = CreateDefaultCurve();
        private static AnimationCurve CreateDefaultCurve()
        {
            var curve = new AnimationCurve();
            Keyframe a = new Keyframe(0f, 0f, 1f, 1f);
            Keyframe b = new Keyframe(1f, 1f, 1f, 1f);
            Keyframe c = new Keyframe(1.5f, 1.5f, 0f, 0f);
            Keyframe d = new Keyframe(2f, 1f, -1f, 0f);

            curve.AddKey(a);
            curve.AddKey(b);
            curve.AddKey(c);
            curve.AddKey(d);

            curve.postWrapMode = WrapMode.ClampForever;
            curve.preWrapMode = WrapMode.ClampForever;

            return curve;
        }

        private Vector2 lastSentPos;
        private float lastSentAngle;
        private float CurrentAngle
        {
            get
            {
                return transform.localEulerAngles.z;
            }
            set
            {
                var current = transform.localEulerAngles;
                current.z = value;
                transform.localEulerAngles = current;
            }
        }
        private Vector2 CurrentPos
        {
            get
            {
                return transform.localPosition;
            }
            set
            {
                transform.localPosition = value;
            }
        }
        private float sendTimer;

        // Client only stuff.
        private Vector2 oldPos, newPos;
        private float oldRot, newRot;
        private float sinceRecievedTimer;

        private void Update()
        {
            if (IsServer)
            {
                ServerUpdate();
            }
            if (IsClient)
            {
                ClientUpdate();
            }
        }

        private void ServerUpdate()
        {
            float interval = SendRate == 0f ? 0f : 1f / SendRate;
            sendTimer += Time.unscaledDeltaTime;
            if(sendTimer >= interval)
            {
                sendTimer = 0f;

                if (CurrentPos != lastSentPos)
                    NetDirty = true;
                if (SyncRotation && CurrentAngle != lastSentAngle)
                    NetDirty = true;
            }
        }

        private void ClientUpdate()
        {
            // Don't do anything if already on the server.
            if (IsServer)
                return;

            // Interpolate to the position that the server sends...
            float interval = SendRate == 0f ? 0f : 1f / SendRate;
            bool noLerp = interval == 0f;
            sinceRecievedTimer += Time.unscaledDeltaTime;
            float elapsed = sinceRecievedTimer;

            if (!noLerp)
            {
                // Lerp!
                float p = elapsed / interval;
                float x = Curve.Evaluate(p);

                Vector2 lerpedPos = Vector2.LerpUnclamped(oldPos, newPos, x);
                CurrentPos = lerpedPos;

                if (SyncRotation)
                {
                    float lerpedAngle = Mathf.LerpAngle(oldRot, newRot, x);
                    CurrentAngle = lerpedAngle;
                }
            }
            else
            {
                CurrentPos = newPos;
                if (SyncRotation)
                    CurrentAngle = newRot;
            }            
        }

        public override void Serialize(NetOutgoingMessage msg, bool isForFirst)
        {
            msg.Write(CurrentPos);
            lastSentPos = CurrentPos;

            if (SyncRotation)
            {
                msg.Write(CurrentAngle);
                lastSentAngle = CurrentAngle;
            }
        }

        public override void Deserialize(NetIncomingMessage msg, bool first)
        {
            oldPos = CurrentPos;
            newPos = msg.ReadVector2();

            if (SyncRotation)
            {
                oldRot = CurrentAngle;
                newRot = msg.ReadFloat();
            }

            if (first)
            {
                oldPos = newPos;
                CurrentPos = newPos;
                if (SyncRotation)
                {
                    oldRot = newRot;
                    CurrentAngle = newRot;
                }
            }

            sinceRecievedTimer = 0f;
        }
    }
}


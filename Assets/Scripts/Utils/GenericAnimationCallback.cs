using UnityEngine;

namespace ProjectB
{
    [RequireComponent(typeof(Animator))]
    public class GenericAnimationCallback : MonoBehaviour
    {
        public void AnimEvent(AnimationEvent e)
        {
            SendMessageUpwards("UponAnimationEvent", e, SendMessageOptions.DontRequireReceiver);
        }
    }
}

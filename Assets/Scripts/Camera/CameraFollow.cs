using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Rigidbody2D Target;
    public float Z = -10f;

    private void Update()
    {
        if(Target != null)
            transform.position = (Vector3)Target.transform.position + Vector3.forward * Z;
    }
}

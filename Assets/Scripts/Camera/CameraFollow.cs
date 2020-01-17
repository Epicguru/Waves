using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public static CameraFollow Instance
    {
        get
        {
            if (_instance == null)
                _instance = Camera.main.GetComponent<CameraFollow>();
            return _instance;
        }
    }
    private static CameraFollow _instance;

    public Transform Target;
    public float Z = -10f;

    private void Update()
    {
        if(Target != null)
            transform.position = (Vector3)Target.transform.position + Vector3.forward * Z;
    }
}

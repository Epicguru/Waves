using UnityEngine;

[RequireComponent(typeof(Camera))]
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

    public Camera Camera
    {
        get
        {
            if (_cam == null)
                _cam = GetComponent<Camera>();
            return _cam;
        }
    }
    private Camera _cam;

    public Transform Target;
    public float Z = -10f;
    public float TargetSize = 10f;

    private void Update()
    {
        if(Target != null)
            transform.position = (Vector3)Target.transform.position + Vector3.forward * Z;

        Camera.orthographicSize = Mathf.Lerp(Camera.orthographicSize, TargetSize, Time.deltaTime * 10f);
    }
}

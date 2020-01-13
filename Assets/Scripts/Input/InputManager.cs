
using UnityEngine;

public class InputManager : MonoBehaviour
{
    public static Vector2 WorldMousePosition { get; private set; }

    public Camera MainCamera;

    private void Update()
    {
        Debug.Assert(MainCamera != null, "Main camera is not assigned in input manager!");

        WorldMousePosition = MainCamera.ScreenToWorldPoint(Input.mousePosition);
    }
}
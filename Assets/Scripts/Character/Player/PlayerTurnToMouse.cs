using UnityEngine;

/// <summary>
/// Turns the character towards the mouse pointer.
/// </summary>
public class PlayerTurnToMouse : MonoBehaviour
{
    private void Update()
    {
        Vector2 currentPos = transform.position;
        Vector2 targetPos = InputManager.WorldMousePosition;
        Vector2 diff = (targetPos - currentPos);
        float angle = diff.ToAngle() * Mathf.Rad2Deg;

        this.transform.localEulerAngles = new Vector3(0f, 0f, angle);
    }
}

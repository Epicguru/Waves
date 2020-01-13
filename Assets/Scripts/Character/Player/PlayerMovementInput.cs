
using UnityEngine;

[RequireComponent(typeof(Character))]
public class PlayerMovementInput : MonoBehaviour
{
    public Character Character
    {
        get
        {
            if (_char == null)
                _char = GetComponent<Character>();
            return _char;
        }
    }
    private Character _char;

    private void Update()
    {
        // URGTODO replace with new input system.
        var move = Character.Movement;

        Vector2 dir = Vector2.zero;
        if (Input.GetKey(KeyCode.A))
            dir.x -= 1f;
        if (Input.GetKey(KeyCode.D))
            dir.x += 1f;
        if (Input.GetKey(KeyCode.W))
            dir.y += 1f;
        if (Input.GetKey(KeyCode.S))
            dir.y -= 1f;

        move.InputDirection = dir;
    }
}


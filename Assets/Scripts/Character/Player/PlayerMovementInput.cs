
using Mirror;
using UnityEngine;

[RequireComponent(typeof(Character))]
public class PlayerMovementInput : NetworkBehaviour
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
        if (!isLocalPlayer)
            return;

        var move = Character.Movement;
        move.InputDirection = CollectInput();
    }

    private Vector2 CollectInput()
    {
        Vector2 dir = Vector2.zero;

        // URGTODO replace with new input system.
        if (Input.GetKey(KeyCode.A))
            dir.x -= 1f;
        if (Input.GetKey(KeyCode.D))
            dir.x += 1f;
        if (Input.GetKey(KeyCode.W))
            dir.y += 1f;
        if (Input.GetKey(KeyCode.S))
            dir.y -= 1f;

        return dir;
    }
}


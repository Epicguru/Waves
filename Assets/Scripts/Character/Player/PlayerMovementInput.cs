
using JNetworking;
using UnityEngine;

[RequireComponent(typeof(Character))]
public class PlayerMovementInput : NetBehaviour
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

    public Vector2 LatestInput;
    private Vector2 lastSent;

    private void Update()
    {
        var move = Character.Movement;

        if (HasLocalOwnership)
        {
            LatestInput = CollectInput();
            if (!IsServer)
            {
                if(LatestInput != lastSent)
                {
                    InvokeCMD("CmdSendInput", LatestInput);
                    lastSent = LatestInput;
                }
            }
        }

        move.InputDirection = LatestInput;
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

    [Cmd]
    private void CmdSendInput(Vector2 dir)
    {
        this.LatestInput = dir;
    }
}



using Mirror;
using UnityEngine;

[RequireComponent(typeof(Character))]
public class PlayerVehicleInput : NetworkBehaviour
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

    [Range(0.1f, 1f)]
    public float MaxForceScale = 1f;

    public float Turn;
    public float Forwards;

    private float lastSentTurn;
    private float lastSentForwards;

    private void Update()
    {
        if (!isLocalPlayer) // Should not actually be necessary since this script is enabled and disabled when needed by each client.
            return;

        Turn = 0f;
        if (Input.GetKey(KeyCode.A))
            Turn += 1f;
        if (Input.GetKey(KeyCode.D))
            Turn -= 1f;

        Forwards = 0f;
        if (Input.GetKey(KeyCode.S))
            Forwards -= 1f * MaxForceScale;
        if (Input.GetKey(KeyCode.W))
            Forwards += 1f * MaxForceScale;

        if (!isServer && RequiresSend()) // If not on server, send input to server.
        {
            CmdSendInput(Turn, Forwards);
            lastSentForwards = Forwards;
            lastSentTurn = Turn;
        }

        if (isServer)
        {
            // Directly apply inputs on server.
            HandleInputs(Turn, Forwards);
        }
    }

    private bool RequiresSend()
    {
        return lastSentTurn != Turn || Forwards != lastSentForwards;
    }

    [Command]
    private void CmdSendInput(float turn, float forwards)
    {
        // Hand these inputs off to the car physics.
        HandleInputs(turn, forwards);
    }

    [Server]
    private void HandleInputs(float turn, float forwards)
    {
        var veh = Character.CurrentVehicle;
        if (veh == null)
        {
            Debug.LogWarning("Character is sending input for a vehicle, but they are not in a vehicle!");
            return;
        }

        veh.HandleInput(turn, forwards);
    }
}
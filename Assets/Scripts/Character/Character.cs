
using JNetworking;
using UnityEngine;

[RequireComponent(typeof(CharacterMovement))]
public class Character : NetBehaviour
{
    public CharacterMovement Movement
    {
        get
        {
            if (_movement == null)
                _movement = GetComponent<CharacterMovement>();
            return _movement;
        }
    }
    private CharacterMovement _movement;

    [SyncVar]
    public string Name = "Bob";

    public bool IsPlayer { get { return ControllingPlayer != null; } }

    public Player ControllingPlayer
    {
        get
        {
            if (PlayerNetRef == null)
                return null;

            return PlayerNetRef.GetComponent<Player>();
        }
        set
        {
            if (PlayerNetRef == null)
                PlayerNetRef = new NetRef();

            PlayerNetRef.Set(value?.NetObject);
        }
    }

    [SyncVar]
    public NetRef PlayerNetRef;

    private void Start()
    {
        if (IsPlayer && HasLocalOwnership)
        {
            Camera.main.GetComponent<CameraFollow>().Target = this.GetComponent<Rigidbody2D>();
        }
    }
}

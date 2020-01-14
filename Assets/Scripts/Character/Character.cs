
using Mirror;
using UnityEngine;

[RequireComponent(typeof(CharacterMovement))]
public class Character : NetworkBehaviour
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

    public bool IsPlayer { get { return false; } }

    private void Start()
    {
        
    }
}

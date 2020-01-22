
using Mirror;
using UnityEngine;

[DisallowMultipleComponent]
public class Item : NetworkBehaviour
{
    public Character Character
    {
        get
        {
            return _character == null ? null : _character.GetComponent<Character>();
        }
        internal set
        {
            if (!isServer)
                return;

            _character = value == null ? null : value.gameObject;
        }
    }
    [SyncVar]
    private GameObject _character;

    public bool IsHeld { get { return _character != null; } }

    [SyncVar]
    public string Name = "My Item Name";

    private void Awake()
    {
        syncInterval = 0f;
    }

    private void Update()
    {
        if (IsHeld)
        {
            if (transform.parent != Character.ItemManager.Holder)
            {
                transform.SetParent(Character.ItemManager.Holder, true);
                transform.localPosition = Vector3.zero;
                transform.localEulerAngles = Vector3.zero;
            }            
        }
        else
        {
            if (transform.parent != null)
            {
                transform.SetParent(null, true);
            }
        }
    }
}

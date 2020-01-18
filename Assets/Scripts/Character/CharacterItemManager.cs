
using Mirror;
using UnityEngine;

public class CharacterItemManager : NetworkBehaviour
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

    public Transform Holder;

    public Item CurrentItem
    {
        get
        {
            if (_itemGO == null)
                return null;

            return _itemGO.GetComponent<Item>();
        }
        set
        {
            if (!isServer)
            {
                Debug.LogError("Cannot set item when not on server.");
                return;
            }

            if (value == null && _itemGO == null)
                return;

            if (value != null && _itemGO == value.gameObject)
                return;

            if (value == null)
            {
                if(_itemGO != null)
                {
                    // Remove client authority from the item we are about to drop.
                    var current = CurrentItem;

                    current.Character = null;
                    current.netIdentity.RemoveClientAuthority();
                }

                _itemGO = null;
            }
            else
            {
                // Check if we are already holding an item.
                if (_itemGO != null)
                {
                    // Remove client authority from the item we are about to drop.
                    var current = CurrentItem;

                    current.Character = null;
                    current.netIdentity.RemoveClientAuthority();
                }

                _itemGO = value.gameObject;

                // We need to assign client authority.
                value.netIdentity.AssignClientAuthority(Character.connectionToClient);

                // Also tell the item that we are the character holding it.
                value.Character = this.Character;
            }

            // The item should now automatically parent and all that good stuff.
        }
    }

    [SyncVar]
    private GameObject _itemGO;
}

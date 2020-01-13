
using JNetworking;
using System.Collections.Generic;

/// <summary>
/// Player class. When a player joins the game, a player object is created, and so is their character.
/// When the player leaves the game, the player object is destroyed.
/// This way the character and the player are separated, but the player still controls their character.
/// </summary>
public class Player : NetBehaviour
{
    public static List<Player> AllPlayers = new List<Player>();

    public string Name;

    public Character Character
    {
        get
        {
            if (CharacterNetRef == null)
                return null;

            return CharacterNetRef.GetComponent<Character>();
        }
        set
        {
            if (CharacterNetRef == null)
                CharacterNetRef = new NetRef();

            CharacterNetRef.Set(value?.NetObject);
        }
    }

    [SyncVar]
    public NetRef CharacterNetRef;
}
    

using UnityEngine;

[DisallowMultipleComponent]
public class Item : MonoBehaviour
{
    public Character Character
    {
        get
        {
            if (transform.parent == null)
                _char = null;

            if (_char == null)
                _char = GetComponentInParent<Character>();
            return _char;
        }
    }
    private Character _char;

    public string Name;
}

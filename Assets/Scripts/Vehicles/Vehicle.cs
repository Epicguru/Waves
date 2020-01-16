
using Mirror;
using UnityEngine;

public class Vehicle : NetworkBehaviour
{
    public string Name = "Car";

    [Header("Riders")]
    [Range(0, 32)]
    public int MaxPassengers = 1;

    [SyncVar]
    private GameObject _driver;
    private readonly SyncListGameObject _passengers = new SyncListGameObject();

    public bool HasDriver { get { return _driver != null; } }
    public int PassengerCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < _passengers.Count; i++)
            {
                var obj = _passengers[i];
                if (obj != null)
                    count++;
            }

            return count;
        }
    }
    public int FreeSpaceCount { get { return (MaxPassengers - PassengerCount) + (HasDriver ? 0 : 1); } }

    public override void OnStartServer()
    {
        // Populate list of passengers with null passengers.
        _passengers.Clear();
        for (int i = 0; i < MaxPassengers; i++)
        {
            _passengers.Add(null);
        }
    }

    [Server]
    public void SetDriver(Character c)
    {
        if(c != null)
        {
            if(c.CurrentVehicle != null)
            {
                Debug.LogError($"Cannot set vehicle driver to {c} because that character is already driving or a passenger in a vehicle ({c.CurrentVehicle}).");
                return;
            }
        }

        if (c != null)
            _driver = c.gameObject;
        else
            _driver = null;
    }

    public Character GetDriver()
    {
        return _driver == null ? null : _driver.GetComponent<Character>();
    }

    [Server]
    public void SetPassenger(int index, Character c)
    {
        if(index < 0 || index >= _passengers.Count)
        {
            Debug.LogError($"Cannot set passenger for index {index}, index must be between 0 and {_passengers.Count - 1} inclusive.");
            return;
        }
        if (c != null)
        {
            if (c.CurrentVehicle != null)
            {
                Debug.LogError($"Cannot set vehicle driver to {c} because that character is already driving or a passenger in a vehicle ({c.CurrentVehicle}).");
                return;
            }
        }

        var current = _passengers[index];
        if (current == c?.gameObject)
            return;

        _passengers[index] = c?.gameObject;
    }

    /// <summary>
    /// Returns the index of the first available passenger seat. Returns -1 if no seats are available (if the vehicle is full).
    /// See <see cref="PassengerCount"/> and <see cref="FreeSpaceCount"/>.
    /// </summary>
    /// <returns>The first available index or -1 if none is available.</returns>
    [Server]
    public int GetAvailablePassengerIndex()
    {
        for (int i = 0; i < _passengers.Count; i++)
        {
            var obj = _passengers[i];
            if (obj == null)
                return i;
        }

        return -1;
    }

    public Character GetPassenger(int index)
    {
        if (_passengers == null)
            return null;

        if (index < 0 || index >= _passengers.Count)
        {
            Debug.LogError($"Cannot get passenger for index {index}, index must be between 0 and {_passengers.Count - 1} [{MaxPassengers - 1}] inclusive.");
            return null;
        }

        return _passengers[index]?.GetComponent<Character>();
    }

    /// <summary>
    /// Gets the passenger index of the character. If the character is not currently a passenger, -1 is returned.
    /// This serves as a way to check if a character is currently riding or driving this vehicle, although <see cref="Character.CurrentVehicle"/> would be an easier way to check.
    /// </summary>
    /// <param name="c">The character to look for.</param>
    /// <returns>The passenger index, or -1 if that character was not found in this vehicle.</returns>
    public int GetPassengerIndex(Character c)
    {
        if (_passengers == null)
            return -1;

        if (c == null)
            return -1;

        var go = c.gameObject;

        for (int i = 0; i < _passengers.Count; i++)
        {
            if (_passengers[i] == go)
                return i;
        }

        return -1;
    }
}

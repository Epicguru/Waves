
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

    /// <summary>
    /// Gets or sets the current vehicle. This should be used to move the character in and out of vehicles, instead of directly accessing the Vehicle class methods.
    /// When setting to null, the character leaves their vehicle.
    /// When assigning a vehicle, the character will be put in the driving slot if available, otherwise in the first available passenger slot.
    /// </summary>
    public Vehicle CurrentVehicle
    {
        get
        {
            return _vehicle == null ? null : _vehicle.GetComponent<Vehicle>();
        }
        set
        {
            if (!isServer)
            {
                Debug.LogWarning($"Can't set current vehicle when not on server.");
                return;
            }

            if (value == null)
            {
                if (_vehicle == null)
                    return; // Nothing to do.

                // Tell our current vehicle that we are leaving. Bye!
                var current = CurrentVehicle;
                if (current.GetDriver() == this)
                {
                    // Remove from driver position.
                    current.SetDriver(null);
                }
                else
                {
                    int index = current.GetPassengerIndex(this);
                    if (index != -1)
                    {
                        // Remove from passenger position.
                        current.SetPassenger(index, null);
                    }
                    else
                    {
                        // Uh oh! We are not the driver, and are not a passenger. This should never happen!
                        Debug.LogError("Logic error: character is registered as being in a vehicle, but is not driver nor passenger.");
                    }
                }
            }
            else
            {
                var current = CurrentVehicle;
                if (value == current)
                    return; // Nothing to do.

                // Check if the vehicle can actually fit this character.
                int available = value.FreeSpaceCount;
                if (available == 0)
                {
                    Debug.LogWarning($"Cannot put character {this} into vehicle {value} because that vehicle is full.");
                    return;
                }

                if (current != null)
                {
                    // Leave current vehicle.
                    if (current.GetDriver() == this)
                    {
                        // Remove from driver position.
                        current.SetDriver(null);
                    }
                    else
                    {
                        int index = current.GetPassengerIndex(this);
                        if (index != -1)
                        {
                            // Remove from passenger position.
                            current.SetPassenger(index, null);
                        }
                        else
                        {
                            // Uh oh! We are not the driver, and are not a passenger. This should never happen!
                            Debug.LogError("Logic error: character is registered as being in a vehicle, but is not driver nor passenger.");
                            return;
                        }
                    }
                }

                bool hasDriver = value.HasDriver;
                if (!hasDriver)
                {
                    // I am the driver now Mr Philips.
                    value.SetDriver(this);

                    // Mark as current vehicle.
                    _vehicle = value.gameObject;
                }
                else
                {
                    // Join as a passenger. We know that there is at least one seat available from the check from before.
                    int seatIndex = value.GetAvailablePassengerIndex();
                    value.SetPassenger(seatIndex, this);

                    // Mark as current vehicle.
                    _vehicle = value.gameObject;
                }
            }
        }
    }

    /// <summary>
    /// Is the character currently driving or riding avehicle? See <see cref="CurrentVehicle"/> for info on the vehicle.
    /// </summary>
    public bool IsInVehicle
    {
        get
        {
            return _vehicle != null;
        }
    }

    [SyncVar]
    private GameObject _vehicle;

    public Vehicle ToMoveInto;

    public override void OnStartLocalPlayer()
    {
        //Camera.main.GetComponent<CameraFollow>().Target = Movement.Body;
    }

    private void Update()
    {
        Movement.enabled = !IsInVehicle;

        if (ToMoveInto != null)
        {
            CurrentVehicle = ToMoveInto;
            ToMoveInto = null;
        }
    }
}

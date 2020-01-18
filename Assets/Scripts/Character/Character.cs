
using Mirror;
using UnityEngine;

[RequireComponent(typeof(CharacterMovement))]
[RequireComponent(typeof(CharacterItemManager))]
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
    public CharacterItemManager ItemManager
    {
        get
        {
            if (_itemManager == null)
                _itemManager = GetComponent<CharacterItemManager>();
            return _itemManager;
        }
    }
    private CharacterItemManager _itemManager;
    public PlayerTurnToMouse TurnToMouse
    {
        get
        {
            if (_turnToMouse == null)
                _turnToMouse = GetComponent<PlayerTurnToMouse>();
            return _turnToMouse;
        }
    }
    private PlayerTurnToMouse _turnToMouse;
    public PlayerVehicleInput VehicleInput
    {
        get
        {
            if (_vehPlayerInput == null)
                _vehPlayerInput = GetComponent<PlayerVehicleInput>();
            return _vehPlayerInput;
        }
    }
    private PlayerVehicleInput _vehPlayerInput;

    [SyncVar]
    public string Name = "Bob";

    public Item StartingItem;

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

                    Debug.Log($"{this} is no longer the driver of {current}.");
                }
                else
                {
                    int index = current.GetPassengerIndex(this);
                    if (index != -1)
                    {
                        // Remove from passenger position.
                        current.SetPassenger(index, null);

                        Debug.Log($"{this} is no longer a passenger of {current}.");
                    }
                    else
                    {
                        // Uh oh! We are not the driver, and are not a passenger. This should never happen!
                        Debug.LogError("Logic error: character is registered as being in a vehicle, but is not driver nor passenger.");
                    }
                }

                // Set vehicle to null.
                _vehicle = null;
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

                        Debug.Log($"{this} is no longer the driver of {current} (moving to new vehicle {value}).");
                    }
                    else
                    {
                        int index = current.GetPassengerIndex(this);
                        if (index != -1)
                        {
                            // Remove from passenger position.
                            current.SetPassenger(index, null);

                            Debug.Log($"{this} is no longer a passenger of {current} (moving to new vehicle {value}).");
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

                    Debug.Log($"{this} is now driving {value}.");
                }
                else
                {
                    // Join as a passenger. We know that there is at least one seat available from the check from before.
                    int seatIndex = value.GetAvailablePassengerIndex();
                    value.SetPassenger(seatIndex, this);

                    // Mark as current vehicle.
                    _vehicle = value.gameObject;

                    Debug.Log($"{this} is now a passenger of {value}.");
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

    /// <summary>
    /// Returns true if this character is currently driving a vehicle. Note that even if this character is not driving (returns false), they could still be 
    /// a passenger.
    /// </summary>
    public bool IsVehicleDriver
    {
        get
        {
            return IsInVehicle && CurrentVehicle.GetDriver() == this;
        }
    }

    [SyncVar]
    private GameObject _vehicle;

    public Vehicle ToMoveInto;

    public override void OnStartLocalPlayer()
    {
        CameraFollow.Instance.Target = this.transform;
    }

    public override void OnStartServer()
    {
        // Spawn in an item and equip it.
        if(StartingItem != null)
        {
            var spawned = Instantiate(StartingItem);
            NetworkServer.Spawn(spawned.gameObject);

            Debug.Log(this.netIdentity.connectionToClient);

            // Now equip it.
            ItemManager.CurrentItem = spawned;
        }
    }

    private void Update()
    {
        //Movement.enabled = isLocalPlayer && !IsInVehicle;
        if (TurnToMouse != null)
            TurnToMouse.enabled = isLocalPlayer && !IsInVehicle;
        VehicleInput.enabled = isLocalPlayer && IsVehicleDriver;

        if (IsInVehicle)
        {
            if (transform.parent == null)
            {
                var veh = CurrentVehicle;
                transform.parent = IsVehicleDriver ? veh.GetDriverSeat() : veh.GetPassengerSeat(veh.GetPassengerIndex(this));
                // Disable physics and rigidbody.
                Movement.Body.simulated = false;
            }

            // Move to local (0, 0).
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

        }
        else
        {
            if (transform.parent != null)
            {
                transform.SetParent(null, true);
                Movement.Body.simulated = true;
            }
        }

        if (ToMoveInto != null)
        {
            CurrentVehicle = ToMoveInto;
            ToMoveInto = null;
        }

        if (isLocalPlayer)
            UpdateVehicleEnter();
    }

    private void UpdateVehicleEnter()
    {
        if (!isLocalPlayer)
            return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (IsInVehicle)
            {
                if (isServer)
                    CurrentVehicle = null;
                else
                    CmdRequestVehicleUpdate(null);
            }
            else
            {
                Vector3 start = InputManager.WorldMousePosition;
                start.z = -10f;
                var hit = Physics2D.GetRayIntersection(new Ray(start, Vector3.forward));

                if (hit)
                {
                    if (hit.collider.gameObject.CompareTag("Vehicle"))
                    {
                        var veh = hit.collider.GetComponentInParent<Vehicle>();
                        if (veh != null && veh.FreeSpaceCount != 0)
                        {
                            if (isServer)
                                CurrentVehicle = veh;
                            else
                                CmdRequestVehicleUpdate(veh.gameObject);
                        }
                    }
                }
            }
            
        }        
    }

    [Command]
    private void CmdRequestVehicleUpdate(GameObject veh)
    {
        if(veh == null)
        {
            CurrentVehicle = null;
        }
        else
        {
            CurrentVehicle = veh.GetComponent<Vehicle>();
        }
    }
}

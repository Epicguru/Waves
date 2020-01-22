
using Mirror;
using System;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Item))]
[RequireComponent(typeof(NetworkAnimator))]
public class Gun : NetworkBehaviour
{
    public Item Item
    {
        get
        {
            if (_item == null)
                _item = GetComponent<Item>();
            return _item;
        }
    }
    private Item _item;
    public NetworkAnimator NetAnim
    {
        get
        {
            if (_anim == null)
                _anim = GetComponent<NetworkAnimator>();
            return _anim;
        }
    }
    private NetworkAnimator _anim;

    [Header("References")]
    public Transform Muzzle;

    [Header("Shooting")]
    public bool IsAutomatic = false;
    public float RPM = 500f;
    public int MagazineSize = 30;

    [Header("Bullets")]
    public Projectile ProjectilePrefab;

    [Header("Effects")]
    public MuzzleFlashData MuzzleFlash;

    [Header("Volatile")]
    [SyncVar]
    public int CurrentBullets;
    [SyncVar]
    public bool IsReloading;
    public byte ShootRequest;

    public UnityAction OnShoot;
    public UnityAction OnReload;

    private float timer = 0f;
    private byte lastSentRequest = 0;

    private void Update()
    {
        if (hasAuthority) // Only on the person who is actually holding the gun.
        {
            ProcessInputs();
        }

        // Update shooting.
        if (isServer)
        {
            timer += Time.deltaTime;
            if (timer >= 1f / (RPM / 60f))
            {
                bool wantsToShoot = ShootRequest > 0;

                if (wantsToShoot)
                {
                    timer = 0f;
                    Shoot();

                    // In semi-automatic, client sends a increment every time they click them mouse. This way, shooting at max rpm is easier and it is overall more responsive, even on a slow network.
                    if (!IsAutomatic)
                        ShootRequest--;
                }
            }
        }

        // Update animator: moving state.
        if(isServer)
            NetAnim.SetBool("Move", Item.Character?.Movement.IsMoving ?? false);
    }

    private void ProcessInputs()
    {
        if (CurrentBullets <= 0 && Input.GetKey(KeyCode.Mouse0))
            TryReload();

        // Update reloading input.
        if (Input.GetKeyDown(KeyCode.R))
        {
            TryReload();
        }

        if (IsAutomatic)
        {
            bool wantsToShoot = Input.GetKey(KeyCode.Mouse0);
            if(wantsToShoot != (lastSentRequest == 1))
            {
                lastSentRequest = wantsToShoot ? (byte)1 : (byte)0;
                if (!isServer)
                    CmdSendShootRequest(lastSentRequest);
                else
                    ProcessShootRequestState(lastSentRequest);
            }
        }
        else
        {
            if(CurrentBullets > 0 && !IsReloading)
            {
                bool wantsToShoot = Input.GetKeyDown(KeyCode.Mouse0);
                if (wantsToShoot)
                {
                    if (!isServer)
                        CmdSendShootRequest(0); // Send a zero to tell the server to increment the shoot count by 1. Sending a 5 would cause 5 bullets to be shot as if in full-auto, useful for burst mode.
                    else
                        ProcessShootRequestState(0);
                }
            }
        }       
    }

    [Command]
    private void CmdSendShootRequest(byte state)
    {
        ProcessShootRequestState(state);
    }

    [Server]
    private void ProcessShootRequestState(byte state)
    {
        if (IsAutomatic)
        {
            ShootRequest = state;
        }
        else
        {
            if (state == 0)
                ShootRequest++;
            else
                ShootRequest = state;
        }
    }

    [Server]
    public bool Shoot()
    {
        if (CurrentBullets <= 0)
            return false;

        if (IsReloading)
            return false;

        NetAnim.SetTrigger("Shoot");
        return true;
    }

    public void TryReload()
    {
        if (isServer)
        {
            Reload();
        }
        else
        {
            if (isClient && !hasAuthority)
            {
                Debug.LogWarning("No authority to reload. If this item is dropped, you should not try to do authorative actions on it.");
                return;
            }

            if (IsReloading | CurrentBullets >= MagazineSize)
                return;

            CmdReload();
        }
    }

    [Command]
    private void CmdReload()
    {
        Reload();
    }

    [Server]
    public bool Reload()
    {
        if (CurrentBullets >= MagazineSize)
            return false;

        if (IsReloading)
            return false;

        NetAnim.SetTrigger("Reload");
        IsReloading = true;
        return true;
    }

    private void AnimEvent(AnimationEvent e)
    {
        string s = e.stringParameter.Trim().ToLowerInvariant();
        switch (s)
        {
            case "reload":
                if (!isServer)
                    break;

                IsReloading = false;
                CurrentBullets = MagazineSize;
                OnReload?.Invoke();
                break;

            case "shoot":
                if (isServer)
                {
                    // Remove one bullet.
                    CurrentBullets--;

                    // Spawn projectile.
                    if(Muzzle != null)
                        Projectile.Spawn(ProjectilePrefab, Muzzle.position, Muzzle.right);

                    // Invoke event. This does not do any core functionality, it's just so that certain guns can spawn custom effects or similar things.
                    OnShoot?.Invoke();
                }

                // Muzzle flash spawn.
                if (MuzzleFlash != null && Muzzle != null)
                {
                    var prefab = MuzzleFlash.Prefab;
                    if (prefab != null)
                    {
                        var spawned = PoolObject.Spawn(prefab);
                        spawned.transform.position = Muzzle.position;
                        spawned.transform.right = Muzzle.right;
                    }
                }

                break;

            default:
                break;
        }
    }
}

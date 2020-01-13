
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Item))]
public class Gun : MonoBehaviour
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
    public Animator Animator
    {
        get
        {
            if (_anim == null)
                _anim = GetComponent<Animator>();
            return _anim;
        }
    }
    private Animator _anim;

    [Header("References")]
    public Transform Muzzle;

    [Header("Shooting")]
    public bool IsAutomatic = false;
    public float RPM = 500f;
    public int MagazineSize = 30;

    [Header("Effects")]
    public MuzzleFlashData MuzzleFlash;

    [Header("Volatile")]
    public int CurrentBullets;
    public bool IsReloading;

    public UnityAction OnShoot;
    public UnityAction OnReload;

    private float timer = 0f;

    private void Update()
    {        
        // Update shooting timing and input.
        timer += Time.deltaTime;
        if(timer >= 1f / (RPM / 60f))
        {
            if (IsAutomatic ? Input.GetKey(KeyCode.Mouse0) : Input.GetKeyDown(KeyCode.Mouse0))
            {
                timer = 0f;
                Shoot();
            }
        }

        if (CurrentBullets <= 0 && Input.GetKey(KeyCode.Mouse0))
            Reload();

        // Update reloading input.
        if (Input.GetKeyDown(KeyCode.R))
        {
            Reload();
        }

        // Update animator: moving state.
        Animator.SetBool("Move", Item.Character?.Movement.IsMoving ?? false);
    }

    public bool Shoot()
    {
        if (CurrentBullets <= 0)
            return false;

        if (IsReloading)
            return false;

        Animator.SetTrigger("Shoot");
        return true;
    }

    public bool Reload()
    {
        if (CurrentBullets >= MagazineSize)
            return false;

        if (IsReloading)
            return false;

        Animator.SetTrigger("Reload");
        IsReloading = true;
        return true;
    }

    private void AnimEvent(AnimationEvent e)
    {
        string s = e.stringParameter.Trim().ToLowerInvariant();
        switch (s)
        {
            case "reload":
                IsReloading = false;
                CurrentBullets = MagazineSize;
                OnReload?.Invoke();
                break;

            case "shoot":
                CurrentBullets--;
                OnShoot?.Invoke();

                // Muzzle flash spawn.
                if(MuzzleFlash != null && Muzzle != null)
                {
                    var prefab = MuzzleFlash.Prefab;
                    if(prefab != null)
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

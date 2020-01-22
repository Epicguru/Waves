
using Mirror;
using UnityEngine;

[RequireComponent(typeof(Item))]
public class MeleeWeapon : NetworkBehaviour
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
    public NetworkAnimator NetworkAnimator
    {
        get
        {
            if (_netAnim == null)
                _netAnim = GetComponent<NetworkAnimator>();
            return _netAnim;
        }
    }
    private NetworkAnimator _netAnim;

    [Header("Attack")]
    [Range(0.01f, 100f)]
    public float RateOfAttack = 2f;
    public bool UseAltAttack = false;

    private bool attackRequested = false;
    private float timer = 1f;

    private void Update()
    {
        if(Item.Character?.isLocalPlayer ?? false)
        {
            CollectAndSendInput();
        }

        // This logic only runs on the server. The animation is synched to clients through the network animator component.
        if (!isServer)
            return;

        if (!Item.IsHeld)
            return;

        // Update swinging...
        timer += Time.deltaTime;
        float minTime = 1f / RateOfAttack;
        if(timer >= minTime)
        {
            if (attackRequested)
            {
                timer = 0f;
                attackRequested = false;
                TriggerAttack();
            }
        }

        // Update animator: moving state. This is automatically synced to clients.
        NetworkAnimator.SetBool("Move", Item.Character?.Movement.IsMoving ?? false);
    }

    [Client]
    private void CollectAndSendInput()
    {
        if (Input.GetKeyDown(KeyCode.Mouse0))
        {
            SetInput(true);
        }
    }

    [Server]
    public void TriggerAttack()
    {
        NetworkAnimator.SetTrigger("Attack");
    }

    private void SetInput(bool attack)
    {
        if (isServer)
            ServerSetInput(attack);
        else
            CmdSetInput(attack);
    }

    [Command]
    private void CmdSetInput(bool attack)
    {
        ServerSetInput(attack);
    }

    [Server]
    private void ServerSetInput(bool attack)
    {
        attackRequested = attack;
    }
}


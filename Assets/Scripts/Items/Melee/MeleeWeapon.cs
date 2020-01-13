
using UnityEngine;

[RequireComponent(typeof(Item))]
public class MeleeWeapon : MonoBehaviour
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
                _anim = GetComponentInChildren<Animator>();
            return _anim;
        }
    }
    private Animator _anim;

    [Header("Attack")]
    [Range(0.01f, 100f)]
    public float RateOfAttack = 2f;
    public bool UseAltAttack = false;

    private float timer = 1f;

    private void Update()
    {
        // Update swinging...
        timer += Time.deltaTime;
        float minTime = 1f / RateOfAttack;
        if(timer >= minTime)
        {
            if (Input.GetKeyDown(KeyCode.Mouse0))
            {
                timer = 0f;
                TriggerAttack();
            }
        }

        // Update animator: moving state.
        Animator.SetBool("Move", Item.Character?.Movement.IsMoving ?? false);
    }

    public void TriggerAttack()
    {
        Animator.SetTrigger("Attack");
    }
}


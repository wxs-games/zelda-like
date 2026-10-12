using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    public Animator anim;
    public float coolDown = 1f;
    private float timer;
    public Transform attackPoint;
    public LayerMask enemyLayer;
    public StatsUI statsUI;
    private PlayerController controller;

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
    }

    private void Update()
    {
        if (timer > 0)
        {
            timer -= Time.deltaTime;
        }
    }

    public bool CanAttack()
    {
        return timer <= 0;
    }

    public void Attack()
    {
        if (timer <= 0)
        {
            anim.SetBool("isAttacking", true);
            timer = coolDown;
        }
    }

    public void DealDamage()
    {
        StatsManager.Instance.playerDamage += 1;
        statsUI.UpdateDamage();

        Collider2D[] enemies = Physics2D.OverlapCircleAll(attackPoint.position, StatsManager.Instance.playerweaponRange, enemyLayer);
        if (enemies.Length > 0)
        {
            enemies[0].GetComponent<EnemyHealth>().ChangeHealth(-StatsManager.Instance.playerDamage);
            enemies[0].GetComponent<EnemyKnockbackable>().Knockback(transform, StatsManager.Instance.playerKnockbackForce, StatsManager.Instance.playerKnockbackStun);
        }
    }

    public void FinshAttacking()
    {
        anim.SetBool("isAttacking", false);
        if (controller != null)
        {
            controller.ChangeState(PlayerIdleState.Instance);
        }
    }
}
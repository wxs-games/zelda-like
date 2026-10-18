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
        // 挥剑音打在动画的命中帧（DealDamage 由动画事件调用），
        // 而不是 Attack()——后者是起手时刻，提前发声会与刀光错位、听起来很假
        SoundManager.Instance?.PlaySwordSwing();

        // 攻击力只由基础值 + 技能树强化决定，不再随攻击次数累加
        int damage = StatsManager.Instance.playerDamage;

        Collider2D[] enemies = Physics2D.OverlapCircleAll(attackPoint.position, StatsManager.Instance.playerweaponRange, enemyLayer);
        if (enemies.Length > 0)
        {
            enemies[0].GetComponent<EnemyHealth>().ChangeHealth(-damage);
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
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyController : MonoBehaviour
{
    public float damageAmount = 25f;
    public float attackRange = 2;
    public float knockbackForce = 5f;
    public Transform player;
    public Animator anim;
    public Rigidbody2D rb;
    public bool canChase = true;
    public EnemyMovement en;

    private StateMachine<EnemyController> enemyStateMachine;
    private EnemyState currentStateEnum;

    void Start()
    {
        enemyStateMachine = new StateMachine<EnemyController>(this);
        ChangeState(EnemyState.Idle);
    }

    public void ChangeState(EnemyState newState)
    {
        currentStateEnum = newState;
        IEnemyState next = newState switch
        {
            EnemyState.Idle => IdleState.Instance,
            EnemyState.Chasing => ChaseState.Instance,
            EnemyState.Attacking => AttackState.Instance,
            _ => IdleState.Instance
        };
        enemyStateMachine.ChangeState(next);
    }

    public void Attack()
    {
        if (player == null || !player.gameObject.activeInHierarchy)
            return;

        UnityEngine.Debug.Log("Attacking player now!");
        if (Vector2.Distance(player.position, transform.position) <= attackRange)
        {
            player.gameObject.GetComponent<PlayerHealth>().ChangeHealth(-damageAmount);

            // 本次伤害若已致死，PlayerHealth 会停用对象，不再施加击退与硬直
            if (!player.gameObject.activeInHierarchy)
                return;

            player.GetComponent<PlayerKnockbackable>().KnockBack(transform, knockbackForce);
            player.GetComponent<PlayerController>().ChangeState(PlayerKnockbackState.Instance);
        }
    }

    public void StartAttackCooldown(float duration)
    {
        StartCoroutine(AttackCooldownRoutine(duration));
    }

    private IEnumerator AttackCooldownRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        canChase = true;

        // 玩家仍存活且未走出侦测圈（状态未被退出事件切走）时，恢复追击
        if (currentStateEnum == EnemyState.Attacking
            && player != null && player.gameObject.activeInHierarchy)
        {
            ChangeState(EnemyState.Chasing);
        }
    }

    void Update()
    {
        enemyStateMachine.Tick();
    }
}

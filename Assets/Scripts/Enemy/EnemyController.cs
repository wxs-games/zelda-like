using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyController : MonoBehaviour
{
    // 与 HeartHealthUI.hpPerHalfHeart 对齐：10 点 = 半颗心，玩家 100 血 = 5 颗心 = 挨 10 下
    public float damageAmount = 10f;
    public float attackRange = 2;
    public float knockbackForce = 5f;
    public Transform player;
    public Animator anim;
    public Rigidbody2D rb;
    public bool canChase = true;
    public EnemyMovement en;

    [Header("巡视")]
    public Transform[] patrolPoints;
    public float patrolSpeed = 1.5f;
    public float patrolWaitTime = 1.5f;
    [HideInInspector] public int patrolIndex;
    [HideInInspector] public float patrolWait;

    /// <summary>
    /// 进入攻击状态的时刻，供 AttackState 的看门狗使用。
    /// 必须放在敌人自己身上而不是 AttackState 单例里——单例是所有敌人共享的，
    /// 多只敌人同时攻击时会互相覆盖计时。
    /// </summary>
    [HideInInspector] public float attackEnterTime;

    private StateMachine<EnemyController> enemyStateMachine;
    private EnemyState currentStateEnum;

    void Start()
    {
        enemyStateMachine = new StateMachine<EnemyController>(this);
        // 配了路点就从巡视开始，否则保持原站桩
        ChangeState(patrolPoints != null && patrolPoints.Length >= 2
            ? EnemyState.Patrolling
            : EnemyState.Idle);
    }

    public void ChangeState(EnemyState newState)
    {
        currentStateEnum = newState;
        IEnemyState next = newState switch
        {
            EnemyState.Idle => IdleState.Instance,
            EnemyState.Patrolling => PatrolState.Instance,
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

    /// <summary>
    /// 攻击动画收尾（由 Enemy_Torch_Attacking.anim 的动画事件调用）。
    /// 配了路点回巡视，否则回站桩。
    /// </summary>
    public void OnAttackAnimComplete()
    {
        ChangeState(patrolPoints != null && patrolPoints.Length >= 2
            ? EnemyState.Patrolling
            : EnemyState.Idle);
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

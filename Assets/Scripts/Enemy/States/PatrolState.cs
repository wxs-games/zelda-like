using UnityEngine;

public class PatrolState : IEnemyState
{
    public static readonly PatrolState Instance = new PatrolState();

    private const float ReachDistance = 0.15f;

    public void OnEnter(EnemyController e)
    {
        // 路点不足两个视为未配置巡视，回退站桩，避免空引用死循环
        if (e.patrolPoints == null || e.patrolPoints.Length < 2)
        {
            e.ChangeState(EnemyState.Idle);
            return;
        }

        // 巡视移动播跑动帧。原先只置 isIdle=false，但 Moving 状态仅由 isChasing 从
        // Entry 进入，巡视时永远停在待机动画——三个火把怪都配了路点，走的都是巡视。
        e.anim.SetBool("isMoving", true);
        e.anim.SetBool("isIdle", false);
    }

    public void OnUpdate(EnemyController e)
    {
        if (e.patrolPoints == null || e.patrolPoints.Length < 2)
        {
            e.ChangeState(EnemyState.Idle);
            return;
        }

        // 到点停留：原地等待，切待机动画
        if (e.patrolWait > 0f)
        {
            e.patrolWait -= Time.deltaTime;
            e.rb.velocity = Vector2.zero;
            e.anim.SetBool("isIdle", true);
            e.anim.SetBool("isMoving", false);
            return;
        }

        Transform target = e.patrolPoints[e.patrolIndex];
        if (target == null)
        {
            // 路点被删则跳过，指向下一个
            e.patrolIndex = (e.patrolIndex + 1) % e.patrolPoints.Length;
            return;
        }

        e.anim.SetBool("isMoving", true);
        e.anim.SetBool("isIdle", false);
        e.en.Patrol(target.position);

        if (Vector2.Distance(e.transform.position, target.position) <= ReachDistance)
        {
            e.rb.velocity = Vector2.zero;
            e.patrolIndex = (e.patrolIndex + 1) % e.patrolPoints.Length;
            e.patrolWait = e.patrolWaitTime;
        }
    }

    public void OnExit(EnemyController e)
    {
        if (e.rb != null)
        {
            e.rb.velocity = Vector2.zero;
        }

        e.anim.SetBool("isMoving", false);
        e.anim.SetBool("isIdle", false);
    }
}

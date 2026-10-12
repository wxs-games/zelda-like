using UnityEngine;

public class ChaseState : IEnemyState
{
    public static readonly ChaseState Instance = new ChaseState();

    public void OnEnter(EnemyController e)
    {
        e.anim.SetBool("isChasing", true);
    }

    public void OnUpdate(EnemyController e)
    {
        e.en.Chase();

        if (Vector2.Distance(e.transform.position, e.player.position) <= e.attackRange)
        {
            e.rb.velocity = Vector2.zero;
            e.canChase = false;
            e.ChangeState(EnemyState.Attacking);
            e.StartAttackCooldown(2f);
        }
    }

    public void OnExit(EnemyController e)
    {
        e.anim.SetBool("isChasing", false);
    }
}

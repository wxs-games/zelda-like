using UnityEngine;

public class AttackState : IEnemyState
{
    public static readonly AttackState Instance = new AttackState();

    public void OnEnter(EnemyController e)
    {
        e.anim.SetBool("isAttacking", true);
    }

    public void OnUpdate(EnemyController e)
    {
    }

    public void OnExit(EnemyController e)
    {
        e.anim.SetBool("isAttacking", false);
    }
}

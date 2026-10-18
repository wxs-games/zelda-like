using UnityEngine;

public class IdleState : IEnemyState
{
    public static readonly IdleState Instance = new IdleState();

    public void OnEnter(EnemyController e)
    {
        e.anim.SetBool("isIdle", true);
        e.anim.SetBool("isMoving", false);
    }

    public void OnUpdate(EnemyController e)
    {
    }

    public void OnExit(EnemyController e)
    {
        e.anim.SetBool("isIdle", false);
    }
}

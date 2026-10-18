using UnityEngine;

public class AttackState : IEnemyState
{
    public static readonly AttackState Instance = new AttackState();

    public void OnEnter(EnemyController e)
    {
        // 攻击时停下跑动帧，避免叠加播放
        e.anim.SetBool("isMoving", false);
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

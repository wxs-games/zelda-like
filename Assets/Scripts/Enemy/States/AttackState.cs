using UnityEngine;

public class AttackState : IEnemyState
{
    public static readonly AttackState Instance = new AttackState();

    /// <summary>
    /// 看门狗：进入攻击状态后超过这个时长仍未收到动画事件，就强制退出。
    ///
    /// 【为什么需要】
    ///   本状态全靠动画事件 `OnAttackAnimComplete` 退出。一旦动画播不到事件
    ///   （根因实例：Animator 的 AnyState→Attacking 转移开启了 canTransitionToSelf，
    ///   每帧把动画重置到第 0 帧，永远到不了 0.65s 的事件时间点），
    ///   `isAttacking` 会永远为 true，敌人永久卡在攻击状态：不动、不掉血、动画停在首帧。
    ///   真机 + 编辑器 Play 双端实测到过这个死锁（Enemy_Torch 卡在 normTime=0.00）。
    ///   超时兜底保证即使动画链路再出问题，敌人也能自行恢复。
    /// </summary>
    private const float WatchdogSeconds = 2f;

    public void OnEnter(EnemyController e)
    {
        // 攻击时停下跑动帧，避免叠加播放
        e.anim.SetBool("isMoving", false);
        e.anim.SetBool("isAttacking", true);
        // 计时存在敌人自己身上：本状态是单例，存字段里会被所有敌人共享
        e.attackEnterTime = Time.time;
    }

    public void OnUpdate(EnemyController e)
    {
        // 正常情况动画事件会先把状态切走，走不到这里
        if (Time.time - e.attackEnterTime > WatchdogSeconds)
        {
            Debug.LogWarning("[Enemy] 攻击状态超时未收到动画事件，强制退出（检查攻击动画的动画事件）");
            e.OnAttackAnimComplete();
        }
    }

    public void OnExit(EnemyController e)
    {
        e.anim.SetBool("isAttacking", false);
    }
}

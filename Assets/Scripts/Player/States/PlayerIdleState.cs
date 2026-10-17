using UnityEngine;

public class PlayerIdleState : IPlayerState
{
    public static readonly PlayerIdleState Instance = new PlayerIdleState();

    public void OnEnter(PlayerController p)
    {
        p.anim.SetFloat("horizontal", 0f);
        p.anim.SetFloat("vertical", 0f);
        p.playerrb.velocity = Vector2.zero;
    }

    public void OnUpdate(PlayerController p)
    {
        if (GameInput.AttackPressed && p.playerCombat.enabled && p.playerCombat.CanAttack())
        {
            p.ChangeState(PlayerAttackState.Instance);
            return;
        }

        // 范围内没有敌人时不进入射击状态，按键不消耗冷却
        if (GameInput.ShootPressed && p.playerBow.enabled && p.playerBow.CanShoot() && p.playerBow.HasEnemyInRange())
        {
            p.ChangeState(PlayerShootState.Instance);
            return;
        }

        Vector2 moveInput = GameInput.Move;
        if (moveInput.x != 0f || moveInput.y != 0f)
        {
            p.ChangeState(PlayerMoveState.Instance);
        }
    }

    public void OnExit(PlayerController p)
    {
    }
}
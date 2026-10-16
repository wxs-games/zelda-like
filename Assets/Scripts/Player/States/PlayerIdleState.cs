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
        if (Input.GetButtonDown("Slash") && p.playerCombat.enabled && p.playerCombat.CanAttack())
        {
            p.ChangeState(PlayerAttackState.Instance);
            return;
        }

        // 范围内没有敌人时不进入射击状态，按键不消耗冷却
        if (Input.GetButtonDown("Shoot") && p.playerBow.enabled && p.playerBow.CanShoot() && p.playerBow.HasEnemyInRange())
        {
            p.ChangeState(PlayerShootState.Instance);
            return;
        }

        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        if (horizontal != 0f || vertical != 0f)
        {
            p.ChangeState(PlayerMoveState.Instance);
        }
    }

    public void OnExit(PlayerController p)
    {
    }
}
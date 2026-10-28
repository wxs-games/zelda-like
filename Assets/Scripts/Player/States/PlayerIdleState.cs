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
        // 举盾中：站桩格挡，不移动也不攻击。
        // 放在所有分支之前，保证「按着盾键时 J 键不会偷偷挥剑」。
        if (p.IsBlocking)
        {
            p.playerrb.velocity = Vector2.zero;
            return;
        }

        // 手游只有一个攻击键：按当前装备分派——拿剑就砍，拿弓就射。
        // PC 端仍可用独立的 U 键直接射击，方便调试和手柄玩家。
        if (GameInput.AttackPressed)
        {
            if (p.playerBow.enabled)
            {
                // 范围内没有敌人时不进入射击状态，按键不消耗冷却
                if (p.playerBow.CanShoot() && p.playerBow.HasEnemyInRange())
                {
                    p.ChangeState(PlayerShootState.Instance);
                    return;
                }
            }
            else if (p.playerCombat.enabled && p.playerCombat.CanAttack())
            {
                p.ChangeState(PlayerAttackState.Instance);
                return;
            }
        }

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
using UnityEngine;

public class PlayerMoveState : IPlayerState
{
    public static readonly PlayerMoveState Instance = new PlayerMoveState();

    public void OnEnter(PlayerController p)
    {
    }

    public void OnUpdate(PlayerController p)
    {
        // 与 PlayerIdleState 同一套分派：手游只有一个攻击键，按当前装备决定砍还是射
        if (GameInput.AttackPressed)
        {
            if (p.playerBow.enabled)
            {
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

        if (moveInput.x == 0f && moveInput.y == 0f)
        {
            p.ChangeState(PlayerIdleState.Instance);
            return;
        }

        int currentSpeed = StatsManager.Instance.playerSpeed;
        if (GameInput.SprintHeld)
            currentSpeed += 3;

        if ((moveInput.x > 0.1f && p.transform.localScale.x == -1f) || (moveInput.x < -0.1f && p.transform.localScale.x == 1f))
        {
            p.transform.localScale = new Vector3(-1f * p.transform.localScale.x, p.transform.localScale.y, p.transform.localScale.z);
        }

        p.anim.SetFloat("horizontal", Mathf.Abs(moveInput.x));
        p.anim.SetFloat("vertical", Mathf.Abs(moveInput.y));
        p.playerrb.velocity = moveInput * currentSpeed;
    }

    public void OnExit(PlayerController p)
    {
    }
}
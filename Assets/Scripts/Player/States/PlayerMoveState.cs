using UnityEngine;

public class PlayerMoveState : IPlayerState
{
    public static readonly PlayerMoveState Instance = new PlayerMoveState();

    public void OnEnter(PlayerController p)
    {
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
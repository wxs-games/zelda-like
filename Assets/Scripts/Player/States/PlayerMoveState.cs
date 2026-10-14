using UnityEngine;

public class PlayerMoveState : IPlayerState
{
    public static readonly PlayerMoveState Instance = new PlayerMoveState();

    public void OnEnter(PlayerController p)
    {
    }

    public void OnUpdate(PlayerController p)
    {
        if (Input.GetButtonDown("Slash") && p.playerCombat.enabled && p.playerCombat.CanAttack())
        {
            p.ChangeState(PlayerAttackState.Instance);
            return;
        }

        if (Input.GetButtonDown("Shoot") && p.playerBow.enabled && p.playerBow.CanShoot())
        {
            p.ChangeState(PlayerShootState.Instance);
            return;
        }

        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        if (horizontal == 0f && vertical == 0f)
        {
            p.ChangeState(PlayerIdleState.Instance);
            return;
        }

        int currentSpeed = StatsManager.Instance.playerSpeed;
        if (Input.GetKey(KeyCode.LeftShift))
            currentSpeed += 3;

        if ((horizontal > 0.1f && p.transform.localScale.x == -1f) || (horizontal < -0.1f && p.transform.localScale.x == 1f))
        {
            p.transform.localScale = new Vector3(-1f * p.transform.localScale.x, p.transform.localScale.y, p.transform.localScale.z);
        }

        p.anim.SetFloat("horizontal", Mathf.Abs(horizontal));
        p.anim.SetFloat("vertical", Mathf.Abs(vertical));
        p.playerrb.velocity = new Vector2(horizontal, vertical) * currentSpeed;
    }

    public void OnExit(PlayerController p)
    {
    }
}
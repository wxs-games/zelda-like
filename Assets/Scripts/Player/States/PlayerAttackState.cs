using UnityEngine;

public class PlayerAttackState : IPlayerState
{
    public static readonly PlayerAttackState Instance = new PlayerAttackState();

    public void OnEnter(PlayerController p)
    {
        p.playerrb.velocity = Vector2.zero;
        p.playerCombat.Attack();
    }

    public void OnUpdate(PlayerController p)
    {
    }

    public void OnExit(PlayerController p)
    {
    }
}
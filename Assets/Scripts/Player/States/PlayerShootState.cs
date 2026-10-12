using UnityEngine;

public class PlayerShootState : IPlayerState
{
    public static readonly PlayerShootState Instance = new PlayerShootState();

    public void OnEnter(PlayerController p)
    {
        p.playerrb.velocity = Vector2.zero;
        p.playerBow.BeginShoot();
    }

    public void OnUpdate(PlayerController p)
    {
    }

    public void OnExit(PlayerController p)
    {
    }
}

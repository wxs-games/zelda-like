using System.Collections;
using UnityEngine;

public class PlayerKnockbackState : IPlayerState
{
    public static readonly PlayerKnockbackState Instance = new PlayerKnockbackState();

    public void OnEnter(PlayerController p)
    {
        p.StartCoroutine(KnockbackEnd(p));
    }

    public void OnUpdate(PlayerController p)
    {
    }

    public void OnExit(PlayerController p)
    {
    }

    private IEnumerator KnockbackEnd(PlayerController p)
    {
        yield return new WaitForSeconds(0.5f);
        p.playerrb.velocity = Vector2.zero;
        p.ChangeState(PlayerIdleState.Instance);
    }
}

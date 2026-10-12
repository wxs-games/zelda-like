using UnityEngine;

public interface IPlayerState : IState<PlayerController> { }

public class PlayerController : MonoBehaviour
{
    public Animator anim;
    public Rigidbody2D playerrb;
    public PlayerCombat playerCombat;
    public PlayerBow playerBow;

    private StateMachine<PlayerController> playerStateMachine;

    void Awake()
    {
        anim = GetComponent<Animator>();
        playerrb = GetComponent<Rigidbody2D>();
        playerCombat = GetComponent<PlayerCombat>();
        playerBow = GetComponent<PlayerBow>();
        playerStateMachine = new StateMachine<PlayerController>(this);
        playerStateMachine.ChangeState(PlayerIdleState.Instance);
    }

    void Update()
    {
        playerStateMachine.Tick();
    }

    public void ChangeState(IPlayerState next)
    {
        playerStateMachine.ChangeState(next);
    }
}
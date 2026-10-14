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

    void Start()
    {
        // 按能力解锁情况启用战斗组件（在 StatsManager.Awake 之后执行，确保单例已就绪）
        RefreshUnlockedAbilities();
    }

    void Update()
    {
        playerStateMachine.Tick();
    }

    /// <summary>
    /// 根据 StatsManager 中的能力解锁状态，启用对应的战斗组件。
    /// 任何时刻只保留一件武器处于启用状态，默认优先装备已解锁的剑。
    /// </summary>
    public void RefreshUnlockedAbilities()
    {
        playerCombat.enabled = StatsManager.Instance.hasSword;
        playerBow.enabled = StatsManager.Instance.hasBow;

        // 两者都解锁时收敛为单武器启用，与 PlayerChangeEquipment 的互斥切换约定一致
        if (playerCombat.enabled && playerBow.enabled)
        {
            playerBow.enabled = false;
        }
    }

    public void ChangeState(IPlayerState next)
    {
        playerStateMachine.ChangeState(next);
    }
}
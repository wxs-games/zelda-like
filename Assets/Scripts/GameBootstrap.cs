using UnityEngine;

/// <summary>
/// 持久根守卫：整个游戏只允许一个 GameRoot 存活（社区主流的 Bootstrap Prefab 单例模式）。
/// 每个场景都放一个 GameRoot 预制体实例（内含 Player、数据管理器与 HUD）。
/// 切场景后新场景的重复实例在 Awake 立即整体停用并销毁：
/// - SetActive(false) 在父节点 Awake 中执行，子节点的 Awake/OnEnable 不会再跑，
///   避免重复单例、重复 EventSystem、重复玩家在首帧产生副作用；
/// - 旧实例通过 DontDestroyOnLoad 跨场景存活，血量/金币/背包/经验/能力全部保留。
/// </summary>
public class GameBootstrap : MonoBehaviour
{
    public static GameBootstrap Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}

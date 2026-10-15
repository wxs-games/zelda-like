using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 玩家出生点处理：若刚通过 AreaGate 切场景，则把玩家挪到门禁指定的出生位置。
/// Player 随 GameRoot 持久存活后，Start 只在游戏启动时执行一次，
/// 因此改订阅 sceneLoaded：每次新场景加载完成都尝试消费一次出生点。
/// </summary>
public class PlayerSpawn : MonoBehaviour
{
    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
        // 直接从某个场景启动 Play（没有经过门禁）时也走一次，保持原有行为
        ConsumePendingSpawn();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ConsumePendingSpawn();
    }

    private void ConsumePendingSpawn()
    {
        if (AreaGate.HasPendingSpawn)
        {
            transform.position = AreaGate.ConsumePendingSpawn();
        }
    }
}

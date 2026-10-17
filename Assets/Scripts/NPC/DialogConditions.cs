using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 内置条件解析器：DialogManager 注册表之外的两种开箱即用条件，无需逐个 RegisterCondition。
/// 支持的 conditionId 前缀（写进 DialogOption.conditionId 直接用）：
///   item.has.&lt;itemId&gt;         玩家背包持有该道具（数量&gt;0），能力道具按 StatsManager 解锁状态兜底
///   location.visited.&lt;场景名&gt;  玩家本次游戏过程中到访过该场景（跨场景持久，用于"我去过某地"类对话）
/// 未匹配任何前缀返回 false，保持"未注册条件视为不满足"的防误显语义。
/// </summary>
public static class DialogConditions
{
    // 到访过的场景名（静态字段跨场景存活，游戏进程内持久）
    private static readonly HashSet<string> _visitedScenes = new HashSet<string>();

    private const string ItemPrefix = "item.has.";
    private const string VisitedPrefix = "location.visited.";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        // 初始场景加载完成时 sceneLoaded 会触发，之后每个场景切换也都会记录
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _visitedScenes.Add(scene.name);
    }

    /// <summary>尝试按内置前缀解析条件。无法识别的 conditionId 返回 false。</summary>
    public static bool TryEvaluate(string conditionId)
    {
        if (string.IsNullOrEmpty(conditionId)) return false;

        if (conditionId.StartsWith(ItemPrefix))
            return HasItem(conditionId.Substring(ItemPrefix.Length));

        if (conditionId.StartsWith(VisitedPrefix))
            return _visitedScenes.Contains(conditionId.Substring(VisitedPrefix.Length));

        return false;
    }

    /// <summary>背包中是否存在指定 id 且数量大于 0 的道具。</summary>
    private static bool HasItem(string itemId)
    {
        var inv = InventoryManager.Instance;
        if (inv != null && inv.itemSlots != null)
        {
            foreach (var slot in inv.itemSlots)
            {
                if (slot != null && slot.itemSO != null && slot.quantity > 0 && slot.itemSO.id == itemId)
                    return true;
            }
        }

        // 能力道具（剑/弓）以 StatsManager 解锁状态为准，
        // 覆盖"能力解锁但背包槽位引用异常"的情况
        var stats = StatsManager.Instance;
        if (stats != null)
        {
            if (itemId == "sword" && stats.hasSword) return true;
            if (itemId == "bow" && stats.hasBow) return true;
        }
        return false;
    }
}

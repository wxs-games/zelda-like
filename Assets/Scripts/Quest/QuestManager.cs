using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 任务系统单例（挂 GameRoot，随持久根存活）。
/// 职责：四状态流转、目标进度计数（订阅杀敌/拾取事件）、
/// 向 DialogManager 注册任务条件（quest.xxx.&lt;id&gt;）与动作（quest.accept/deliver.&lt;id&gt;）、交付时发奖。
/// </summary>
public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    [Tooltip("本任务线包含的全部任务资产，在 Inspector 里配置")]
    public List<QuestSO> quests = new List<QuestSO>();

    // questId → 状态
    private readonly Dictionary<string, QuestState> _states = new Dictionary<string, QuestState>();
    // questId → 每个 objective 的当前进度
    private readonly Dictionary<string, int[]> _progress = new Dictionary<string, int[]>();

    /// <summary>任务状态或进度变化时触发（UI 订阅后整体刷新）。</summary>
    public static event Action<QuestSO> OnQuestChanged;

    // ========== 生命周期 ==========

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // 与 DialogManager 一致：只销毁重复组件，不动 GameObject
            Destroy(this);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        // 初始化所有任务为未接，并向对话系统注册条件/动作
        foreach (var quest in quests)
        {
            if (quest == null || string.IsNullOrEmpty(quest.questId)) continue;
            if (!_states.ContainsKey(quest.questId))
                _states[quest.questId] = QuestState.NotAccepted;
            RegisterDialogBindings(quest);
        }

        // 订阅全局游戏事件
        EnemyHealth.OnMonsterDefeated += HandleMonsterDefeated;
        Loot.OnItemLooted += HandleItemLooted;
    }

    void OnDestroy()
    {
        // 静态事件必须退订，防止重复订阅
        EnemyHealth.OnMonsterDefeated -= HandleMonsterDefeated;
        Loot.OnItemLooted -= HandleItemLooted;
        if (Instance == this) Instance = null;
    }

    // ========== 对话系统联动 ==========
    //
    // 约定（写进 DialogOption.conditionId / actionId）：
    //   条件：quest.notaccepted.&lt;questId&gt; / quest.inprogress.&lt;questId&gt;
    //        quest.completed.&lt;questId&gt;  / quest.delivered.&lt;questId&gt;
    //   动作：quest.accept.&lt;questId&gt;   / quest.deliver.&lt;questId&gt;

    private void RegisterDialogBindings(QuestSO quest)
    {
        string id = quest.questId;

        DialogManager.RegisterCondition($"quest.notaccepted.{id}",
            () => GetState(id) == QuestState.NotAccepted && DialogManager.CheckCondition(quest.acceptConditionId));
        DialogManager.RegisterCondition($"quest.inprogress.{id}",
            () => GetState(id) == QuestState.InProgress && !IsCompletionReached(quest));
        DialogManager.RegisterCondition($"quest.completed.{id}",
            () => GetState(id) == QuestState.Completed);
        DialogManager.RegisterCondition($"quest.delivered.{id}",
            () => GetState(id) == QuestState.Delivered);

        DialogManager.RegisterAction($"quest.accept.{id}", () => AcceptQuest(quest));
        DialogManager.RegisterAction($"quest.deliver.{id}", () => DeliverQuest(quest));
    }

    // ========== 状态查询（给 UI/其他系统用） ==========

    public QuestState GetState(string questId)
    {
        return _states.TryGetValue(questId, out var s) ? s : QuestState.NotAccepted;
    }

    /// <summary>某任务某目标的当前进度（未接取/无目标时返回 0）。</summary>
    public int GetProgress(string questId, int objectiveIndex)
    {
        if (!_progress.TryGetValue(questId, out var arr)) return 0;
        if (objectiveIndex < 0 || objectiveIndex >= arr.Length) return 0;
        return arr[objectiveIndex];
    }

    /// <summary>日志面板用：进行中或已完成待交付的任务。</summary>
    public List<QuestSO> GetActiveQuests()
    {
        var list = new List<QuestSO>();
        foreach (var quest in quests)
        {
            if (quest == null) continue;
            var state = GetState(quest.questId);
            if (state == QuestState.InProgress || state == QuestState.Completed)
                list.Add(quest);
        }
        return list;
    }

    // ========== 接取 / 交付 ==========

    /// <summary>接取任务：未接 + 接取条件满足才生效。由对话 action 触发。</summary>
    public void AcceptQuest(QuestSO quest)
    {
        if (quest == null) return;
        if (GetState(quest.questId) != QuestState.NotAccepted) return;
        if (!DialogManager.CheckCondition(quest.acceptConditionId))
        {
            Debug.LogWarning($"[Quest] 任务 {quest.questId} 接取条件不满足");
            return;
        }

        _states[quest.questId] = QuestState.InProgress;
        _progress[quest.questId] = new int[quest.objectives != null ? quest.objectives.Count : 0];
        NotifyChanged(quest);
    }

    /// <summary>交付任务：目标全部达成 + 完成条件满足 → 发金币/经验 → Delivered。由对话 action 触发。</summary>
    public void DeliverQuest(QuestSO quest)
    {
        if (quest == null) return;

        // 纯条件任务可能还没经过事件评估，先尝试翻状态
        if (GetState(quest.questId) == QuestState.InProgress)
            EvaluateCompletion(quest);

        if (GetState(quest.questId) != QuestState.Completed)
        {
            Debug.LogWarning($"[Quest] 任务 {quest.questId} 尚未完成，无法交付");
            return;
        }

        // 发放奖励
        if (quest.rewardGold > 0 && InventoryManager.Instance != null)
            InventoryManager.Instance.AddGold(quest.rewardGold);
        if (quest.rewardExp > 0 && ExpManager.Instance != null)
            ExpManager.Instance.GainExperience(quest.rewardExp);

        _states[quest.questId] = QuestState.Delivered;
        NotifyChanged(quest);
    }

    // ========== 进度事件 ==========

    private void HandleMonsterDefeated(string enemyId, int exp)
    {
        AdvanceObjectives(QuestObjectiveType.KillEnemy, enemyId, 1);
    }

    private void HandleItemLooted(ItemSO item, int quantity)
    {
        if (item == null || item.isGold) return; // 金币不算收集目标
        AdvanceObjectives(QuestObjectiveType.CollectItem, item.id, quantity);
    }

    /// <summary>所有进行中的任务，推进匹配类型/目标 ID 的目标计数。</summary>
    private void AdvanceObjectives(QuestObjectiveType type, string targetId, int amount)
    {
        if (string.IsNullOrEmpty(targetId)) return;

        foreach (var quest in quests)
        {
            if (quest == null || GetState(quest.questId) != QuestState.InProgress) continue;
            if (!_progress.TryGetValue(quest.questId, out var arr)) continue;

            bool changed = false;
            for (int i = 0; i < arr.Length && i < quest.objectives.Count; i++)
            {
                var obj = quest.objectives[i];
                if (obj.type != type || obj.targetId != targetId) continue;
                if (arr[i] >= obj.requiredCount) continue; // 已满不再加
                arr[i] = Mathf.Min(obj.requiredCount, arr[i] + amount);
                changed = true;
            }

            if (changed)
            {
                EvaluateCompletion(quest);
                NotifyChanged(quest);
            }
        }
    }

    /// <summary>目标全部达成且完成条件谓词为 true → InProgress 翻 Completed。</summary>
    private void EvaluateCompletion(QuestSO quest)
    {
        if (GetState(quest.questId) != QuestState.InProgress) return;
        if (IsCompletionReached(quest))
        {
            _states[quest.questId] = QuestState.Completed;
            NotifyChanged(quest);
        }
    }

    /// <summary>完成条件：所有目标计数达标（纯条件任务无目标则跳过）且 completionConditionId 谓词为 true。</summary>
    private bool IsCompletionReached(QuestSO quest)
    {
        if (quest.objectives != null && quest.objectives.Count > 0
            && _progress.TryGetValue(quest.questId, out var arr))
        {
            for (int i = 0; i < quest.objectives.Count; i++)
            {
                int current = i < arr.Length ? arr[i] : 0;
                if (current < quest.objectives[i].requiredCount) return false;
            }
        }
        return DialogManager.CheckCondition(quest.completionConditionId);
    }

    private void NotifyChanged(QuestSO quest)
    {
        OnQuestChanged?.Invoke(quest);
    }
}

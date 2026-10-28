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

    /// <summary>
    /// 本局游戏各enemyId 的累计击杀数（不区分是否接了对应任务）。
    ///
    /// 【为什么必须有这个，重要】
    /// 进度原本只靠 EnemyHealth.OnMonsterDefeated 事件累加，而该事件只在
    /// 「任务处于 InProgress」时才对任务生效（见 AdvanceObjectives）。
    /// 于是出现经典边界 bug：**玩家在接取任务前就把敌人全杀光了**，
    /// 接取那一刻 _progress 被清零，而此后再无击杀事件，任务永久卡在 0/N，
    /// 永远无法交付。
    ///
    /// 现在接取时会用这份历史计数回补进度，若目标已达成则在同一次调用里
    /// 直接翻成 Completed，玩家再找 NPC 说话就会命中「已完成」分支直接交付。
    /// 这也顺带覆盖「读档后进度对了但状态没翻」等同类场景。
    /// </summary>
    private static readonly Dictionary<string, int> _killTotals = new Dictionary<string, int>();

    /// <summary>取某enemyId 本局累计击杀数（无记录返回 0）。存档/调试可用。</summary>
    public static int GetTotalKills(string enemyId)
    {
        if (string.IsNullOrEmpty(enemyId)) return 0;
        return _killTotals.TryGetValue(enemyId, out var n) ? n : 0;
    }

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

    private bool _subscribed;

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

        // 订阅全局游戏事件。必须在这里而不是 Awake：
        // Awake 里若是重复实例会 Destroy(this)，那种情况下不能订阅，
        // 否则退订后仍留着一条野回调，导致击杀被记两次。
        if (Instance != this) return;
        if (_subscribed) return;
        _subscribed = true;

        EnemyHealth.OnMonsterDefeated += HandleMonsterDefeated;
        Loot.OnItemLooted += HandleItemLooted;
    }

    void OnDestroy()
    {
        // 静态事件必须退订，防止重复订阅
        if (_subscribed)
        {
            EnemyHealth.OnMonsterDefeated -= HandleMonsterDefeated;
            Loot.OnItemLooted -= HandleItemLooted;
            _subscribed = false;
        }
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

        // 回补接取之前就已经发生的击杀进度。
        // 典型场景：玩家先把火把怪全杀了才去接任务，此时没有任何击杀事件会再来，
        // 不回补的话任务会永远停在 0/N。回补后若已达标，EvaluateCompletion
        // 会在同一次调用里把状态翻成 Completed，玩家再去对话即可直接交付。
        BackfillProgressFromHistory(quest);
        EvaluateCompletion(quest);

        NotifyChanged(quest);
    }

    /// <summary>
    /// 用 _killTotals 里的历史击杀数初始化 KillEnemy 型目标的进度。
    ///
    /// 语义约定：**累计击杀量**（而不是"接取前击杀量"）——
    /// 这样「接任务 → 杀够 → 交付」和「先杀光 → 接任务直接完成」两条路径
    /// 结果一致，不会出现同一存档两种进度的不一致状态。
    /// 已达上限的按 requiredCount 截断，不会溢出。
    /// </summary>
    private void BackfillProgressFromHistory(QuestSO quest)
    {
        if (quest.objectives == null || quest.objectives.Count == 0) return;
        if (!_progress.TryGetValue(quest.questId, out var arr)) return;

        for (int i = 0; i < arr.Length && i < quest.objectives.Count; i++)
        {
            var obj = quest.objectives[i];
            if (obj == null || obj.type != QuestObjectiveType.KillEnemy) continue;
            if (string.IsNullOrEmpty(obj.targetId)) continue;

            int total = GetTotalKills(obj.targetId);
            arr[i] = Mathf.Clamp(total, 0, obj.requiredCount);
        }
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

        _states[quest.questId] = QuestState.Delivered;
        NotifyChanged(quest);
    }

    // ========== 进度事件 ==========

    private void HandleMonsterDefeated(string enemyId, int exp)
    {
        // 先记全局累计，供以后接取任务时回补进度（见 _killTotals 注释）
        if (!string.IsNullOrEmpty(enemyId))
        {
            _killTotals.TryGetValue(enemyId, out var cur);
            _killTotals[enemyId] = cur + 1;
        }

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
            // 完成音放在状态真正翻转的这一刻，而不是接取或交付：
            // 「先杀光再接任务」也会在这里响一次，玩家能立刻听到「这事成了」
            SoundManager.Instance?.PlayQuestComplete();
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

    // ========== 存档导出 / 导入 ==========
    //
    // JsonUtility 不能序列化 Dictionary，所以这里把 _states / _progress
    // 转成可序列化的平行列表交给 SaveSystem，读档时再转回去。

    /// <summary>导出任务状态与进度（存档系统调用）。</summary>
    public QuestsData ExportSaveData()
    {
        var data = new QuestsData();

        foreach (var quest in quests)
        {
            if (quest == null) continue;
            if (GetState(quest.questId) != QuestState.NotAccepted)
                data.activeQuestIds.Add(quest.questId);
        }

        foreach (var kv in _states)
        {
            data.states.Add(new QuestStateEntry { questId = kv.Key, stateValue = (int)kv.Value });
        }

        foreach (var kv in _progress)
        {
            data.progress.Add(new QuestProgressEntry { questId = kv.Key, objectiveProgress = kv.Value });
        }

        foreach (var kv in _killTotals)
        {
            data.killTotals.Add(new KillCountEntry { enemyId = kv.Key, count = kv.Value });
        }

        return data;
    }

    /// <summary>导入任务状态与进度（存档系统调用）。database 用于校验任务 ID 是否存在。</summary>
    public void ImportSaveData(QuestsData data, GameDatabase database)
    {
        if (data == null) return;

        _states.Clear();
        _progress.Clear();

        // 先全部置回未接，保证配置里新增的任务也有默认状态
        foreach (var quest in quests)
        {
            if (quest == null || string.IsNullOrEmpty(quest.questId)) continue;
            _states[quest.questId] = QuestState.NotAccepted;
        }

        foreach (var entry in data.states)
        {
            if (string.IsNullOrEmpty(entry.questId)) continue;
            if (database != null && database.GetQuest(entry.questId) == null)
            {
                Debug.LogWarning($"[Quest] 存档中的任务 ID 不存在，已跳过：{entry.questId}");
                continue;
            }
            _states[entry.questId] = (QuestState)entry.stateValue;
        }

        foreach (var entry in data.progress)
        {
            if (string.IsNullOrEmpty(entry.questId)) continue;
            _progress[entry.questId] = entry.objectiveProgress ?? new int[0];
        }

        // 恢复累计击杀数：老存档没有这个字段，data.killTotals 可能为空，
        // 此时用任务进度反推一个下界，避免读档后接任务无法回补。
        if (data.killTotals != null && data.killTotals.Count > 0)
        {
            _killTotals.Clear();
            foreach (var entry in data.killTotals)
            {
                if (string.IsNullOrEmpty(entry.enemyId)) continue;
                _killTotals[entry.enemyId] = entry.count;
            }
        }
        else
        {
            foreach (var quest in quests)
            {
                if (quest == null || quest.objectives == null) continue;
                if (!_progress.TryGetValue(quest.questId, out var arr)) continue;
                for (int i = 0; i < arr.Length && i < quest.objectives.Count; i++)
                {
                    var obj = quest.objectives[i];
                    if (obj == null || obj.type != QuestObjectiveType.KillEnemy) continue;
                    if (string.IsNullOrEmpty(obj.targetId)) continue;
                    int cur;
                    _killTotals.TryGetValue(obj.targetId, out cur);
                    if (arr[i] > cur) _killTotals[obj.targetId] = arr[i];
                }
            }
        }

        // 通知 UI 全量刷新
        foreach (var quest in quests)
        {
            if (quest != null) NotifyChanged(quest);
        }
    }

    private void NotifyChanged(QuestSO quest)
    {
        OnQuestChanged?.Invoke(quest);
    }
}

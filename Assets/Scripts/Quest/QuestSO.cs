using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 任务状态机（四态）：
/// NotAccepted 未接 → InProgress 进行中 → Completed 已完成(待交付) → Delivered 已交付。
/// </summary>
public enum QuestState
{
    NotAccepted,
    InProgress,
    Completed,
    Delivered
}

/// <summary>任务目标类型：击杀指定敌人 / 收集指定物品。</summary>
public enum QuestObjectiveType
{
    KillEnemy,
    CollectItem
}

/// <summary>
/// 单个任务目标。targetId 含义随 type 变化：
/// KillEnemy  → EnemyHealth.enemyId
/// CollectItem → ItemSO.id
/// </summary>
[Serializable]
public class QuestObjective
{
    [Tooltip("目标类型：击杀敌人 / 收集物品")]
    public QuestObjectiveType type;

    [Tooltip("KillEnemy 填 EnemyHealth.enemyId；CollectItem 填 ItemSO.id")]
    public string targetId;

    [Tooltip("需要的数量")]
    public int requiredCount = 1;

    [Tooltip("任务日志里显示的目标描述（中文），例如：清理火把怪 0/3")]
    public string displayText;
}

/// <summary>
/// 任务数据资产。一个资产 = 一个任务。
/// 接取/完成的额外条件通过 conditionId 引用 DialogManager 的条件注册表
/// （Func<bool> 委托字典），与对话选项的 conditionId 是同一套机制。
/// </summary>
[CreateAssetMenu(fileName = "NewQuest", menuName = "zeldaLike/Quest")]
public class QuestSO : ScriptableObject
{
    [Header("基础信息")]
    [Tooltip("任务内部标识：全小写英文，全局唯一（如 q_torch_cleanup）")]
    public string questId;

    [Tooltip("给玩家看的任务名（中文）")]
    public string title;

    [TextArea]
    [Tooltip("任务描述（中文），显示在任务日志里")]
    public string description;

    [Header("条件（对应 DialogManager 条件注册表里的 conditionId，留空=无条件）")]
    [Tooltip("接取条件：满足后对话里才会出现接任务选项")]
    public string acceptConditionId;

    [Tooltip("完成条件：所有目标达成后，若此项谓词也为 true 才算 Completed（可用于'还要回去和某人对话'之类）")]
    public string completionConditionId;

    [Header("目标")]
    [Tooltip("结构化进度目标。留空表示纯条件任务（只靠 completionConditionId 判定）")]
    public List<QuestObjective> objectives = new List<QuestObjective>();

    [Header("奖励（交付时发放）")]
    [Tooltip("金币奖励，进 InventoryManager.gold")]
    public int rewardGold;
}

using System;
using System.Collections.Generic;

/// <summary>
/// 存档数据结构（纯数据，不含任何 Unity 对象引用）。
///
/// JsonUtility 的两条硬约束决定了这里的形状：
/// 1. 不能序列化 Dictionary —— 所以任务状态/进度用「键 + 值」的平行列表表达；
/// 2. 不能序列化 ScriptableObject 引用 —— 所以道具/技能/任务只存字符串 ID，
///    读档时由 GameDatabase 反查回资产对象。
/// </summary>
[Serializable]
public class SaveData
{
    public const string CurrentVersion = "1.0";

    public string version = CurrentVersion;
    public string savedAtIso;

    /// <summary>存档时的场景名与玩家坐标，用于读档后回到原位置。</summary>
    public string sceneName;
    public float playerX;
    public float playerY;

    public StatsData stats = new StatsData();
    public InventoryData inventory = new InventoryData();
    public SkillData skills = new SkillData();
    public ExpData exp = new ExpData();
    public QuestsData quests = new QuestsData();
}

[Serializable]
public class StatsData
{
    public int playerDamage;
    public int playerSpeed;
    public float playerMaxHealth;
    public float playerCurrentHealth;
    public float playerweaponRange;
    public float playerKnockbackForce;
    public float playerKnockbackStun;
    public bool hasSword;
    public bool hasBow;
}

[Serializable]
public class InventoryData
{
    public int gold;
    /// <summary>每项对应一个背包槽（按索引对齐 InventoryManager.itemSlots）。</summary>
    public List<InventorySlotData> slots = new List<InventorySlotData>();
}

[Serializable]
public class InventorySlotData
{
    /// <summary>ItemSO.id；空槽为空串。</summary>
    public string itemId;
    public int quantity;
}

[Serializable]
public class SkillData
{
    public int availablePoints;
    public List<SkillSlotData> slots = new List<SkillSlotData>();
}

[Serializable]
public class SkillSlotData
{
    /// <summary>SkillSO.skillName；空槽为空串。</summary>
    public string skillName;
    public int currentLevel;
    public bool isUnlocked;
}

[Serializable]
public class ExpData
{
    public int level;
    public int currentExp;
    public int expToLevel;
    public float expGrowMultiplier;
}

/// <summary>任务状态与进度。QuestState 本身是 enum，JsonUtility 不支持字典，
/// 这里存枚举的整数值，由 QuestManager 转回。</summary>
[Serializable]
public class QuestsData
{
    /// <summary>已接取/进行中/已完成/已交付的任务 ID 列表。</summary>
    public List<string> activeQuestIds = new List<string>();

    /// <summary>questId → 状态枚举值。</summary>
    public List<QuestStateEntry> states = new List<QuestStateEntry>();

    /// <summary>questId → 该任务各目标的进度数组。</summary>
    public List<QuestProgressEntry> progress = new List<QuestProgressEntry>();
}

[Serializable]
public class QuestStateEntry
{
    public string questId;
    public int stateValue;
}

[Serializable]
public class QuestProgressEntry
{
    public string questId;
    public int[] objectiveProgress;
}

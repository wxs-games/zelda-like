using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 磁盘存档系统（单存档文件，JSON）。
///
/// 设计要点：
/// - 路径：Application.persistentDataPath/save.json（各平台都能写，Windows 在
///   C:\Users\&lt;用户名&gt;\AppData\LocalLow\&lt;公司名&gt;\&lt;产品名&gt;）；
/// - 落盘内容：属性、背包、金币、技能树、等级经验、任务状态与进度、玩家位置与场景；
/// - SO 引用不进 JSON，只存字符串 ID，读档时靠 GameDatabase 反查回资产；
/// - 触发方式：菜单手动存档/读档 + OnApplicationQuit 退出前自动存档。
///
/// 挂在 GameRoot（持久根）上，随DontDestroyOnLoad 跨场景存活。
/// </summary>
public class SaveSystem : MonoBehaviour
{
    [Tooltip("道具/技能/任务资产库，由 Tools/Setup/Build Game Database 生成")]
    public GameDatabase database;

    public static SaveSystem Instance { get; private set; }

    private const string FileName = "save.json";

    /// <summary>读档时若需要先切场景，数据挂在这里，等场景加载完再落地。</summary>
    private SaveData _pendingRestore;

    public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

    public static bool HasSave => File.Exists(SavePath);

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        database?.BuildIndex();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>退出游戏前自动存档，避免玩家忘记手动存。</summary>
    void OnApplicationQuit()
    {
        SaveGame();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 读档触发的切场景：等场景里各管理器 Start 跑完再应用数据
        if (_pendingRestore == null) return;
        ApplyData(_pendingRestore);
        _pendingRestore = null;
    }

    // ========== 对外接口（UI 按钮绑定） ==========

    /// <summary>手动存档。返回是否成功。</summary>
    public bool SaveGame()
    {
        var data = CaptureData();
        if (data == null)
        {
            Debug.LogWarning("[SaveSystem] 存档失败：关键管理器未就绪");
            return false;
        }

        try
        {
            File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
            Debug.Log($"[SaveSystem] 已存档 → {SavePath}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] 写档失败：{e.Message}");
            return false;
        }
    }

    /// <summary>读档。若存档场景与当前不同会先切场景，返回是否成功。</summary>
    public bool LoadGame()
    {
        var data = ReadData();
        if (data == null) return false;

        if (!string.IsNullOrEmpty(data.sceneName)
            && data.sceneName != SceneManager.GetActiveScene().name
            && Application.CanStreamedLevelBeLoaded(data.sceneName))
        {
            _pendingRestore = data;
            SceneManager.LoadScene(data.sceneName);
            return true;
        }

        ApplyData(data);
        return true;
    }

    /// <summary>删除存档文件（调试与「重新开始」用）。</summary>
    public void DeleteSave()
    {
        if (File.Exists(SavePath)) File.Delete(SavePath);
        Debug.Log("[SaveSystem] 存档已删除");
    }

    // ========== 读盘 ==========

    public SaveData ReadData()
    {
        if (!File.Exists(SavePath))
        {
            Debug.Log("[SaveSystem] 没有存档文件");
            return null;
        }

        try
        {
            var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
            if (data == null)
            {
                Debug.LogError("[SaveSystem] 存档解析失败");
                return null;
            }
            Debug.Log($"[SaveSystem] 已读取存档（版本 {data.version}，存于 {data.savedAtIso}）");
            return data;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] 读档失败：{e.Message}");
            return null;
        }
    }

    // ========== 采集：游戏状态 → SaveData ==========

    private SaveData CaptureData()
    {
        var stats = StatsManager.Instance;
        var inv = InventoryManager.Instance;
        if (stats == null || inv == null) return null;

        var data = new SaveData
        {
            savedAtIso = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            sceneName = SceneManager.GetActiveScene().name
        };

        var player = FindPlayer();
        if (player != null)
        {
            var pos = GetPlayerPosition(player);
            data.playerX = pos.x;
            data.playerY = pos.y;
        }

        CaptureStats(stats, data);
        CaptureInventory(inv, data);
        CaptureExp(data);

        var skills = FindSkillTreeManager();
        if (skills != null) CaptureSkills(skills, data);

        var quest = QuestManager.Instance;
        if (quest != null) CaptureQuests(quest, data);

        return data;
    }

    private static void CaptureStats(StatsManager s, SaveData data)
    {
        data.stats = new StatsData
        {
            playerDamage = s.playerDamage,
            playerSpeed = s.playerSpeed,
            playerMaxHealth = s.playerMaxHealth,
            playerCurrentHealth = s.playerCurrentHealth,
            playerweaponRange = s.playerweaponRange,
            playerKnockbackForce = s.playerKnockbackForce,
            playerKnockbackStun = s.playerKnockbackStun,
            hasSword = s.hasSword,
            hasBow = s.hasBow
        };
    }

    private static void CaptureInventory(InventoryManager inv, SaveData data)
    {
        data.inventory = new InventoryData { gold = inv.gold };
        if (inv.itemSlots == null) return;

        foreach (var slot in inv.itemSlots)
        {
            data.inventory.slots.Add(new InventorySlotData
            {
                itemId = slot.itemSO != null ? slot.itemSO.id : string.Empty,
                quantity = slot.quantity
            });
        }
    }

    private static void CaptureExp(SaveData data)
    {
        var exp = ExpManager.Instance;
        if (exp == null) return;

        data.exp = new ExpData
        {
            level = exp.level,
            currentExp = exp.currentExp,
            expToLevel = exp.expToLevel,
            expGrowMultiplier = exp.expGrowMultiplier
        };
    }

    private static void CaptureSkills(SkillTreeManager tree, SaveData data)
    {
        data.skills = new SkillData { availablePoints = tree.availablePoints };
        if (tree.skillSlots == null) return;

        foreach (var slot in tree.skillSlots)
        {
            data.skills.slots.Add(new SkillSlotData
            {
                skillName = slot.skillSO != null ? slot.skillSO.skillName : string.Empty,
                currentLevel = slot.currentLevel,
                isUnlocked = slot.isUnlocked
            });
        }
    }

    private static void CaptureQuests(QuestManager quest, SaveData data)
    {
        data.quests = quest.ExportSaveData();
    }

    // ========== 应用：SaveData → 游戏状态 ==========

    private void ApplyData(SaveData data)
    {
        if (database == null)
        {
            Debug.LogError("[SaveSystem] 未配置 GameDatabase，无法按 ID 还原资产。" +
                           "请执行菜单 Tools/Setup/Build Game Database 并把生成的资产拖到本组件上。");
            return;
        }
        database.BuildIndex();

        ApplyPlayerPosition(data);
        ApplyStats(data.stats);
        ApplyInventory(data.inventory);
        ApplyExp(data.exp);
        ApplySkills(data.skills);
        ApplyQuests(data.quests);

        Debug.Log("[SaveSystem] 读档完成");
    }

    private static void ApplyPlayerPosition(SaveData data)
    {
        var player = FindPlayer();
        if (player == null) return;

        // 玩家是刚体：直接写 position，避免用 velocity 被状态机当帧清零
        var body = player.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.position = new Vector2(data.playerX, data.playerY);
            // 清零速度与角速度，否则跨场景读档时旧场景残留的速度会让玩家
            // 从存档点继续漂移，导致「读档后位置对不上」。
            body.velocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
        else player.transform.position = new Vector2(data.playerX, data.playerY);
    }

    /// <summary>
    /// 读取玩家世界坐标。与 <see cref="ApplyPlayerPosition"/> 保持同一口径：
    /// 有刚体时以 Rigidbody2D.position 为准，否则回退 transform.position。
    /// </summary>
    private static Vector3 GetPlayerPosition(PlayerController player)
    {
        var body = player.GetComponent<Rigidbody2D>();
        return body != null ? (Vector3)body.position : player.transform.position;
    }

    private static void ApplyStats(StatsData s)
    {
        var stats = StatsManager.Instance;
        if (stats == null || s == null) return;

        stats.playerDamage = s.playerDamage;
        stats.playerSpeed = s.playerSpeed;
        stats.playerMaxHealth = s.playerMaxHealth;
        stats.playerCurrentHealth = s.playerCurrentHealth;
        stats.playerweaponRange = s.playerweaponRange;
        stats.playerKnockbackForce = s.playerKnockbackForce;
        stats.playerKnockbackStun = s.playerKnockbackStun;
        stats.hasSword = s.hasSword;
        stats.hasBow = s.hasBow;

        if (stats.playerHealthText != null)
            stats.playerHealthText.text = "HP:" + stats.playerCurrentHealth + "/" + stats.playerMaxHealth;
        if (stats.statsUI != null) stats.statsUI.UpdateAllStats();
    }

    private void ApplyInventory(InventoryData inv)
    {
        var manager = InventoryManager.Instance;
        if (manager == null || inv == null) return;

        manager.gold = inv.gold;
        if (manager.goldText != null) manager.goldText.text = manager.gold.ToString();

        if (manager.itemSlots == null) return;

        for (int i = 0; i < manager.itemSlots.Length && i < inv.slots.Count; i++)
        {
            var slot = manager.itemSlots[i];
            var saved = inv.slots[i];

            slot.itemSO = string.IsNullOrEmpty(saved.itemId) ? null : database.GetItem(saved.itemId);
            if (slot.itemSO == null && !string.IsNullOrEmpty(saved.itemId))
                Debug.LogWarning($"[SaveSystem] 道具 ID 在 GameDatabase 中不存在：{saved.itemId}");

            slot.quantity = slot.itemSO != null ? saved.quantity : 0;
            slot.UpdateUI();
        }
    }

    private static void ApplyExp(ExpData e)
    {
        var exp = ExpManager.Instance;
        if (exp == null || e == null) return;

        exp.level = e.level;
        exp.currentExp = e.currentExp;
        exp.expToLevel = e.expToLevel;
        exp.expGrowMultiplier = e.expGrowMultiplier;
        exp.UpdateUI();
    }

    private void ApplySkills(SkillData s)
    {
        var tree = FindSkillTreeManager();
        if (tree == null || s == null) return;

        tree.availablePoints = s.availablePoints;
        if (tree.skillSlots != null)
        {
            for (int i = 0; i < tree.skillSlots.Length && i < s.slots.Count; i++)
            {
                var slot = tree.skillSlots[i];
                var saved = s.slots[i];

                slot.skillSO = string.IsNullOrEmpty(saved.skillName) ? null : database.GetSkill(saved.skillName);
                if (slot.skillSO == null && !string.IsNullOrEmpty(saved.skillName))
                    Debug.LogWarning($"[SaveSystem] 技能名在 GameDatabase 中不存在：{saved.skillName}");

                slot.currentLevel = saved.currentLevel;
                slot.isUnlocked = saved.isUnlocked;
                slot.RefreshUI();
            }
        }
        tree.UpdateAbilityPoints(0); // 顺带刷新点数文本
    }

    private void ApplyQuests(QuestsData q)
    {
        var quest = QuestManager.Instance;
        if (quest == null || q == null) return;

        quest.ImportSaveData(q, database);
    }

    // ========== 查找辅助 ==========

    private static PlayerController FindPlayer()
    {
        // 玩家随 GameRoot 持久存活，场景里可能同时存在已停用的旧实例，取活动的那个
        return FindObjectOfType<PlayerController>();
    }

    private static SkillTreeManager FindSkillTreeManager()
    {
        return FindObjectOfType<SkillTreeManager>();
    }
}

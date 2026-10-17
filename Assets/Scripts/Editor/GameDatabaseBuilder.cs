using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 菜单 Tools/Setup/Build Game Database：扫描工程内全部 ItemSO / SkillSO / QuestSO，
/// 生成或更新 GameDatabase.asset。
/// 新增了可序列化的 SO 资产（道具/技能/任务）后重跑一次，存档系统才能按 ID 找到它们。
/// </summary>
public static class GameDatabaseBuilder
{
    private const string DatabasePath = "Assets/Settings/GameDatabase.asset";

    [MenuItem("Tools/Setup/Build Game Database")]
    public static void Build()
    {
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
        if (db == null)
        {
            EnsureFolder("Assets/Settings");
            db = ScriptableObject.CreateInstance<GameDatabase>();
            AssetDatabase.CreateAsset(db, DatabasePath);
            Debug.Log("[GameDatabaseBuilder] 已创建 GameDatabase 资产");
        }

        db.items = new List<ItemSO>(FindAll<ItemSO>());
        db.skills = new List<SkillSO>(FindAll<SkillSO>(IsUsableSkill));
        db.quests = new List<QuestSO>(FindAll<QuestSO>());

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 生成后立刻用未保存的内存内容建索引，方便马上进 Play 验证
        db.BuildIndex();

        Debug.Log($"[GameDatabaseBuilder] 完成：道具 {db.items.Count}、技能 {db.skills.Count}、任务 {db.quests.Count}");
    }

    private static List<T> FindAll<T>(System.Predicate<T> accept = null) where T : ScriptableObject
    {
        var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        var list = new List<T>(guids.Length);
        foreach (var guid in guids)
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset == null) continue;
            if (accept != null && !accept(asset)) continue;
            list.Add(asset);
        }
        list.Sort((a, b) => string.Compare(a.name, b.name));
        return list;
    }

    /// <summary>
    /// 过滤掉「占位空壳」技能资产：建表期残留的空对象（skillName 为空、maxLevel 为 0）、
    /// 或尚未填好名称与等级的半成品。这类资产进了数据库会以空字符串为键污染索引，
    /// 读档反查时拿到 null。真正的空壳资产应由人工确认后删除。
    /// </summary>
    private static bool IsUsableSkill(SkillSO skill)
    {
        if (string.IsNullOrWhiteSpace(skill.skillName)) return false;
        if (skill.maxLevel <= 0) return false;
        return true;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        var parts = path.Split('/');
        var current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            var next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }
}

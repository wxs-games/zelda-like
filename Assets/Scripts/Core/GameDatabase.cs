using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 资产数据库：集中列出工程内全部可序列化的 SO（道具/技能/任务），
/// 供存档系统按字符串 ID 反查回 ScriptableObject 引用。
///
/// 为什么需要它：JSON 不能直接序列化 ScriptableObject 引用，
/// 存档只能存 ItemSO.id / QuestSO.questId / SkillSO.skillName，
/// 读档时必须靠本表把 ID 换回真正的资产对象。
///
/// 由编辑器菜单 Tools/Setup/Build Game Database 扫描工程自动生成内容，
/// 新增 SO 资产后重跑一次菜单即可。
/// </summary>
[CreateAssetMenu(fileName = "GameDatabase", menuName = "zeldaLike/Game Database")]
public class GameDatabase : ScriptableObject
{
    public List<ItemSO> items = new List<ItemSO>();
    public List<SkillSO> skills = new List<SkillSO>();
    public List<QuestSO> quests = new List<QuestSO>();

    // 运行时索引：资产在编辑器里赋值，字典每次进 Play 重建（不进序列化）
    private Dictionary<string, ItemSO> _itemById;
    private Dictionary<string, SkillSO> _skillByName;
    private Dictionary<string, QuestSO> _questById;

    /// <summary>建立 ID → SO 索引。重复调用是安全的（先清空再重建）。</summary>
    public void BuildIndex()
    {
        _itemById = new Dictionary<string, ItemSO>();
        _skillByName = new Dictionary<string, SkillSO>();
        _questById = new Dictionary<string, QuestSO>();

        foreach (var item in items)
        {
            if (item == null || string.IsNullOrEmpty(item.id)) continue;
            if (!_itemById.ContainsKey(item.id)) _itemById.Add(item.id, item);
            else Debug.LogWarning($"[GameDatabase] 道具 ID 重复：{item.id}，已保留首个", this);
        }

        foreach (var skill in skills)
        {
            if (skill == null || string.IsNullOrEmpty(skill.skillName)) continue;
            if (!_skillByName.ContainsKey(skill.skillName)) _skillByName.Add(skill.skillName, skill);
            else Debug.LogWarning($"[GameDatabase] 技能名重复：{skill.skillName}，已保留首个", this);
        }

        foreach (var quest in quests)
        {
            if (quest == null || string.IsNullOrEmpty(quest.questId)) continue;
            if (!_questById.ContainsKey(quest.questId)) _questById.Add(quest.questId, quest);
            else Debug.LogWarning($"[GameDatabase] 任务 ID 重复：{quest.questId}，已保留首个", this);
        }
    }

    public ItemSO GetItem(string id)
    {
        if (_itemById == null) BuildIndex();
        return !string.IsNullOrEmpty(id) && _itemById.TryGetValue(id, out var v) ? v : null;
    }

    public SkillSO GetSkill(string name)
    {
        if (_skillByName == null) BuildIndex();
        return !string.IsNullOrEmpty(name) && _skillByName.TryGetValue(name, out var v) ? v : null;
    }

    public QuestSO GetQuest(string id)
    {
        if (_questById == null) BuildIndex();
        return !string.IsNullOrEmpty(id) && _questById.TryGetValue(id, out var v) ? v : null;
    }
}

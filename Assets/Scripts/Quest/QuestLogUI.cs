using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 任务日志面板：按 ToggleQuestLog（L 键）开关。
/// 惯例与 StatsUI / ToggleSkillTree 一致：CanvasGroup 显隐 + timeScale=0 暂停 + 面板互斥。
/// 内容由 QuestManager.GetActiveQuests() 动态生成；任务状态变化时自动刷新。
/// </summary>
public class QuestLogUI : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("整个面板的 CanvasGroup（控制显隐和射线）")]
    public CanvasGroup panelCanvas;

    [Tooltip("任务条目的父容器（ScrollRect 的 Content，带 VerticalLayoutGroup）")]
    public Transform entryContainer;

    [Tooltip("没有进行中任务时显示的提示")]
    public GameObject emptyHint;

    private bool _open;
    public bool IsOpen => _open;

    void OnEnable()
    {
        QuestManager.OnQuestChanged += HandleQuestChanged;
    }

    void OnDisable()
    {
        QuestManager.OnQuestChanged -= HandleQuestChanged;
    }

    void Start()
    {
        // 初始隐藏
        panelCanvas.alpha = 0;
        panelCanvas.blocksRaycasts = false;
        panelCanvas.interactable = false;
        Refresh();
    }

    void Update()
    {
        if (GameInput.ToggleQuestLogPressed)
        {
            if (_open) ClosePanel();
            else OpenPanel();
        }
    }

    // 打开任务日志：与属性/技能面板互斥，暂停游戏后刷新内容
    public void OpenPanel()
    {
        // 对话进行中不允许打开（避免与对话框的 timeScale/输入冲突）
        if (DialogManager.Instance != null && DialogManager.Instance.IsInDialog) return;

        var stats = FindObjectOfType<StatsUI>();
        if (stats != null && stats.IsOpen) stats.ClosePanel();
        var skill = FindObjectOfType<ToggleSkillTree>();
        if (skill != null && skill.IsOpen) skill.ClosePanel();

        Time.timeScale = 0;
        panelCanvas.alpha = 1;
        panelCanvas.blocksRaycasts = true;
        panelCanvas.interactable = true;
        _open = true;
        Refresh();
    }

    public void ClosePanel()
    {
        panelCanvas.alpha = 0;
        panelCanvas.blocksRaycasts = false;
        panelCanvas.interactable = false;
        _open = false;
        Time.timeScale = 1;
    }

    private void HandleQuestChanged(QuestSO quest)
    {
        if (_open) Refresh();
    }

    // ========== 内容生成 ==========

    private void Refresh()
    {
        if (entryContainer == null || QuestManager.Instance == null) return;

        // 清掉旧条目
        for (int i = entryContainer.childCount - 1; i >= 0; i--)
            Destroy(entryContainer.GetChild(i).gameObject);

        var quests = QuestManager.Instance.GetActiveQuests();
        if (emptyHint != null) emptyHint.SetActive(quests.Count == 0);

        foreach (var quest in quests)
            CreateEntry(quest);
    }

    private void CreateEntry(QuestSO quest)
    {
        var row = new GameObject($"Quest_{quest.questId}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        row.transform.SetParent(entryContainer, false);
        var img = row.GetComponent<Image>();
        img.color = new Color(0.12f, 0.12f, 0.14f, 0.95f);
        var le = row.GetComponent<LayoutElement>();
        le.minHeight = 110;
        le.flexibleWidth = 1;

        var vlg = row.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(12, 12, 8, 8);
        vlg.spacing = 4;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var state = QuestManager.Instance.GetState(quest.questId);
        string stateText = state == QuestState.Completed ? "（可交付）" : "（进行中）";
        Color stateColor = state == QuestState.Completed ? new Color(1f, 0.85f, 0.3f) : new Color(0.7f, 0.85f, 1f);

        CreateText(row.transform, quest.title + stateText, 20, FontStyle.Bold, stateColor);
        CreateText(row.transform, quest.description, 15, FontStyle.Normal, new Color(0.8f, 0.8f, 0.8f));

        // 目标进度
        if (quest.objectives != null)
        {
            for (int i = 0; i < quest.objectives.Count; i++)
            {
                var obj = quest.objectives[i];
                int current = Mathf.Min(QuestManager.Instance.GetProgress(quest.questId, i), obj.requiredCount);
                string label = string.IsNullOrEmpty(obj.displayText)
                    ? $"{obj.type} {obj.targetId}"
                    : obj.displayText;
                bool done = current >= obj.requiredCount;
                CreateText(row.transform, $"  • {label}：{current}/{obj.requiredCount}", 15,
                    FontStyle.Normal, done ? new Color(0.45f, 0.85f, 0.45f) : Color.white);
            }
        }
    }

    private void CreateText(Transform parent, string content, int fontSize, FontStyle style, Color color)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var txt = go.GetComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        txt.fontSize = fontSize;
        txt.fontStyle = style;
        txt.color = color;
        txt.text = content;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        var le = go.AddComponent<LayoutElement>();
        le.flexibleWidth = 1;
    }
}

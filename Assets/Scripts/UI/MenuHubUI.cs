using UnityEngine;

/// <summary>
/// 菜单中枢面板：点右上菜单按钮展开（再点收起），条目点击打开对应面板。
/// 中枢自身不暂停游戏（不碰 timeScale），暂停由各面板自己的 OpenPanel 处理。
/// </summary>
public class MenuHubUI : MonoBehaviour
{
    [Tooltip("中枢面板的 CanvasGroup")]
    public CanvasGroup hubCanvas;

    public StatsUI statsUI;
    public ToggleSkillTree skillTreeUI;
    public QuestLogUI questLogUI;

    private bool _open;
    public bool IsOpen => _open;

    void Start()
    {
        CloseHub();
    }

    public void ToggleHub()
    {
        if (_open) CloseHub();
        else OpenHub();
    }

    public void OpenHub()
    {
        if (DialogManager.Instance != null && DialogManager.Instance.IsInDialog) return;
        if (hubCanvas == null) return;
        hubCanvas.alpha = 1;
        hubCanvas.blocksRaycasts = true;
        hubCanvas.interactable = true;
        _open = true;
    }

    public void CloseHub()
    {
        if (hubCanvas == null) return;
        hubCanvas.alpha = 0;
        hubCanvas.blocksRaycasts = false;
        hubCanvas.interactable = false;
        _open = false;
    }

    // 三个条目按钮的 OnClick 绑定
    public void OpenStats()
    {
        CloseHub();
        if (statsUI != null) statsUI.OpenPanel();
    }

    public void OpenSkillTree()
    {
        CloseHub();
        if (skillTreeUI != null) skillTreeUI.OpenPanel();
    }

    public void OpenQuestLog()
    {
        CloseHub();
        if (questLogUI != null) questLogUI.OpenPanel();
    }
}

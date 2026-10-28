using System;
using TMPro;
using UnityEngine;

/// <summary>
/// 菜单中枢面板：点右上菜单按钮展开（再点收起），条目点击打开对应面板。
/// 中枢自身不暂停游戏（不碰 timeScale），暂停由各面板自己的 OpenPanel 处理。
///
/// 【为什么展开时要把触屏控件整体让位——真机死锁，务必保留】
///   本面板挂在 TouchCanvas 下，且是 SafeAreaRoot 的**后一个兄弟节点**（绘制在上层），
///   矩形又铺满全屏 (0,0)-(1275,607)。展开后 blocksRaycasts=true，
///   会把 SafeAreaRoot 里的摇杆、攻击键、切换键、菜单键**全部盖死**。
///   而关闭方式恰恰是「再点一次右上角的 MenuButton」——那个按钮正被自己盖住，
///   于是形成死锁：菜单一开就再也关不掉，玩家只能重启游戏。
///   面板内 5 个条目都是「打开别的面板」，没有关闭项，所以没有任何兜底出路。
///
///   故展开时通知 TouchUIController 挂起触屏控件的射线，关闭时恢复。
///   注意只关射线、不关 activeSelf——摇杆要保持可见，玩家才知道还能不能操作。
/// </summary>
public class MenuHubUI : MonoBehaviour
{
    [Tooltip("中枢面板的 CanvasGroup")]
    public CanvasGroup hubCanvas;

    public StatsUI statsUI;
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

    /// <summary>中枢展开时把触屏控件让位，关闭时归还。用 GetActive 而非 FindObjectOfType，
    /// 避免每次开关都做一次全场搜索。</summary>
    private static void SetTouchControlsSuspended(bool suspended)
    {
        var touch = TouchUIController.Active;
        if (touch != null) touch.Suspend(suspended);
    }

    public void OpenHub()
    {
        if (DialogManager.Instance != null && DialogManager.Instance.IsInDialog) return;
        if (hubCanvas == null) return;
        hubCanvas.alpha = 1;
        hubCanvas.blocksRaycasts = true;
        hubCanvas.interactable = true;
        _open = true;
        SetTouchControlsSuspended(true);
    }

    public void CloseHub()
    {
        if (hubCanvas == null) return;
        hubCanvas.alpha = 0;
        hubCanvas.blocksRaycasts = false;
        hubCanvas.interactable = false;
        _open = false;
        SetTouchControlsSuspended(false);
    }

    /// <summary>菜单面板的关闭按钮（新增）。此前面板内没有任何关闭项，
    /// 唯一的收起方式是点右上角菜单键，而那又会被本面板盖住，必然卡死。</summary>
    public void CloseFromPanel()
    {
        CloseHub();
    }

    // 三个条目按钮的 OnClick 绑定
    public void OpenStats()
    {
        CloseHub();
        if (statsUI != null) statsUI.OpenPanel();
    }

    public void OpenQuestLog()
    {
        CloseHub();
        if (questLogUI != null) questLogUI.OpenPanel();
    }

    // ========== 存档条目 ==========

    [Tooltip("手动存档按钮（留空则按钮仍可用，只是不提示路径）")]
    public TMP_Text saveHint;

    public void SaveGame()
    {
        CloseHub();
        if (SaveSystem.Instance == null)
        {
            Debug.LogWarning("[MenuHubUI] SaveSystem 未就绪，无法存档");
            return;
        }
        if (SaveSystem.Instance.SaveGame() && saveHint != null)
            saveHint.text = "已存档 " + DateTime.Now.ToString("HH:mm:ss");
    }

    public void LoadGame()
    {
        CloseHub();
        if (SaveSystem.Instance == null)
        {
            Debug.LogWarning("[MenuHubUI] SaveSystem 未就绪，无法读档");
            return;
        }
        if (!SaveSystem.HasSave)
        {
            Debug.LogWarning("[MenuHubUI] 还没有存档");
            if (saveHint != null) saveHint.text = "暂无存档";
            return;
        }
        SaveSystem.Instance.LoadGame();
    }
}

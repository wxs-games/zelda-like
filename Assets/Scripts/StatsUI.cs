using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Threading;
using System.Security.AccessControl;

public class StatsUI : MonoBehaviour
{
    public GameObject[] statsSlots;
    public CanvasGroup statsCanvas;
    private bool statsOpen = false;
    public bool IsOpen => statsOpen;

    private void Start()
    {
      UpdateAllStats();
    }
    private void Update()
    {
        if (GameInput.ToggleStatsPressed)
        {
            if (statsOpen) ClosePanel();
            else OpenPanel();
        }
    }

    // 打开属性面板：先关闭技能面板（互斥），再暂停游戏并显示自己。
    // 暂停统一使用 timeScale（与技能面板一致），不再分别禁用各组件。
    public void OpenPanel()
    {
        var skillTree = FindObjectOfType<ToggleSkillTree>();
        if (skillTree != null && skillTree.IsOpen) skillTree.ClosePanel();

        Time.timeScale = 0;
        statsCanvas.alpha = 1;
        statsCanvas.blocksRaycasts = true;
        statsOpen = true;
        UpdateAllStats();
    }

    public void ClosePanel()
    {
        statsCanvas.alpha = 0;
        statsCanvas.blocksRaycasts = false;
        statsOpen = false;
        // 两面板互斥：关闭时另一个必然也关着，可以安全恢复时间
        Time.timeScale = 1;
    }
    public void UpdateDamage()
    {
        if (statsSlots == null || statsSlots.Length < 1) return;
        statsSlots[0].GetComponentInChildren<TMP_Text>().text = "Damage: " + StatsManager.Instance.playerDamage.ToString();
    }
    public void UpdateSpeed()
    {
        if (statsSlots == null || statsSlots.Length < 2) return;
        statsSlots[1].GetComponentInChildren<TMP_Text>().text = "Speed: " + StatsManager.Instance.playerSpeed.ToString();
    }
    public void UpdateAllStats()
    {
        UpdateDamage();
        UpdateSpeed();
    }
}

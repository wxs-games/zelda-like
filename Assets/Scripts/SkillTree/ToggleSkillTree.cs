using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class ToggleSkillTree : MonoBehaviour
{
    public CanvasGroup statsCanvas;
    private bool skillTreeOpen = false;
    public bool IsOpen => skillTreeOpen;

    void Update()
    {
        if (GameInput.ToggleSkillTreePressed)
        {
            if (skillTreeOpen) ClosePanel();
            else OpenPanel();
        }
    }

    // 打开技能面板：先关闭属性面板（互斥），再暂停游戏并显示自己
    public void OpenPanel()
    {
        var stats = FindObjectOfType<StatsUI>();
        if (stats != null && stats.IsOpen) stats.ClosePanel();

        Time.timeScale = 0;
        statsCanvas.alpha = 1;
        statsCanvas.blocksRaycasts = true;
        skillTreeOpen = true;
    }

    public void ClosePanel()
    {
        Time.timeScale = 1;
        statsCanvas.alpha = 0;
        statsCanvas.blocksRaycasts = false;
        skillTreeOpen = false;
    }
}

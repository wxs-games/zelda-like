using UnityEngine;

/// <summary>
/// 手游运行时适配（挂在 GameRoot）：
///
/// 1. **切后台暂停** —— 手机上切出去（来电、微信、切任务）Unity 收到
///    OnApplicationPause(true)。若不暂停，回来时游戏会「自己往前跑」，
///    玩家可能因为操作延迟被怪打死；而低端机切后台再回来常被系统直接回收进程，
///    没存档就丢进度。所以切后台必须：暂停 + 自动存档。
///
/// 2. **自动存档** —— 与 SaveSystem 的退出自动存档配合，构成
///    「切后台/退出都存一次」的双保险。
///
/// 3. **帧率与休眠** —— 移动端统一 60 帧上限、禁止自动熄屏（战斗中被锁屏很糟）。
///    帧率走代码而非 PlayerSettings，是因为编辑器与各平台 QualitySettings 的
///    vSync 行为不一致，代码里设一次最可靠。
///
/// 4. **多点触控** —— 明确请求多点支持，否则部分机型只上报第一个触点，
///    摇杆 + 攻击键同时按会失效。
/// </summary>
public class MobileRuntime : MonoBehaviour
{
    [Tooltip("切后台时是否自动存档（强烈建议保持开启）")]
    public bool autoSaveOnPause = true;

    [Tooltip("移动端目标帧率")]
    public int targetFrameRate = 60;

    [Tooltip("是否禁止自动熄屏")]
    public bool preventSleep = true;

    private bool _wasPlayingBeforePause;

    void Awake()
    {
        ApplyRuntimeSettings();
    }

    private void ApplyRuntimeSettings()
    {
        Application.targetFrameRate = targetFrameRate > 0 ? targetFrameRate : 60;
        QualitySettings.vSyncCount = 0;

        if (preventSleep) Screen.sleepTimeout = SleepTimeout.NeverSleep;

        // 移动端多点触控：摇杆与动作键需要能同时响应
        Input.multiTouchEnabled = true;

        // runInBackground=false：切到后台就停止渲染，省电且避免系统回收进程
        Application.runInBackground = false;
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        // 暂停瞬间记录，恢复时才知道该不该解除暂停
        if (pauseStatus)
        {
            _wasPlayingBeforePause = !IsAnyPanelPaused();
        }

        if (!pauseStatus)
        {
            // 恢复：只解除「切后台造成的」暂停，不能踩掉玩家自己打开的面板
            if (_wasPlayingBeforePause) Time.timeScale = 1f;
            return;
        }

        if (autoSaveOnPause && SaveSystem.Instance != null)
        {
            SaveSystem.Instance.SaveGame();
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        // 部分安卓机型（分屏/画中画）只触发 Focus 而不触发 Pause
        if (hasFocus) return;
        if (autoSaveOnPause && SaveSystem.Instance != null)
        {
            SaveSystem.Instance.SaveGame();
        }
    }

    /// <summary>当前是否已被某个 UI 面板暂停（避免恢复时误把面板关掉）。</summary>
    private static bool IsAnyPanelPaused()
    {
        if (DialogManager.Instance != null && DialogManager.Instance.IsInDialog) return true;
        return Time.timeScale <= 0f;
    }
}
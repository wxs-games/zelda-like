using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 触屏控件显隐门控：
/// 1) 总开关——手机平台默认开启，PC/编辑器默认关闭（forceShow 可强制预览）；
/// 2) 游戏进行中才显示——timeScale=0（面板/商店/对话暂停）或对话进行中一律隐藏，
///    防止暂停期摇杆残留移动输入、跨 Canvas 射线穿透；
/// 3) 检测到实体手柄时隐藏，避免虚拟手柄与实体手柄输入打架。
/// </summary>
public class TouchUIController : MonoBehaviour
{
    [Tooltip("触屏控件根（摇杆区+按钮簇+菜单按钮）")]
    public GameObject controlsRoot;

    [Tooltip("PC/编辑器强制显示触屏控件（预览测试用）")]
    public bool forceShow;

    void Update()
    {
        bool should = ShouldShow();
        if (controlsRoot != null && controlsRoot.activeSelf != should)
            controlsRoot.SetActive(should);
    }

    private bool ShouldShow()
    {
        if (!forceShow && !Application.isMobilePlatform) return false;
        if (HasPhysicalGamepad()) return false;
        if (Time.timeScale <= 0f) return false;
        if (DialogManager.Instance != null && DialogManager.Instance.IsInDialog) return false;
        return true;
    }

    /// <summary>是否存在实体手柄（排除 OnScreenControl 自动创建的虚拟设备）。</summary>
    private static bool HasPhysicalGamepad()
    {
        foreach (var pad in Gamepad.all)
        {
            bool onScreen = false;
            foreach (var usage in pad.usages)
            {
                if (usage.ToString() == "OnScreen")
                {
                    onScreen = true;
                    break;
                }
            }
            if (!onScreen) return true;
        }
        return false;
    }
}

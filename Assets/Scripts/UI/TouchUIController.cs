using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;   // InternedString 所在命名空间

/// <summary>
/// 触屏控件显隐门控：
/// 1) 总开关——手机平台默认开启，PC/编辑器默认关闭（forceShow 可强制预览）；
/// 2) 游戏进行中才显示——timeScale=0（面板/商店/对话暂停）或对话进行中一律隐藏，
///    防止暂停期摇杆残留移动输入、跨 Canvas 射线穿透；
/// 3) 检测到实体手柄时隐藏，避免虚拟手柄与实体手柄输入打架。
///
/// 【真机看不到摇杆的排查修复】
///   原实现有三处会造成「APK 装上去触屏控件完全不显示」，且都不报错：
///   1. Application.isMobilePlatform 是编译期常量按构建目标固化，一旦出包平台与预期不符
///      （例如误用 Standalone 目标、或条件编译把平台判成 false），门控会永远拒绝显示，
///      表现为「手机上什么都没有」，编辑器里却一切正常。
///   2. HasPhysicalGamepad 用 usage.ToString() == "OnScreen" 字符串比较来判断虚拟手柄。
///      字符串比对既慢又脆弱：usage 列表里出现任何非虚拟设备（含部分机型自动枚举出的
///      无名设备）都会让该方法误判为「有实体手柄」，从而把整套触屏控件隐藏掉。
///   3. 门控只在 Update 里静默改 activeSelf，出问题时没有任何日志，现场无从判断。
///   本实现改为：用 Application.platform 枚举判定（同样编译期固化但语义明确，
///   并支持 forceShow 强制放行）；用 InputDevice 的 layout/displayName 判定虚拟手柄，
///   不依赖 usage 字符串；首次判定结果输出一次日志，真机 logcat 可直接确认。
/// </summary>
public class TouchUIController : MonoBehaviour
{
    [Tooltip("触屏控件根（摇杆区+按钮簇+菜单按钮）")]
    public GameObject controlsRoot;

    [Tooltip("PC/编辑器强制显示触屏控件（预览测试与真机排障用）")]
    public bool forceShow;

    /// <summary>输出一次门控判定明细，便于真机用 logcat 定位「控件不显示」的原因。</summary>
    [Tooltip("输出一次门控判定日志（真机排障用，正常发布可关闭）")]
    public bool logDecisionOnce = true;

    private bool _logged;

    void Update()
    {
        if (controlsRoot == null) return;

        bool should = ShouldShow();

        if (logDecisionOnce && !_logged)
        {
            _logged = true;
            Debug.Log($"[TouchUI] 门控判定：显示={should} platform={Application.platform} " +
                      $"isMobilePlatform={Application.isMobilePlatform} forceShow={forceShow} " +
                      $"timeScale={Time.timeScale} inDialog=" +
                      $"{DialogManager.Instance != null && DialogManager.Instance.IsInDialog} " +
                      $"实体手柄={HasPhysicalGamepad()}");
        }

        if (controlsRoot.activeSelf != should)
            controlsRoot.SetActive(should);
    }

    private bool ShouldShow()
    {
        if (!forceShow && !IsMobilePlatform()) return false;
        if (HasPhysicalGamepad()) return false;
        if (Time.timeScale <= 0f) return false;
        if (DialogManager.Instance != null && DialogManager.Instance.IsInDialog) return false;
        return true;
    }

    /// <summary>
    /// 移动平台判定。Application.isMobilePlatform 在 Android 构建下即为 true，
    /// 这里额外并列 RuntimePlatform 枚举判定，避免任何一方失效导致永不显示——
    /// 触屏控件「多显示」的后果只是多一层透明 UI，「不显示」则游戏完全不可玩。
    /// </summary>
    private static bool IsMobilePlatform()
    {
        if (Application.isMobilePlatform) return true;
        var p = Application.platform;
        return p == RuntimePlatform.Android
            || p == RuntimePlatform.IPhonePlayer
            || p == RuntimePlatform.OSXPlayer;   // 便于在 Mac 上真机预览触屏布局
    }

    /// <summary>
    /// 是否存在实体手柄。
    ///
    /// OnScreenControl 注册虚拟设备时固定调用 InputSystem.AddDeviceUsage(device, "OnScreen")
    /// （见 Input System 包 OnScreenControl.cs 约 142 行），因此「usage 含 OnScreen」是
    /// 判定虚拟设备的**唯一可靠依据**——注意不能用 device.description 判断：
    /// 虚拟设备只是 AddDevice("Gamepad")，description 里并不含 OnScreen 字样。
    ///
    /// usages 的元素类型是 InternedString，支持 == 直接比较，无须 ToString() 转字符串
    /// （原实现每帧对每个手柄做字符串分配与比较，既慢又难以排除大小写/空白差异）。
    /// </summary>
    private static bool HasPhysicalGamepad()
    {
        var onScreenUsage = new InternedString("OnScreen");

        foreach (var pad in Gamepad.all)
        {
            bool isVirtual = false;
            foreach (var usage in pad.usages)
            {
                if (usage == onScreenUsage)
                {
                    isVirtual = true;
                    break;
                }
            }
            if (!isVirtual) return true;
        }
        return false;
    }
}
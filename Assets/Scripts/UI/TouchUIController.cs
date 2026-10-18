using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;   // InternedString 所在命名空间
using UnityEngine.UI;

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
///   4.【真因】HasPhysicalGamepad 只看 usage 是否含 OnScreen 就判定「有实体手柄」。
///      国产 ROM 会注册 uinput 虚拟输入设备（小米 dumpsys input 实测有
///      Device 3: uinput-xiaomi），Unity 把它枚举成 Gamepad 且不带 OnScreen usage，
///      于是该方法永远返回 true，触屏 UI 在真机上被永久隐藏。
/// 本实现改为：Application.platform 枚举判定（并支持 forceShow 强制放行）；
/// 物理手柄判定需同时排除 OnScreen / layout 关键词 / 显示名关键词三类虚拟设备；
/// 首次判定输出一次日志，并可把判定明细直接画在屏幕左上角，真机无需 logcat 也能自证。
/// </summary>
public class TouchUIController : MonoBehaviour
{
    /// <summary>当前活动的实例。菜单面板等外部系统需要挂起触屏控件时从这里取，
    /// 避免每次开关都做一次全场 FindObjectOfType。</summary>
    public static TouchUIController Active
    {
        get
        {
            if (_active != null && _active.isActiveAndEnabled) return _active;
            // 静态字段可能在域重载后为 null（Awake 不重跑），此时回退一次查找
            _active = FindObjectOfType<TouchUIController>();
            return _active;
        }
    }

    private static TouchUIController _active;

    [Tooltip("触屏控件根（摇杆区+按钮簇+菜单按钮）")]
    public GameObject controlsRoot;

    [Tooltip("PC/编辑器强制显示触屏控件（预览测试与真机排障用）")]
    public bool forceShow;

    [Tooltip("切换武器按钮（剑↔弓）。没拿到弓之前灰置不可点——按了也没反应，灰置比点了没反应更清楚")]
    public GameObject changeWeaponButton;

    [Tooltip("切换武器按钮灰置时的透明度")]
    [Range(0.1f, 1f)] public float lockedButtonAlpha = 0.35f;

    [Tooltip("切换武器按钮正常时的透明度")]
    [Range(0.1f, 1f)] public float unlockedButtonAlpha = 1f;

    /// <summary>输出一次门控判定明细，便于真机用 logcat 定位「控件不显示」的原因。</summary>
    [Tooltip("输出一次门控判定日志（真机排障用，正常发布可关闭）")]
    public bool logDecisionOnce = true;

    [Tooltip("把门控判定明细画在屏幕左上角（真机排障用，不依赖 logcat，排完记得关）")]
    public bool showOnScreenDiagnostics = false;

    private bool _logged;
    private Text _diagText;
    private float _diagTimer;
    private bool _changeButtonStateApplied;
    private bool _changeWeaponHooked;

    /// <summary>
    /// 给切换武器按钮挂上 Button.onClick → PlayerChangeEquipment.Toggle()。
    ///
    /// 【为什么必须绕开 OnScreenButton 那条路——移动中失效的根因】
    ///   ChangeBtn 原本挂 OnScreenButton 绑定 `<Gamepad>/rightShoulder`，
    ///   GameInput.changeEquipment 也绑同一个键位。但摇杆的
    ///   `<Gamepad>/leftStick`、攻击的 `<Gamepad>/buttonSouth`
    ///   **共用同一个虚拟手柄设备**。Unity 虚拟手柄是共享状态：
    ///   摇杆每帧写 leftStick → 设备每帧都有状态变化 →
    ///   `WasPressedThisFrame()` 的边沿窗口被持续冲刷，
    ///   于是**移动过程中点切换按钮没反应，静止时却正常**。
    ///
    ///   改走 Unity UI 的 onClick 完全不经过虚拟手柄，两条通路互不干扰。
    ///   PC 键鼠仍由 PlayerChangeEquipment.Update 读 GameInput，不受影响。
    ///
    ///   用 bool 去重是因为 Unity 的 AddListener 无法移除，
    ///   不去重会每帧叠加、点一下切好几次。
    /// </summary>
    private void EnsureChangeWeaponClickHook()
    {
        if (changeWeaponButton == null || _changeWeaponHooked) return;
        _changeWeaponHooked = true;

        var btn = changeWeaponButton.GetComponent<Button>();
        if (btn == null)
        {
            btn = changeWeaponButton.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            var img = changeWeaponButton.GetComponent<Image>();
            if (img != null) btn.targetGraphic = img;
        }

        var equip = FindObjectOfType<PlayerChangeEquipment>();
        if (equip == null)
        {
            Debug.LogWarning("[TouchUI] 未找到 PlayerChangeEquipment，切换武器按钮的触屏通路未挂上");
            return;
        }
        btn.onClick.AddListener(() => equip.Toggle());
        Debug.Log("[TouchUI] 切换武器按钮已改走 onClick 直连（不再经由虚拟手柄）");
    }

    /// <summary>被菜单等全屏面板挂起时为 true。见Suspend 的注释。</summary>
    private bool _suspended;
    private bool _suspendApplied;

    void Awake()
    {
        _active = this;
    }

    void OnDestroy()
    {
        if (_active == this) _active = null;
    }

    /// <summary>
    /// 挂起/恢复触屏控件的射线响应（不隐藏控件本身）。
    ///
    /// 【为什么需要】
    ///   菜单中枢面板 MenuHubPanel 挂在同一个 TouchCanvas 下、位于 SafeAreaRoot 的
    ///   后一个兄弟节点（绘制在上层），矩形铺满全屏。它展开后 blocksRaycasts=true，
    ///   会把摇杆、攻击键、切换键、菜单键全部盖住。
    ///   而关闭菜单的唯一入口就是右上角那个菜单键——正被它自己盖着，于是死锁：
    ///   **菜单一开就再也关不掉**。
    ///
    /// 【为什么只关射线而不是 SetActive(false)】
    ///   隐藏后玩家看不到摇杆和按键，会以为游戏卡死或触控失灵。
    ///   保留可见但不可点，玩家能明确感知「现在要点菜单里的东西」。
    ///
    /// 【为什么用 CanvasGroup.blocksRaycasts 而不是逐个 Image.raycastTarget】
    ///   一个 CanvasGroup 开关整棵子树，O(1) 操作，且能在关闭时精确还原
    ///   （逐个改 raycastTarget 需要自己缓存原值，忘了还原就是新的坑）。
    /// </summary>
    public void Suspend(bool suspended)
    {
        if (_suspended == suspended && _suspendApplied) return;
        _suspended = suspended;
        ApplySuspend();
    }

    private void ApplySuspend()
    {
        if (controlsRoot == null) return;
        _suspendApplied = true;

        var group = controlsRoot.GetComponent<CanvasGroup>();
        if (group == null) group = controlsRoot.AddComponent<CanvasGroup>();

        // blocksRaycasts 拦射线（谁被点到），interactable 拦控件自身响应，
        // 两者都要关，否则 Button 仍会响应 OnScreenButton 写入的虚拟按键。
        group.blocksRaycasts = !_suspended;
        group.interactable = !_suspended;
    }

    void Update()
    {
        if (controlsRoot == null) return;

        bool should = ShouldShow();

        // 切换武器按钮的可用性跟随弓箭解锁状态，与总显隐门控相互独立
        RefreshChangeWeaponButton();

        // 切换武器的点击通路：见EnsureChangeWeaponClickHook 的注释
        EnsureChangeWeaponClickHook();

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
        {
            controlsRoot.SetActive(should);
            // 控件被整体隐藏再显示时，挂起标记需要重新落到刚启用的对象上
            if (should) ApplySuspend();
        }

        // 屏幕诊断：每 0.25 秒刷新一次，避免每帧重建字符串。
        if (showOnScreenDiagnostics)
        {
            _diagTimer -= Time.unscaledDeltaTime;
            if (_diagTimer <= 0f)
            {
                _diagTimer = 0.25f;
                UpdateDiagnostics(should);
            }
        }
        else if (_diagText != null)
        {
            _diagText.text = string.Empty;
        }
    }

    /// <summary>
    /// 把「为什么没显示」直接画到屏幕上。真机抓 logcat 需要 USB 连线，
    /// 而这一步在手机上就能自证：哪一行是 ✗ 就说明哪一项门控把它挡掉了。
    /// </summary>
    private void UpdateDiagnostics(bool should)
    {
        if (_diagText == null)
        {
            _diagText = BuildDiagnosticsLabel();
            if (_diagText == null) return;
        }

        var canvas = GetComponentInParent<Canvas>();
        bool inDialog = DialogManager.Instance != null && DialogManager.Instance.IsInDialog;
        bool mobile = IsMobilePlatform();
        bool pad = HasPhysicalGamepad();

        var sb = new System.Text.StringBuilder(320);
        sb.Append("[TouchUI] ").Append(should ? "<已显示>" : "<被隐藏>").Append('\n');
        sb.Append(Mark(!forceShow && !mobile))
          .Append(" 平台=").Append(Application.platform)
          .Append(" mobile=").Append(mobile).Append('\n');
        sb.Append(Mark(forceShow || mobile)).Append(" 平台门控")
          .Append(forceShow ? " (forceShow)" : string.Empty).Append('\n');
        sb.Append(Mark(!pad)).Append(" 实体手柄=").Append(pad).Append('\n');
        sb.Append(Mark(Time.timeScale > 0f)).Append(" timeScale=")
          .Append(Time.timeScale.ToString("0.00")).Append('\n');
        sb.Append(Mark(!inDialog)).Append(" 对话中=").Append(inDialog).Append('\n');
        sb.Append("root=").Append(controlsRoot.activeSelf ? "Active" : "Inactive")
          .Append(" parent=").Append((controlsRoot.transform.parent != null
              && controlsRoot.transform.parent.gameObject.activeSelf) ? "Active" : "Inactive")
          .Append('\n');
        var rt = controlsRoot.GetComponent<RectTransform>();
        if (rt != null)
            sb.Append("rect=").Append(rt.rect.size.x.ToString("0"))
              .Append('x').Append(rt.rect.size.y.ToString("0"))
              .Append(" scale=").Append(rt.localScale.ToString("0.0"))
              .Append('\n');
        sb.Append("cam=").Append(Camera.main != null)
          .Append(" Screen=").Append(Screen.width).Append('x').Append(Screen.height);

        _diagText.text = sb.ToString();
        _diagText.color = should ? new Color(0.4f, 1f, 0.4f, 0.95f) : new Color(1f, 0.5f, 0.3f, 0.95f);
    }

    private static string Mark(bool pass) => pass ? "[OK] " : "[XX] ";

    /// <summary>
    /// 切换武器按钮（剑↔弓）的解锁状态刷新。
    ///
    /// 【为什么要做】
    /// 射箭按钮已按手游惯例删除，攻击键按当前装备自动分派。此时「切换武器」成了
    /// 玩家进入弓箭状态的唯一入口——没弓时它是个死按钮。
    /// 原先既不隐藏也不置灰，玩家点了毫无反应却看不出原因，因此这里按 hasBow
    /// 实时置灰+ 调暗，让「还没解锁」一眼可见。
    ///
    /// 只在状态真正变化时写属性，避免每帧刷 CanvasRenderer 造成不必要的重建。
    /// </summary>
    private void RefreshChangeWeaponButton()
    {
        if (changeWeaponButton == null) return;

        var stats = StatsManager.GetActive();
        bool unlocked = stats != null && stats.hasBow;
        bool shouldInteract = unlocked;

        if (_changeButtonStateApplied == shouldInteract) return;
        _changeButtonStateApplied = shouldInteract;

        // OnScreenButton 挂在同一个对象上时，关掉它才能真正屏蔽写入虚拟手柄的按键
        var onScreen = changeWeaponButton.GetComponent<UnityEngine.InputSystem.OnScreen.OnScreenButton>();
        if (onScreen != null) onScreen.enabled = shouldInteract;

        // 置灰：Image 调暗 + Button 不可交互
        var image = changeWeaponButton.GetComponent<Image>();
        if (image != null)
        {
            var c = image.color;
            c.a = unlocked ? unlockedButtonAlpha : lockedButtonAlpha;
            image.color = c;
        }

        var button = changeWeaponButton.GetComponent<Button>();
        if (button != null) button.interactable = shouldInteract;

        // 文字标签同步压暗，否则「暗底亮字」看着仍像可用
        var labels = changeWeaponButton.GetComponentsInChildren<UnityEngine.UI.Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            var lc = labels[i].color;
            lc.a = unlocked ? 1f : lockedButtonAlpha;
            labels[i].color = lc;
        }
    }

    /// <summary>在现有触屏 Canvas 上挂一个置顶的诊断文本，不新建 Canvas 以免影响门控本身。</summary>
    private Text BuildDiagnosticsLabel()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return null;

        var go = new GameObject("[TouchUI Diagnostics]",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.layer = LayerMask.NameToLayer("UI");

        var rt = (RectTransform)go.transform;
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(12f, -12f);
        rt.sizeDelta = new Vector2(760f, 300f);

        var text = go.GetComponent<Text>();
        // font 为 null 时 Text.font setter 会抛 ArgumentNullException，直接放弃诊断文字。
        // 诊断是辅助功能，绝不能让它把整个 Update 打挂（那反而会导致每帧报错、控件彻底不可用）。
        var font = LoadBuiltinFont();
        if (font == null)
        {
            UnityEngine.Object.Destroy(go);
            return null;
        }
        text.font = font;
        text.fontSize = 22;
        text.alignment = TextAnchor.UpperLeft;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;   // 关键：绝不能挡住下面的触屏控件
        text.color = Color.white;
        return text;
    }

    /// <summary>
    /// 取内置字体。
    /// 2021.3 的内置字体资源名是 LegacyRuntime.ttf；但它在部分 Unity 版本/中国版
    /// 发行版里已被移除（真机实测：GetBuiltinResource("LegacyRuntime.ttf") 返回
    /// "The resource LegacyRuntime.ttf could not be loaded"），因此这里多级回退，
    /// 全部失败时返回 null 由调用方跳过诊断文字——诊断只是辅助，绝不能因此抛异常。
    /// </summary>
    private static Font LoadBuiltinFont()
    {
        string[] candidates = { "LegacyRuntime.ttf", "Arial.ttf" };
        foreach (var name in candidates)
        {
            try
            {
                var f = Resources.GetBuiltinResource<Font>(name);
                if (f != null) return f;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[TouchUI] 内置字体 {name} 加载失败：{e.Message}");
            }
        }
        Debug.LogWarning("[TouchUI] 无可用内置字体，屏幕诊断文字将不显示（不影响游戏）");
        return null;
    }

    private bool ShouldShow()
    {
        // 注意：不要在这里 return false 得太果断。触屏控件「多显示」的代价远小于
        // 「不显示」——后者会让整局游戏不可玩。所以只有确定不适用时才隐藏。
        if (!forceShow && !IsMobilePlatform()) return false;
        if (Time.timeScale <= 0f) return false;
        if (DialogManager.Instance != null && DialogManager.Instance.IsInDialog) return false;

        // 实体手柄判定放在最后，且仅在确实枚举到「真·物理手柄」时隐藏。
        if (HasPhysicalGamepad()) return false;

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
    /// 是否存在**真·物理手柄**。
    ///
    /// 【真机实测踩坑，重要】
    /// 国产 ROM（小米/红米/Huawei/OPPO）会在系统层注册一堆 **uinput 虚拟输入设备**，
    /// 用于体感截屏、按键录制、长按识别等功能。实测小米机型 `dumpsys input` 里
    /// 就有 `Device 3: uinput-xiaomi`，Unity Input System 把它枚举成一个 Gamepad。
    /// 旧判定只看「usage 不含 OnScreen → 就是实体手柄」，因此**永远为 true**，
    /// 整套触屏 UI 在真机上被永久隐藏——这就是「APK 装上去没有摇杆和攻击键」的真实根因。
    ///
    /// 现在需同时排除三类虚拟设备：OnScreen usage、layout 关键词、显示名关键词。
    /// 取向明确：**宁可漏判（多显示）也不误判（把 UI 全藏了）**。
    /// </summary>
    private static bool HasPhysicalGamepad()
    {
        var onScreenUsage = new InternedString("OnScreen");

        foreach (var pad in Gamepad.all)
        {
            // 规则 1：Unity 自己的屏幕虚拟设备
            bool isOnScreen = false;
            foreach (var usage in pad.usages)
            {
                if (usage == onScreenUsage) { isOnScreen = true; break; }
            }
            if (isOnScreen) continue;

            // 规则 2：layout 名。国产 ROM 的 uinput 虚拟设备 layout 常含这些词。
            string layout = pad.layout;
            if (!string.IsNullOrEmpty(layout))
            {
                string lower = layout.ToLowerInvariant();
                if (lower.Contains("virtual") || lower.Contains("uinput")
                    || lower.Contains("emulated") || lower.Contains("dummy"))
                    continue;
            }

            // 规则 3：设备显示名关键词
            string name = ((pad.displayName ?? string.Empty) + " "
                         + (pad.name ?? string.Empty)).ToLowerInvariant();
            if (name.Contains("virtual") || name.Contains("uinput")
                || name.Contains("emulated") || name.Contains("dummy")
                || name.Contains("xiaomi") || name.Contains("virtualdevice"))
                continue;

            // 走到这里才认定为真手柄，打日志便于日后核对判定是否符合预期
            Debug.Log($"[TouchUI] 检测到物理手柄：name={pad.name} displayName={pad.displayName} " +
                      $"layout={pad.layout}，隐藏触屏控件");
            return true;
        }
        return false;
    }
}
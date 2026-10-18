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

    [Tooltip("攻击键（触屏）。onClick 由运行时接线，见 HookTouchButtons")]
    public GameObject attackButton;

    [Tooltip("交互键（触屏，与 NPC 对话）。留空则不接线，交互只能靠键盘 F")]
    public GameObject interactButton;

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

    /// <summary>
    /// 把所有触屏按键从「虚拟手柄」改接到「Unity UI onClick」。
    ///
    /// 【为什么必须全改，只改一个不够——上一轮的真实教训】
    ///   上一轮只把 ChangeBtn 改成 onClick，用户复测仍然报「推摇杆时其他按钮点不动」。
    ///   原因是攻击键（AttackBtn，OnScreenButton→`&lt;Gamepad&gt;/buttonSouth`）和
    ///   交互键（InteractBtn，OnScreenButton→`&lt;Gamepad&gt;/buttonNorth`）
    ///   **仍在写虚拟手柄**。这些 OnScreen 控件与摇杆共用同一个虚拟 Gamepad 设备，
    ///   摇杆移动时每帧写 leftStick 让该设备持续产生状态变化，
    ///   `WasPressedThisFrame()` 的边沿窗口被冲刷 → 按键无反应。
    ///   只要还有一个按钮留在虚拟手柄上，这个 bug 就会以「那个按钮」的形式继续存在。
    ///
    ///   现在：摇杆（FloatingOnScreenStick）也不再写虚拟手柄，改写 TouchInputBridge.Move。
    ///   移动是「持续值」，不是「边沿」，原理上无法冲刷任何按键边沿——
    ///   这是根治，而非逐个打补丁。
    ///
    ///【为什么用 AddListener 而不用持久监听器】
    ///   本方法在运行时挂载，而 Unity 的 AddListener 加的是**运行时监听器**，
    ///   不会写进 prefab。但本项目里这层接线本来就依赖运行时状态
    ///   （例如 ChangeBtn 要按 hasBow 决定是否可用），运行时接线反而更合适：
    ///   prefab 保持干净，且改代码后无需重新配置 prefab。
    ///   对比：MenuHubPanel 的「关闭Entry」用的是 prefab 持久监听器，
    ///   那个必须持久化——因为它指向 MenuHubUI 自己的方法，语义上是 UI 的一部分。
    ///
    /// 【为什么要禁用 OnScreenButton 而不是留着】
    ///   留着会出现「双通路重复响应」：一次点击既走 onClick 又写虚拟手柄，
    ///   在 PC 上（键盘 K 同时可用）会导致一次按键触发两次攻击。
    /// </summary>
    /// <summary>三个按键是否已接过 onClick，防止每帧重复 AddListener。</summary>
    private bool _attackHooked, _changeWeaponHooked, _interactHooked;

    /// <summary>临时排障覆盖层（真机读状态用，排完删 TouchOverlayDiag.cs 与本字段）。</summary>
    private static TouchOverlayDiag _overlay;

    private void HookTouchButtons()
    {
        if (!_attackHooked) HookButton(attackButton, () =>
        {
            TouchInputBridge.PressAttack();
            if (_overlay != null) _overlay.CountAttackClick();
        });
        if (!_changeWeaponHooked) HookButton(changeWeaponButton, () =>
        {
            var equip = FindObjectOfType<PlayerChangeEquipment>();
            if (equip != null) equip.Toggle();
        });
        if (!_interactHooked) HookButton(interactButton, () => TouchInputBridge.PressInteract());

        _attackHooked = attackButton != null;
        _changeWeaponHooked = changeWeaponButton != null;
        _interactHooked = interactButton != null;
    }

    /// <summary>给一个触屏按键挂 onClick，并禁掉它身上的 OnScreenButton（若有）。</summary>
    private static void HookButton(GameObject go, UnityEngine.Events.UnityAction action)
    {
        if (go == null) return;

        var btn = go.GetComponent<Button>();
        if (btn == null)
        {
            btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            var img = go.GetComponent<Image>();
            if (img != null) btn.targetGraphic = img;
        }
        btn.onClick.AddListener(action);

        // 屏蔽虚拟手柄通路，见 HookTouchButtons 的「为什么要禁用」一段
        var onScreen = go.GetComponent<UnityEngine.InputSystem.OnScreen.OnScreenButton>();
        if (onScreen != null) onScreen.enabled = false;
    }

    /// <summary>
    ///帧末清除触屏的一次性按键标记。
    ///
    /// 【为什么必须放在 LateUpdate 而不是 Update】
    ///   Unity 不保证脚本之间的 Update 顺序。若在 Update 里清除，而 TouchUIController
    ///   的 Update 恰好早于玩家状态机的 Update 跑，就会把本帧的点击标记擦掉——
    ///   症状是「按钮要偶发失灵」，且极难复现。
    ///   LateUpdate 一定晚于本帧所有 Update，因此只要点击在本帧发生，
    ///   玩家状态机（Update 中读取）就一定读得到。
    /// </summary>
    void LateUpdate()
    {
        TouchInputBridge.EndFrameClear();
    }

    /// <summary>被菜单等全屏面板挂起时为 true。见Suspend 的注释。</summary>
    private bool _suspended;
    private bool _suspendApplied;

    void Awake()
    {
        _active = this;

        // 【临时排障】创建屏幕状态覆盖层：真机截图即可读到同时手指数峰值、
        // 攻击按钮点击次数、武器解锁与战斗组件启用状态。排完删掉这段。
        if (_overlay == null)
        {
            var go = new GameObject("[TouchOverlayDiag]");
            _overlay = go.AddComponent<TouchOverlayDiag>();
            DontDestroyOnLoad(go);
        }
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

        // 触屏按键统一改走 onClick，见 HookTouchButtons 的注释
        HookTouchButtons();

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

        // 置灰：Image 调暗 + Button 不可交互
        // 注意这里不要再动 OnScreenButton —— 触屏按键已统一改走 onClick，
        // 虚拟手柄通路由 HookButton 一次性禁用，见 HookTouchButtons 的注释。
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
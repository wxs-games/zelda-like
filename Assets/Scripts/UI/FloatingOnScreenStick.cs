using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// 手机端虚拟摇杆（王者荣耀式常驻固定摇杆）。
///
/// 【架构变更：不再写虚拟手柄，输出改走 TouchInputBridge】
///   旧实现继承 OnScreenControl，把值写进 `<Gamepad>/leftStick`。但攻击键
///   （`<Gamepad>/buttonSouth`）、交互键（`<Gamepad>/buttonNorth`）也写在
///   **同一个虚拟 Gamepad 设备**上，而 GameInput 读的是 `WasPressedThisFrame()`
///   这种边沿量。摇杆移动时每帧写 leftStick → 该虚拟设备每帧都有状态变化 →
///   边沿窗口被持续冲刷 → **推着摇杆时点其他按钮全部无反应，静止时却正常**。
///   （上一轮只把 ChangeBtn 单独改成 onClick，攻击/交互仍走虚拟手柄，所以问题没消失。）
///
///   现方案：摇杆不再与 Input System 的虚拟设备通信，值写入
///   TouchInputBridge.Move，由 GameInput.Move 与键盘输入合并。
///   **移动操作从「写进共享设备的边沿源」变成「独立持续值」，
///   原理上就不可能再冲刷任何按键的边沿。**
///
/// 【历史坑，保留说明：为什么不用 OnScreenStick 的指针方法】
///   之前本类 `class FloatingOnScreenStick : OnScreenStick` 并用
///   `new public void OnPointerDown(...)` 隐藏基类方法，实测真机「摇杆滚不动」。
///   原因是 Unity 的 EventSystem 通过
///   `ExecuteEvents.GetEventHandler<IPointerDownHandler>()` **按接口**在组件列表上查找并调用。
///   基类 OnScreenStick 自身已实现 IPointerDownHandler/IDragHandler/IPointerUpHandler，
///   派生类用 `new` 隐藏**不会改变接口映射表**。
///   现方案：指针方法由本类自己实现接口，行为完全可控。
/// </summary>
public class FloatingOnScreenStick : MonoBehaviour,
    IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Tooltip("摇杆视觉根（Base+Knob 容器）。常驻模式下启动即显示，不随松手隐藏")]
    public RectTransform visualRoot;

    [Tooltip("摇杆圆钮，跟随手指偏移，松手回中")]
    public RectTransform knob;

    [Tooltip("底座 Image（常驻显示时用它表达可交互区域）")]
    public Image baseImage;

    [Tooltip("半透明底座颜色（alpha 决定闲置时的存在感）")]
    public Color idleColor = new Color(1f, 1f, 1f, 0.28f);

    [Tooltip("按下时底座颜色（反馈已抓住）")]
    public Color pressedColor = new Color(1f, 1f, 1f, 0.55f);

    [Tooltip("最大拖动距离（Canvas 本地单位/像素）")]
    public float maxKnobTravel = 110f;

    [Tooltip("true=浮动摇杆（按下处生成底座）；false=常驻固定摇杆（王者荣耀式）")]
    public bool floatingMode = false;

    [Tooltip("输出指针事件与写入值的日志（真机排障用，排完记得关）")]
    public bool verboseLog = true;

    private int _pointerId = -1;
    private Vector2 _pressScreenPos;
    private bool _pressed;

    /// <summary>
    /// 当前控制摇杆的指针是否来自触摸屏。
    /// 只有触摸指针才启用「手指是否还在屏上」的看门狗——鼠标指针没有对应的
    /// Touchscreen 槽位，若对它也做校验，编辑器里用鼠标拖摇杆会被误判成已松手。
    /// </summary>
    private bool _isTouchPointer;

    private float Travel => maxKnobTravel > 0f ? maxKnobTravel : 110f;

    void OnEnable()
    {
        // 常驻模式下把底座立刻显示出来，玩家一眼知道「这里能拖」。
        if (!floatingMode && visualRoot != null)
            visualRoot.gameObject.SetActive(true);

        ApplyIdleLook();
        Report($"OnEnable travel={Travel}");
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData == null) return;
        if (_pointerId != -1) return;   // 已有手指在控，忽略多指

        _pointerId = eventData.pointerId;
        _pressScreenPos = eventData.position;
        _pressed = true;
        // ExtendedPointerEventData 才带 pointerType；普通 PointerEventData 视为非触摸
        _isTouchPointer = eventData is ExtendedPointerEventData ext
                          && ext.pointerType == UIPointerType.Touch;

        if (visualRoot != null)
        {
            visualRoot.gameObject.SetActive(true);

            // 只有浮动模式才把底座挪到按下处；常驻模式底座固定不动
            if (floatingMode && transform.parent is RectTransform canvasRect)
            {
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        canvasRect, eventData.position, eventData.pressEventCamera, out var local))
                {
                    visualRoot.localPosition = local;
                }
            }
        }

        if (knob != null) knob.anchoredPosition = Vector2.zero;
        ApplyPressedLook(true);
        TouchInputBridge.SetMove(Vector2.zero);
        Report($"PointerDown id={_pointerId} screen={_pressScreenPos}");
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData == null) return;
        if (eventData.pointerId != _pointerId)
        {
            if (verboseLog) Debug.Log($"[Joystick] 忽略他指针 Drag id={eventData.pointerId}，当前 {_pointerId}");
            return;
        }

        var canvas = GetComponentInParent<Canvas>();
        float scale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        float travel = Travel;

        // 屏幕位移换算到 Canvas 本地单位再夹取
        Vector2 delta = Vector2.ClampMagnitude(
            (eventData.position - _pressScreenPos) / scale, travel);

        if (knob != null) knob.anchoredPosition = delta;

        // 死区：手指微抖不该让角色抖
        Vector2 normalized = delta / travel;
        if (normalized.sqrMagnitude < 0.02f) normalized = Vector2.zero;

        TouchInputBridge.SetMove(normalized);
        Report($"Drag delta={delta} value={normalized}");
    }

    /// <summary>
    /// 看门狗：每帧校验「控制摇杆的那根手指是否还按在屏幕上」。
    ///
    /// 【为什么需要】
    ///   复位原本只依赖 OnPointerUp。但真机实测出现过「松手后摇杆不复位、
    ///   角色一直朝最后方向走」——Up 事件要么没送达、要么 pointerId 与按下时
    ///   不一致（按下记的是 A，抬起回调给的是 B），于是 OnPointerUp 的
    ///   `eventData.pointerId != _pointerId` 早退，Move 永远停在最后的值。
    ///   输入类的 bug 不能只靠「事件一定会到」来保证正确性，必须有状态兜底。
    ///
    /// 判定方式：InputSystemUIInputModule 的触摸 pointerId 编码为
    ///   (deviceId &lt;&lt; 24) + touchId
    /// 所以逐槽位重建该 id 并比对，再读 press 状态即可精确知道那根手指还在不在。
    /// 不要用 `pointerId &amp; 0xFF` 反解 touchId——touchId 超过 255 会被截断。
    /// </summary>
    void Update()
    {
        if (!_pressed || !_isTouchPointer) { _missingFrames = 0; return; }

        if (IsControllingTouchStillDown())
        {
            _missingFrames = 0;
            return;
        }

        // 连续两帧都判定「手指已不在」才复位：单帧读到异常值（个别机型 touchId
        // 在某帧读作 0）不该让正在拖动的摇杆突然归零。
        _missingFrames++;
        if (_missingFrames < 2) return;

        Report("看门狗：控制手指已不在屏上，强制复位");
        _missingFrames = 0;
        ResetStick();
    }

    private int _missingFrames;

    private bool IsControllingTouchStillDown()
    {
        // 必须遍历**所有** Touchscreen 设备，不能只看 Touchscreen.current。
        // 真机实测同时存在多个触摸设备（真实触摸屏 + adb 注入产生的虚拟设备），
        // current 指向哪一个取决于「最后使用的是哪个设备」。只看 current 会在
        // 多设备场景下与 _pointerId 的 deviceId 段对不上，从而把正在按住的
        // 手指误判成已松开，摇杆被强行归零。
        var devices = InputSystem.devices;
        for (int d = 0; d < devices.Count; d++)
        {
            if (!(devices[d] is Touchscreen ts)) continue;

            var touches = ts.touches;
            for (int i = 0; i < touches.Count; i++)
            {
                int touchId = touches[i].touchId.ReadValue();
                int encoded = (ts.deviceId << 24) + touchId;
                if (encoded == _pointerId)
                    return touches[i].press.isPressed;
            }
        }
        return false;   // 所有设备上都找不到该触摸 → 已结束
    }

    /// <summary>把摇杆归零并释放控制权。Up / Disable / 看门狗三条路径共用。</summary>
    private void ResetStick()
    {
        _pointerId = -1;
        _pressed = false;
        _isTouchPointer = false;
        _missingFrames = 0;

        if (knob != null) knob.anchoredPosition = Vector2.zero;
        TouchInputBridge.SetMove(Vector2.zero);
        ApplyIdleLook();

        if (floatingMode && visualRoot != null)
            visualRoot.gameObject.SetActive(false);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData == null) return;
        if (eventData.pointerId != _pointerId) return;

        Report($"PointerUp id={_pointerId}");
        ResetStick();
    }

    void OnDisable()
    {
        // 控件被隐藏（暂停/对话/挂起）时必须清零，否则玩家松手后角色会一直朝最后方向走
        ResetStick();
    }

    private void ApplyIdleLook()
    {
        if (baseImage != null && !_pressed) baseImage.color = idleColor;
    }

    private void ApplyPressedLook(bool pressed)
    {
        if (baseImage != null) baseImage.color = pressed ? pressedColor : idleColor;
    }

    private void Report(string msg)
    {
        if (!verboseLog) return;
        Debug.Log($"[Joystick] {msg}");
    }
}

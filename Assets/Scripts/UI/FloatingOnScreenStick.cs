using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;   // InputControlAttribute 在此；不加会解析到抽象类 InputControl
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.UI;

/// <summary>
/// 手机端虚拟摇杆（王者荣耀式常驻固定摇杆）。
///
/// 【为什么不再继承 OnScreenStick 接管它的指针方法】
/// 之前本类 `class FloatingOnScreenStick : OnScreenStick` 并用
/// `new public void OnPointerDown(...)` 隐藏基类方法，实测真机「摇杆滚不动」。
/// 原因是 Unity 的 EventSystem 通过
/// `ExecuteEvents.GetEventHandler&lt;IPointerDownHandler&gt;()` **按接口**在组件列表上查找并调用。
/// 基类 OnScreenStick 自身已实现 IPointerDownHandler/IDragHandler/IPointerUpHandler，
/// 派生类用 `new` 隐藏**不会改变接口映射表**——EventSystem 调用的仍是
/// **基类 OnScreenStick 的实现**，它按 `transform.parent` 算坐标、并直接改
/// **transform 自身（JoystickZone）** 的 anchoredPosition。
/// 于是常驻布局下整个触摸区被拖走，而我们维护的 visualRoot/knob 完全不动，看起来就是滚不动。
///
/// 现方案：摇杆逻辑独立实现（仍继承 OnScreenControl，仅为复用 SendValueToControl
/// 与虚拟设备注册），指针方法**不用 new 隐藏**，而是本类自己实现接口，
/// 接口映射指向本类，行为完全可控。
///
/// 值写入 controlPath（&lt;Gamepad&gt;/leftStick），OnScreenControl 自动创建虚拟手柄设备，
/// GameInput.Move（InputAction "Move"）直接可读。
/// </summary>
public class FloatingOnScreenStick : OnScreenControl,
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

    // 注意：movementRange 是 OnScreenStick 的属性，本类不再继承 OnScreenStick，
    // 因此没有该成员，统一用自身的 maxKnobTravel。
    private float Travel => maxKnobTravel > 0f ? maxKnobTravel : 110f;

    // 说明：OnScreenStick 用的是同样的写法——[InputControl(layout = "Vector2")]
    // 标记 controlPath 字段，让 Input System 知道这个组件写的是 Vector2 类型的控件。
    // 注意必须 using UnityEngine.InputSystem.Layouts，否则 [InputControl] 会解析到
    // 同名的抽象类 InputControl 上，编译报 CS0653「不能应用抽象类特性」。
    [InputControl(layout = "Vector2")]
    [SerializeField]
    private string m_ControlPath = "<Gamepad>/leftStick";

    /// <summary>
    /// OnScreenControl 的抽象成员。
    ///
    /// 【踩坑，务必保留自有字段】基类 public 属性 controlPath 的 getter/setter
    /// 内部就是读写 controlPathInternal：
    ///     public string controlPath { get => controlPathInternal; set { controlPathInternal = value; ... } }
    /// 如果这里写成 `get => controlPath; set => controlPath = value;`
    /// 就是**无限递归** → 栈溢出 → 游戏在初始化阶段直接黑屏（实测真机整屏黑、logcat 无任何 Unity 日志）。
    /// 因此必须像 OnScreenStick 自己那样持有一份独立的序列化字段，绕开这层间接。
    /// </summary>
    protected override string controlPathInternal
    {
        get => m_ControlPath;
        set => m_ControlPath = value;
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        // 本方案不让基类移动 transform（我们只借 SendValueToControl），
        // 常驻模式下把底座立刻显示出来，玩家一眼知道「这里能拖」。
        if (!floatingMode && visualRoot != null)
            visualRoot.gameObject.SetActive(true);

        ApplyIdleLook();
        Report($"OnEnable controlPath={controlPath} travel={Travel}");
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData == null) return;
        if (_pointerId != -1) return;   // 已有手指在控，忽略多指

        _pointerId = eventData.pointerId;
        _pressScreenPos = eventData.position;
        _pressed = true;

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
        SendValueToControl(Vector2.zero);
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

        SendValueToControl(normalized);
        Report($"Drag delta={delta} value={normalized}");
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData == null) return;
        if (eventData.pointerId != _pointerId) return;

        Report($"PointerUp id={_pointerId}");
        _pointerId = -1;
        _pressed = false;

        if (knob != null) knob.anchoredPosition = Vector2.zero;
        SendValueToControl(Vector2.zero);
        ApplyIdleLook();

        if (floatingMode && visualRoot != null)
            visualRoot.gameObject.SetActive(false);
    }

    protected override void OnDisable()
    {
        _pointerId = -1;
        _pressed = false;
        if (knob != null) knob.anchoredPosition = Vector2.zero;
        SendValueToControl(Vector2.zero);
        if (floatingMode && visualRoot != null)
            visualRoot.gameObject.SetActive(false);
        base.OnDisable();
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
        var ctl = control;   // m_Control 是基类 private 字段，只能走 public 的 control 属性
        Debug.Log($"[Joystick] {msg} control={(ctl == null ? "NULL" : ctl.path)}");
    }
}

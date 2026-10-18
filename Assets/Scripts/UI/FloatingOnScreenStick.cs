using UnityEngine;
using UnityEngine.EventSystems;
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

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData == null) return;
        if (eventData.pointerId != _pointerId) return;

        Report($"PointerUp id={_pointerId}");
        _pointerId = -1;
        _pressed = false;

        if (knob != null) knob.anchoredPosition = Vector2.zero;
        TouchInputBridge.SetMove(Vector2.zero);
        ApplyIdleLook();

        if (floatingMode && visualRoot != null)
            visualRoot.gameObject.SetActive(false);
    }

    void OnDisable()
    {
        _pointerId = -1;
        _pressed = false;
        if (knob != null) knob.anchoredPosition = Vector2.zero;
        // 控件被隐藏（暂停/对话/挂起）时必须清零，否则玩家松手后角色会一直朝最后方向走
        TouchInputBridge.SetMove(Vector2.zero);
        if (floatingMode && visualRoot != null)
            visualRoot.gameObject.SetActive(false);
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

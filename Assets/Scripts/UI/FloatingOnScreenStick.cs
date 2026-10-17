using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.OnScreen;

/// <summary>
/// 浮动摇杆：手指在摇杆区任意位置按下时，摇杆在按下处显形并跟随手指滑动，
/// 松手隐藏并归零。值写入 controlPath（如 <Gamepad>/leftStick），
/// 由 OnScreenControl 自动创建虚拟手柄设备，GameInput.Move 直接可读。
/// 注意：OnScreenStick 的指针方法不是 virtual，这里以接口重实现接管事件，
/// 仅复用基类的 controlPath、movementRange 与 SendValueToControl。
/// </summary>
public class FloatingOnScreenStick : OnScreenStick,
    IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Tooltip("摇杆视觉根（Base+Knob 容器），按下时显形、松手隐藏")]
    public RectTransform visualRoot;

    [Tooltip("摇杆圆钮，跟随手指偏移")]
    public RectTransform knob;

    private int _pointerId = -1;
    private Vector2 _pressScreenPos;

    new public void OnPointerDown(PointerEventData eventData)
    {
        // 已有一根手指控制时忽略多指，防止抢位
        if (_pointerId != -1) return;
        _pointerId = eventData.pointerId;
        _pressScreenPos = eventData.position;

        if (visualRoot != null)
        {
            visualRoot.gameObject.SetActive(true);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)transform, eventData.position, eventData.pressEventCamera, out var local))
            {
                visualRoot.localPosition = local;
            }
        }
        if (knob != null) knob.anchoredPosition = Vector2.zero;
        SendValueToControl(Vector2.zero);
    }

    new public void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerId != _pointerId) return;

        // movementRange 是 Canvas 本地单位，屏幕位移要除以缩放
        var canvas = GetComponentInParent<Canvas>();
        float scale = canvas != null ? canvas.scaleFactor : 1f;
        Vector2 delta = Vector2.ClampMagnitude(
            (eventData.position - _pressScreenPos) / scale, movementRange);

        if (knob != null) knob.anchoredPosition = delta;
        SendValueToControl(delta / movementRange);
    }

    new public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != _pointerId) return;
        ResetStick();
        SendValueToControl(Vector2.zero);
    }

    protected override void OnDisable()
    {
        ResetStick();
        base.OnDisable();
    }

    private void ResetStick()
    {
        _pointerId = -1;
        if (knob != null) knob.anchoredPosition = Vector2.zero;
        if (visualRoot != null) visualRoot.gameObject.SetActive(false);
    }
}

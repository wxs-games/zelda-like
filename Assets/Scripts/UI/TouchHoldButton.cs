using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 触屏「按住型」按钮：手指按下即生效，抬起或移出即失效。
///
/// 【为什么不用 OnScreenButton，也不用 onClick】
///   1) OnScreenButton 写的是共享虚拟手柄控件，摇杆一动就会把边沿窗口冲刷掉，
///      表现为「推摇杆时按钮没反应」——本工程已整体弃用这条通路；
///   2) Button.onClick 只在**抬起**时触发一次，拿不到「按住」这个状态，
///      而举盾必须按住。
///   所以这里直接实现 IPointerDown/Up/Exit，把按下状态写进 TouchInputBridge。
///
/// 【为什么还要处理 OnDisable】
///   手指按住时如果面板弹出、控件被隐藏，PointerUp 不会派发，
///   盾牌状态会永久卡在「举着」。OnDisable 里强制归零兜住这种情况。
/// </summary>
public class TouchHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public void OnPointerDown(PointerEventData eventData) => TouchInputBridge.SetShieldHeld(true);

    public void OnPointerUp(PointerEventData eventData) => TouchInputBridge.SetShieldHeld(false);

    /// <summary>手指滑出按钮区域也算松开，避免「按住滑走后一直举着盾」。</summary>
    public void OnPointerExit(PointerEventData eventData) => TouchInputBridge.SetShieldHeld(false);

    private void OnDisable() => TouchInputBridge.SetShieldHeld(false);
}

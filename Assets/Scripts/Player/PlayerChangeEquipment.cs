using UnityEngine;

/// <summary>
/// 武器切换（剑↔弓）。
///
/// 【为什么触屏按钮走 Toggle 而不靠 GameInput】
///   ChangeBtn 上挂的 OnScreenButton 绑定 `<Gamepad>/rightShoulder`，
///   与摇杆的`<Gamepad>/leftStick`、攻击的 `<Gamepad>/buttonSouth`
///   **共用同一个虚拟手柄设备**。Unity 的虚拟手柄是共享状态：
///   摇杆每帧都在写 leftStick 值 → 设备每帧都有状态变化 →
///   `WasPressedThisFrame()` 的边沿窗口被持续冲刷，表现为
///   **移动过程中点切换按钮没反应**（静止时反而正常，容易漏测）。
///
///   修法：触屏按钮改为直接调 Toggle()，走 Unity UI 的 onClick，
///   完全不经过虚拟手柄那条链路。PC 键鼠仍走 GameInput.ChangeEquipmentPressed，
///   两条通路互不干扰。
/// </summary>
public class PlayerChangeEquipment : MonoBehaviour
{
    public PlayerCombat combat;
    public PlayerBow bow;

    private void Update()
    {
        // PC 键鼠通路（Q 键）。触屏不再走这里，见类注释。
        if (GameInput.ChangeEquipmentPressed) Toggle();
    }

    /// <summary>在已解锁的武器间切换。由触屏按钮的 OnClick 直接调用。</summary>
    public void Toggle()
    {
        if (StatsManager.Instance == null) return;

        // 没解锁弓时切换无效；只解锁弓而未解锁剑时，弓保持装备，不会误启用剑
        if (!StatsManager.Instance.hasBow) return;

        if (combat.enabled && StatsManager.Instance.hasSword)
        {
            combat.enabled = false;
            bow.enabled = true;
        }
        else if (bow.enabled)
        {
            bow.enabled = false;
            if (StatsManager.Instance.hasSword) combat.enabled = true;
        }
    }
}

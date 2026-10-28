using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 输入抽象门面（动作层）：游戏逻辑只读「动作」（Move/Attack/Interact…），
/// 不直接读物理按键。PC 键鼠、手柄、手机触屏各自把物理输入绑定到同一套动作，
/// 加端/换端只需补绑定，不改任何游戏逻辑。
/// 基于 Unity Input System（com.unity.inputsystem），键位与旧 InputManager 一致：
/// WASD/方向键=移动，K=挥剑，J=射箭，F=交互，Q=切换武器，1/2/L=面板，Shift=冲刺，Esc=取消。
/// 手柄/触屏绑定为 OnScreenStick/OnScreenButton 等虚拟控件预留（写入 Gamepad 控件位）。
/// </summary>
public static class GameInput
{
    private static readonly InputAction move;
    private static readonly InputAction sprint;
    private static readonly InputAction attack;
    private static readonly InputAction shoot;
    private static readonly InputAction interact;
    private static readonly InputAction changeEquipment;
    private static readonly InputAction toggleStats;
    private static readonly InputAction toggleQuestLog;
    private static readonly InputAction cancel;

    static GameInput()
    {
        // 移动：WASD + 方向键 + 实体手柄左摇杆。
        // 注意：**虚拟摇杆不再绑 <Gamepad>/leftStick**，它的值由 TouchInputBridge 单独提供。
        // 原因见 TouchInputBridge 类注释：所有 OnScreen 控件共用同一个虚拟 Gamepad 设备，
        // 摇杆每帧写 leftStick 会把该设备的状态变化拉满，导致同一设备上其他按钮
        // （buttonSouth/buttonNorth）的 WasPressedThisFrame() 边沿窗口被持续冲刷，
        // 表现为「推着摇杆时点任何按钮都没反应」。
        move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
        move.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        move.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/rightArrow");
        move.AddBinding("<Gamepad>/leftStick");

        sprint = NewButton("Sprint", "<Keyboard>/leftShift", "<Gamepad>/leftShoulder");
        // 攻击/交互的 <Gamepad>/buttonSouth / buttonNorth 绑定**保留**：
        // 它们服务的是实体手柄（Switch Pro / Xbox 手柄的 A/B 键）。
        // 触屏按键不再经由它们，而是由 TouchInputBridge 直接送标记（见 AttackPressed 属性）。
        attack = NewButton("Attack", "<Keyboard>/k", "<Gamepad>/buttonSouth");
        shoot = NewButton("Shoot", "<Keyboard>/j", "<Gamepad>/buttonWest");
        interact = NewButton("Interact", "<Keyboard>/f", "<Gamepad>/buttonNorth");
        changeEquipment = NewButton("ChangeEquipment", "<Keyboard>/q", "<Gamepad>/rightShoulder");
        toggleStats = NewButton("ToggleStats", "<Keyboard>/1");
        toggleQuestLog = NewButton("ToggleQuestLog", "<Keyboard>/l");
        cancel = NewButton("Cancel", "<Keyboard>/escape", "<Gamepad>/select");

        move.Enable();
        sprint.Enable();
        attack.Enable();
        shoot.Enable();
        interact.Enable();
        changeEquipment.Enable();
        toggleStats.Enable();
        toggleQuestLog.Enable();
        cancel.Enable();
    }

    private static InputAction NewButton(string name, params string[] bindings)
    {
        var action = new InputAction(name, InputActionType.Button);
        foreach (string path in bindings)
            action.AddBinding(path);
        return action;
    }

    /// <summary>
    /// 移动向量。键盘/实体手柄的 InputAction 值与虚拟摇杆的 TouchInputBridge 值合并。
    ///
    /// 【为什么合并而不是二选一】
    ///   手机上只有摇杆，PC 上只有键盘，理论上不会同时有值；但取二者之和并夹到单位圆内
    ///   比「if 触屏非零则用触屏」更健壮——后者在某些设备上（如外接手柄 + 摇杆同时可用）
    ///   会突然丢掉键盘输入。两者方向一致时和为 1，不会加速；
    ///   ClampMagnitude 保证相反方向时不会叠加出 2 倍速度。
    /// </summary>
    public static Vector2 Move
    {
        get
        {
            Vector2 v = move.ReadValue<Vector2>() + TouchInputBridge.Move;
            return v.sqrMagnitude > 1f ? v.normalized : v;
        }
    }

    /// <summary>冲刺是否按住。</summary>
    public static bool SprintHeld => sprint.IsPressed();

    /// <summary>
    /// 本帧按下挥剑。合并「键盘 K / 实体手柄 buttonSouth」与「触屏攻击键 onClick」两条通路。
    /// </summary>
    public static bool AttackPressed => attack.WasPressedThisFrame() || TouchInputBridge.AttackPressed;

    /// <summary>本帧按下射箭（PC J 键 / 手柄 buttonWest，触屏无独立射箭键——攻击键按装备分派）。</summary>
    public static bool ShootPressed => shoot.WasPressedThisFrame();

    /// <summary>
    /// 本帧按下交互。合并「键盘 F / 实体手柄 buttonNorth」与「触屏交互键 onClick」两条通路。
    /// </summary>
    public static bool InteractPressed => interact.WasPressedThisFrame() || TouchInputBridge.InteractPressed;

    /// <summary>本帧按下切换武器。</summary>
    public static bool ChangeEquipmentPressed => changeEquipment.WasPressedThisFrame();

    /// <summary>本帧按下属性面板开关。</summary>
    public static bool ToggleStatsPressed => toggleStats.WasPressedThisFrame();

    /// <summary>本帧按下任务日志面板开关。</summary>
    public static bool ToggleQuestLogPressed => toggleQuestLog.WasPressedThisFrame();

    /// <summary>本帧按下取消（Esc，关商店等）。</summary>
    public static bool CancelPressed => cancel.WasPressedThisFrame();
}

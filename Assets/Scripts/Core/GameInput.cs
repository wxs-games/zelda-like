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
    private static readonly InputAction toggleSkillTree;
    private static readonly InputAction toggleQuestLog;
    private static readonly InputAction cancel;

    static GameInput()
    {
        // 移动：WASD + 方向键 + 手柄左摇杆（虚拟摇杆通过 Gamepad 左摇杆位写入）
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
        attack = NewButton("Attack", "<Keyboard>/k", "<Gamepad>/buttonSouth");
        shoot = NewButton("Shoot", "<Keyboard>/j", "<Gamepad>/buttonWest");
        interact = NewButton("Interact", "<Keyboard>/f", "<Gamepad>/buttonNorth");
        changeEquipment = NewButton("ChangeEquipment", "<Keyboard>/q", "<Gamepad>/rightShoulder");
        toggleStats = NewButton("ToggleStats", "<Keyboard>/1");
        toggleSkillTree = NewButton("ToggleSkillTree", "<Keyboard>/2");
        toggleQuestLog = NewButton("ToggleQuestLog", "<Keyboard>/l");
        cancel = NewButton("Cancel", "<Keyboard>/escape", "<Gamepad>/select");

        move.Enable();
        sprint.Enable();
        attack.Enable();
        shoot.Enable();
        interact.Enable();
        changeEquipment.Enable();
        toggleStats.Enable();
        toggleSkillTree.Enable();
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

    /// <summary>移动向量（2DVector 合成控件，已归一化）。</summary>
    public static Vector2 Move => move.ReadValue<Vector2>();

    /// <summary>冲刺是否按住。</summary>
    public static bool SprintHeld => sprint.IsPressed();

    /// <summary>本帧按下挥剑。</summary>
    public static bool AttackPressed => attack.WasPressedThisFrame();

    /// <summary>本帧按下射箭。</summary>
    public static bool ShootPressed => shoot.WasPressedThisFrame();

    /// <summary>本帧按下交互。</summary>
    public static bool InteractPressed => interact.WasPressedThisFrame();

    /// <summary>本帧按下切换武器。</summary>
    public static bool ChangeEquipmentPressed => changeEquipment.WasPressedThisFrame();

    /// <summary>本帧按下属性面板开关。</summary>
    public static bool ToggleStatsPressed => toggleStats.WasPressedThisFrame();

    /// <summary>本帧按下技能树面板开关。</summary>
    public static bool ToggleSkillTreePressed => toggleSkillTree.WasPressedThisFrame();

    /// <summary>本帧按下任务日志面板开关。</summary>
    public static bool ToggleQuestLogPressed => toggleQuestLog.WasPressedThisFrame();

    /// <summary>本帧按下取消（Esc，关商店等）。</summary>
    public static bool CancelPressed => cancel.WasPressedThisFrame();
}

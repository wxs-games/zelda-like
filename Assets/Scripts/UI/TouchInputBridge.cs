using UnityEngine;

/// <summary>
/// 触屏输入桥：把虚拟摇杆/触屏按钮的原始值直接送到游戏逻辑，**完全不经过 Input System 的虚拟手柄**。
///
/// 【为什么必须绕开虚拟手柄——真机必现的「移动中按钮失效」】
///   Unity 的 OnScreenControl 家族（OnScreenStick / OnScreenButton）都把值写进
///   **同一个虚拟 Gamepad 设备**：摇杆写 `<Gamepad>/leftStick`、攻击写 `<Gamepad>/buttonSouth`、
///   交互写 `<Gamepad>/buttonNorth`。而 GameInput 读的是这些控件的
///   `WasPressedThisFrame()` —— 边沿窗口是靠「本帧状态与上帧状态比较」算出来的。
///   摇杆在移动时每帧都在写 leftStick，导致这个虚拟设备**每帧都产生状态变化**，
///   边沿窗口被持续冲刷 → 表现为：
///       **推着摇杆时点攻击/交互/任何虚拟按键按钮，全部没反应；松手静止后又正常。**
///   这个 bug 极具迷惑性：静止测试 100% 正常，必须边推摇杆边点按钮才复现，
///   所以很容易被误判成「按钮 raycast 被挡」而查错方向。
///
/// 【本类的职责】
///   1) Move：常驻值，摇杆拖动时持续写入，玩家控制器每帧读；
///   2) 攻击/交互：一次性触发标记，由触屏按钮的 Unity UI onClick 置位，
///      供 GameInput 合并读取，帧末（LateUpdate）清除。
///
/// 【与PC 键鼠的关系】
///   两条通路在 GameInput 里做「或」合并，互不干扰：
///   PC 键鼠仍读 InputAction（键盘/实体手柄），触屏读本类。
///   同一台设备上不会重复响应（手机上没有物理键盘）。
/// </summary>
public static class TouchInputBridge
{
    /// <summary>虚拟摇杆当前输出（-1..1，已含死区处理），由 FloatingOnScreenStick 每帧写入。</summary>
    public static Vector2 Move { get; private set; }

    /// <summary>攻击键本帧是否被按下（触屏通路）。</summary>
    public static bool AttackPressed { get; private set; }

    /// <summary>交互键本帧是否被按下（触屏通路）。</summary>
    public static bool InteractPressed { get; private set; }

    // 【为什么要记帧号而不是直接清】Unity 不保证脚本 Update 顺序。
    // 若玩家状态机的 Update 排在 EventSystem.Update() 之前，则本帧的点击
    // 是在玩家已经跑完之后才置位的；如果 LateUpdate 立刻清掉，玩家下一帧就再也读不到
    // → 表现为「点击偶发失灵」，且与帧率/脚本加载顺序相关，极难复现。
    // 修法：记录置位时的帧号，LateUpdate 只在「已经跨过一帧」后才清，
    // 保证任何执行顺序下，至少有一个完整的 Update 周期能读到该标记。
    private static int _attackSetFrame = -1;
    private static int _interactSetFrame = -1;

    // 摇杆归位时也会调一次，确保松手后不会残留上一帧的方向
    public static void SetMove(Vector2 value)
    {
        Move = value;
    }

    public static void PressAttack()
    {
        AttackPressed = true;
        _attackSetFrame = Time.frameCount;
    }

    public static void PressInteract()
    {
        InteractPressed = true;
        _interactSetFrame = Time.frameCount;
    }

    /// <summary>
    /// 帧末清除一次性标记（由 TouchUIController 在 LateUpdate 调用）。
    ///
    /// 只清除「在上一帧或更早」置位的标记：本帧刚置位的标记会保留到下一帧，
    /// 从而保证无论脚本 Update 顺序如何，玩家状态机都一定有一个完整周期读到它。
    /// </summary>
    public static void EndFrameClear()
    {
        if (_attackSetFrame < Time.frameCount) AttackPressed = false;
        if (_interactSetFrame < Time.frameCount) InteractPressed = false;
    }

    /// <summary>进入 Play 时清空，避免编辑器里残留上一次运行的标记。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLoad()
    {
        Move = Vector2.zero;
        AttackPressed = false;
        InteractPressed = false;
        _attackSetFrame = -1;
        _interactSetFrame = -1;
    }
}

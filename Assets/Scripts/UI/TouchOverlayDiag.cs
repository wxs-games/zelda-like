using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;

/// <summary>
/// 真机状态覆盖层（临时排障用，验证完整个文件 + 挂载处一起删）。
///
/// 为什么用 OnGUI 而不是 UGUI Text：
///   零依赖——不需要 Canvas、不需要字体资源（本项目 LegacyRuntime.ttf 在部分
///   中国版 Unity 里取不到，UGUI 方案会有字体回退失败的风险），
///   而且不受任何 Canvas 排序/遮挡影响，永远看得见。
///
/// 打印内容（全 ASCII，避免 OnGUI 内置字体缺中文字形）：
///   峰值同时手指数 / 当前手指数 / 攻击按钮累计点击次数 /
///   剑弓解锁状态 / 玩家战斗组件启用状态
///
/// 用法：让玩家正常玩，随时 adb 截图读这几个数即可，不需要玩家口述。
/// </summary>
public class TouchOverlayDiag : MonoBehaviour
{
    private int _peakFingers;
    private int _curFingers;
    private int _attackClicks;

    private void Awake()
    {
        // Touch.activeTouches 不调这句永远为空
        EnhancedTouchSupport.Enable();
        TouchSimulation.Enable();
    }

    private void Update()
    {
        // 路径1：EnhancedTouch（当前按住的真实手指数）
        // 注意必须写全限定名：using UnityEngine 与 EnhancedTouch 同时引入时
        // `Touch` 会在 UnityEngine.Touch 与 EnhancedTouch.Touch 之间二义（CS0104）
        int enhanced = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count;

        // 路径2：直接数 Touchscreen 上 press=true 的槽位（冗余，防单一 API 失效）
        int pressedSlots = 0;
        var ts = Touchscreen.current;
        if (ts != null)
        {
            for (int i = 0; i < ts.touches.Count; i++)
                if (ts.touches[i].press.isPressed) pressedSlots++;
        }

        _curFingers = Mathf.Max(enhanced, pressedSlots);
        if (_curFingers > _peakFingers) _peakFingers = _curFingers;
    }

    /// <summary>由 TouchUIController 的攻击按钮回调调用，累计点击数。</summary>
    public void CountAttackClick()
    {
        _attackClicks++;
        Debug.Log("[Overlay] AttackBtn clicked, total=" + _attackClicks);
    }

    private void OnGUI()
    {
        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 34,
            alignment = TextAnchor.UpperLeft
        };
        style.normal.textColor = Color.black;   // 先画深色描边再画亮色，草地上也看得清

        var stats = StatsManager.GetActive();
        bool hasSword = stats != null && stats.hasSword;
        bool hasBow = stats != null && stats.hasBow;

        var pc = FindObjectOfType<PlayerController>();
        bool combatOn = pc != null && pc.playerCombat != null && pc.playerCombat.enabled;
        bool bowOn = pc != null && pc.playerBow != null && pc.playerBow.enabled;

        string text =
            "FINGERS cur=" + _curFingers + " PEAK=" + _peakFingers + "\n" +
            "ATTACK_CLICKS=" + _attackClicks + "\n" +
            "sword=" + (hasSword ? 1 : 0) + " bow=" + (hasBow ? 1 : 0) + "\n" +
            "combat=" + (combatOn ? 1 : 0) + " bowComp=" + (bowOn ? 1 : 0) + "\n" +
            "Move=" + TouchInputBridge.Move.ToString("F2");

        var r = new Rect(12, 120, 900, 260);
        var prev = style.normal.textColor;
        style.normal.textColor = Color.black;
        GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, style);
        style.normal.textColor = Color.white;
        GUI.Label(r, text, style);
        style.normal.textColor = prev;
    }
}

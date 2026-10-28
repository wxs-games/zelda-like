using UnityEngine;

/// <summary>
/// 盾牌格挡。
///
/// 【设计（对齐塞尔达）】
///   盾一直挂在左臂上，平时不上举；**按住盾键**才举到身前，松开立刻放下。
///   举盾期间：正面来的伤害被挡（默认完全免伤），背面照常受伤。
///   能不能举盾由「是否捡到盾」决定（StatsManager.hasShield），没捡到时按键无效。
///
/// 【为什么用「按住」而不是「切换」】
///   格挡是瞬时决策动作，必须能在半秒内进出。切换式会出现「忘了收盾」，
///   站着不动却一直免伤，破坏战斗节奏。
///
/// 【和移动的关系】
///   举盾时默认站桩（lockMovementWhileBlocking）。塞尔达 2D 作里举盾不能跑，
///   这条约束同时解决了「举盾姿势只有一帧、没有行走帧」的美术问题。
/// </summary>
public class PlayerShield : MonoBehaviour
{
    [Tooltip("举盾时是否禁止移动。美术上举盾没有行走帧，默认锁死")]
    public bool lockMovementWhileBlocking = true;

    [Tooltip("被挡住的伤害保留比例。0 = 完全免伤，1 = 完全不挡")]
    [Range(0f, 1f)] public float blockedDamageRatio = 0f;

    [Tooltip("格挡成功时的音效音量")]
    [Range(0f, 1f)] public float blockSoundVolume = 0.7f;

    /// <summary>当前是否处于举盾状态（由 Update 每帧刷新，其它系统只读）。</summary>
    public bool IsBlocking { get; private set; }

    /// <summary>是否已经拥有盾（捡到盾牌道具）。</summary>
    public bool HasShield
    {
        get
        {
            var stats = StatsManager.GetActive();
            return stats != null && stats.hasShield;
        }
    }

    private PlayerController _controller;
    private PlayerHealth _health;

    private void Awake()
    {
        _controller = GetComponent<PlayerController>();
        _health = GetComponent<PlayerHealth>();
    }

    private void Update()
    {
        bool want = HasShield && GameInput.ShieldHeld;

        // 挥剑、受击、死亡时不允许举盾：否则会出现「边砍边挡」的双重收益，
        // 死亡状态下血量归零还能挡会让玩家永不掉血。
        if (want && _controller != null && _controller.anim != null
            && _controller.anim.GetBool("isAttacking"))
        {
            want = false;
        }
        if (want && _health != null && _health.IsDead) want = false;

        IsBlocking = want;
    }

    /// <summary>
    /// 判断一次来自 <paramref name="source"/> 的攻击是否被盾挡住。
    ///
    /// 判定用「攻击者在自己面朝的哪一侧」而不是精确碰撞盒——
    /// 像素风小怪贴脸时碰撞盒互相穿插，用物理判定会出现「明明举着盾却挨打」。
    /// 侧向偏移很小（几乎同 x）时按挡住处理，避免擦边判成背后中招。
    /// </summary>
    public bool BlocksHitFrom(Transform source)
    {
        if (!IsBlocking) return false;
        if (source == null) return false;

        float facing = transform.localScale.x >= 0f ? 1f : -1f;
        float dx = source.position.x - transform.position.x;
        if (Mathf.Abs(dx) < 0.12f) return true;

        return Mathf.Sign(dx) == facing;
    }

    /// <summary>播放格挡成功反馈。音量压低，避免连续挨打时盖住其它音效。</summary>
    public void PlayBlockFeedback()
    {
        SoundManager.Instance?.PlayShieldBlock(blockSoundVolume);
    }
}

using UnityEngine;

/// <summary>
/// 玩家装备外观：按「有没有剑 / 有没有盾 / 是否举盾」切换整套动画。
///
/// 【为什么是四套动画而不是叠武器图层】
///   Tiny Swords 的 Warrior 图集把剑和盾**画进了身体每一帧**，没有独立武器图层，
///   也就没法像挂骨点那样动态附加。派生图集（去掉剑/去掉盾/举盾）见
///   Assets/Sprites/Player/ 下 5 张表，由 Tools 菜单重新生成。
///
/// 【实现方式：AnimatorOverrideController】
///   不复制 AnimatorController（里面有弓箭的第二层 ArcherLayer，复制会漂移），
///   而是给每个外观建一个 Override，只替换 Idle / Waking / Slash_player 三段 clip。
///   状态机图、参数、层权重全继承自 baseController，改动面最小。
///
/// 【为什么举盾也走 override】
///   不动状态机就加不了 Block 状态；而举盾时玩家本来就站桩（PlayerShield 锁移动），
///   当前状态必然是 Idle。所以只要把 Idle 的 clip 换成「举盾帧」即可，
///   等价于一个 Block 状态，却不需要给 controller 加参数和转移。
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerEquipmentVisual : MonoBehaviour
{
    [Header("各装备外观（AnimatorOverrideController）")]
    [Tooltip("剑 + 盾（默认外观，与原图集一致）")]
    public RuntimeAnimatorController withSwordAndShield;

    [Tooltip("只有剑")]
    public RuntimeAnimatorController swordOnly;

    [Tooltip("只有盾")]
    public RuntimeAnimatorController shieldOnly;

    [Tooltip("空手（开局）")]
    public RuntimeAnimatorController bare;

    [Tooltip("举盾（持剑）")]
    public RuntimeAnimatorController blockWithSword;

    [Tooltip("举盾（无剑）")]
    public RuntimeAnimatorController blockShieldOnly;

    private Animator _anim;
    private PlayerShield _shield;

    /// <summary>上一次应用的外观键，避免每帧写 runtimeAnimatorController（会触发 Animator 重建）。</summary>
    private int _lastKey = int.MinValue;

    private void Awake()
    {
        _anim = GetComponent<Animator>();
        _shield = GetComponent<PlayerShield>();
    }

    private void Start()
    {
        Apply(true);
    }

    private void Update()
    {
        Apply(false);
    }

    /// <summary>按当前装备状态套用外观。force=true 时忽略缓存强制写一次。</summary>
    public void Apply(bool force)
    {
        var stats = StatsManager.GetActive();
        if (stats == null || _anim == null) return;

        bool sword = stats.hasSword;
        bool shield = stats.hasShield;
        bool blocking = shield && _shield != null && _shield.IsBlocking;

        // 位编码：剑=2，盾=1，举盾=+100
        int key = (sword ? 2 : 0) + (shield ? 1 : 0) * 10 + (blocking ? 100 : 0);
        if (!force && key == _lastKey) return;
        _lastKey = key;

        var target = Resolve(sword, shield, blocking);
        if (target == null) return;
        if (_anim.runtimeAnimatorController == target) return;

        // 换 controller 会触发 Animator 重建，层权重被打回各层的 defaultWeight。
        // Play.controller 的 Base Layer 默认权重是 0（平时靠 PlayerBow 的 OnEnable/OnDisable
        // 手动置 0/1 切换），重建后不还原的话角色会整条消失。
        int layers = _anim.layerCount;
        var weights = new float[layers];
        for (int i = 0; i < layers; i++) weights[i] = _anim.GetLayerWeight(i);

        _anim.runtimeAnimatorController = target;

        // 权重全为 0 说明之前也没显示（例如刚初始化），此时至少把身体那一层打开
        bool anyVisible = false;
        for (int i = 0; i < layers; i++) if (weights[i] > 0.001f) anyVisible = true;
        if (!anyVisible)
        {
            for (int i = 0; i < layers; i++) weights[i] = 0f;
            weights[0] = 1f;
        }
        for (int i = 0; i < layers; i++) _anim.SetLayerWeight(i, weights[i]);
    }

    private RuntimeAnimatorController Resolve(bool sword, bool shield, bool blocking)
    {
        if (blocking)
        {
            // 举盾必须有盾；有剑时用「剑+举盾」的帧
            return sword && blockWithSword != null ? blockWithSword : blockShieldOnly;
        }
        if (sword && shield) return withSwordAndShield;
        if (sword) return swordOnly;
        if (shield) return shieldOnly;
        return bare;
    }
}

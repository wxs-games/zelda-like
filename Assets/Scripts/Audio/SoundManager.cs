using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 全局音频管理器：单例，挂 GameRoot 随持久根存活。
///
/// 【为什么需要它】
///   音频资源不在场景里，而是集中挂在本组件的公开字段上，由策划在 Inspector
///   里一次性拖好。任何系统要出声只需SoundManager.Instance?.PlaySfx(...)，
///   不必自己找 AudioSource、不必管对象池、不必管场景切换。
///
/// 【三个必须处理好的问题】
///   1. 场景切换：GameRoot 是 DontDestroyOnLoad，音源不会随场景销毁；
///      但若音频源挂在普通场景对象上就会失活——所以本组件固定常驻。
///   2. 同时播放多个音效：同一帧可能既有挥剑又有受击，单个 AudioSource 会互相打断。
///      故用固定大小的音源池轮转（community 主流做法，无需 Instantiate/Destroy）。
///   3. 暂停：商店/对话/面板会把 Time.timeScale 设为 0。
///      若音效源用 AudioSource.Play()，timeScale=0 时声音会变调/卡住；
///      故音效一律用 PlayOneShot 并让该音源 ignoreListenerPause=false，
///      BGM 则交给 AudioListener.pause 统一暂停。
/// </summary>
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    // ---------- BGM ----------
    [Header("背景音乐")]
    public AudioClip bgm;
    [Range(0f, 1f)] public float bgmVolume = 0.35f;

    // ---------- 音效 ----------
    [Header("音效")]
    public AudioClip swordSwing;
    public AudioClip bowShoot;
    public AudioClip enemyHit;
    public AudioClip enemyDie;
    public AudioClip coinPickup;
    public AudioClip questComplete;
    public AudioClip uiClick;
    public AudioClip purchase;
    public AudioClip playerHurt;
    public AudioClip uiHover;
    public AudioClip levelUp;
    public AudioClip jump;
    [Tooltip("盾牌挡下攻击的撞击声。留空时回退到 enemyHit（金属撞击感接近）")]
    public AudioClip shieldBlock;

    [Header("音量")]
    [Range(0f, 1f)] public float sfxVolume = 0.7f;

    [Header("并发")]
    [Tooltip("音效池大小。同时最多能叠加多少个音效。")]
    [Range(2, 16)] public int sfxPoolSize = 8;

    // 运行时
    private AudioSource _bgmSource;
    private AudioSource[] _sfxPool;
    private int _sfxCursor;
    private bool _subscribedToPause;

    /// <summary>当 timeScale 变 0（暂停）时，是否应暂停 BGM。
    /// 商店/对话/属性面板都可能置 0，这里统一响应。</summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // 与 DialogManager/QuestManager 一致：只销毁重复组件，不动 GameObject
            Destroy(this);
            return;
        }
        Instance = this;
        // 本组件挂在 GameRoot 下的子节点上，而 DontDestroyOnLoad 只对根节点生效，
        // 故显式对根 GameObject 调用一次（GameRoot 本就由 GameBootstrap 持久化）
        DontDestroyOnLoad(transform.root.gameObject);

        // 专用 BGM 音源
        _bgmSource = gameObject.AddComponent<AudioSource>();
        _bgmSource.clip = bgm;
        _bgmSource.loop = true;
        _bgmSource.volume = bgmVolume;
        _bgmSource.playOnAwake = false;      // 等 Start 里按场景逻辑决定
        _bgmSource.spatialBlend = 0f;        // 2D 音乐，不做空间化
        _bgmSource.outputAudioMixerGroup = null;

        // 音效池：全部预建，避免播放时才 Instantiate
        int n = Mathf.Max(2, sfxPoolSize);
        _sfxPool = new AudioSource[n];
        for (int i = 0; i < n; i++)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 0f;
            src.volume = sfxVolume;
            _sfxPool[i] = src;
        }
    }

    private void Start()
    {
        // 只有真正持有资源的那份才订阅（重复实例已在 Awake 被销毁）
        if (Instance != this) return;

        // 暂停时 BGM 跟随 AudioListener 一起暂停，音效不受影响
        SceneManager.activeSceneChanged += OnSceneChanged;
        _subscribedToPause = true;

        PlayBgm();
    }

    private void OnDestroy()
    {
        if (_subscribedToPause)
        {
            SceneManager.activeSceneChanged -= OnSceneChanged;
            _subscribedToPause = false;
        }
        if (Instance == this) Instance = null;
    }

    private void OnSceneChanged(Scene from, Scene to)
    {
        // 场景切换后如果BGM 没在放，重新起播（防止因音源被意外停掉而永久静音）
        if (_bgmSource != null && bgm != null && !_bgmSource.isPlaying)
            PlayBgm();
    }

    // ==========================================================
    // 对外接口
    // ==========================================================

    /// <summary>播放一个音效。clip 为空时静默忽略（素材未配置不应报错）。</summary>
    public void PlaySfx(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null || _sfxPool == null || _sfxPool.Length == 0) return;

        // 轮转到下一个空闲位：找到没在播的优先，否则强制覆盖最旧的那个
        AudioSource chosen = null;
        for (int i = 0; i < _sfxPool.Length; i++)
        {
            var cand = _sfxPool[(_sfxCursor + i) % _sfxPool.Length];
            if (!cand.isPlaying) { chosen = cand; _sfxCursor = (_sfxCursor + i + 1) % _sfxPool.Length; break; }
        }
        // 全都在播：抢当前游标指向的那个（最旧）
        if (chosen == null)
        {
            chosen = _sfxPool[_sfxCursor];
            _sfxCursor = (_sfxCursor + 1) % _sfxPool.Length;
        }

        chosen.volume = Mathf.Clamp01(sfxVolume * volumeScale);
        // PlayOneShot：多声源可在同一 AudioSource 上叠加且不打断自己，
        // 但这里用的是独立音源，直接 Play() 语义更清晰
        chosen.PlayOneShot(clip, chosen.volume);
    }

    /// <summary>开始播放 BGM（幂等）。</summary>
    public void PlayBgm()
    {
        if (_bgmSource == null || bgm == null) return;
        if (_bgmSource.isPlaying) return;
        _bgmSource.clip = bgm;
        _bgmSource.volume = bgmVolume;
        _bgmSource.Play();
    }

    public void StopBgm()
    {
        if (_bgmSource != null) _bgmSource.Stop();
    }

    /// <summary>换BGM（跨场景切换不同音乐时用）。传 null 表示不换。</summary>
    public void SetBgm(AudioClip newBgm)
    {
        if (newBgm == null) return;
        bgm = newBgm;
        if (_bgmSource == null) return;
        _bgmSource.clip = newBgm;
        _bgmSource.volume = bgmVolume;
        _bgmSource.Play();
    }

    /// <summary>运行时改音量（给设置面板用）。</summary>
    public void SetBgmVolume(float v)
    {
        bgmVolume = Mathf.Clamp01(v);
        if (_bgmSource != null) _bgmSource.volume = bgmVolume;
    }

    public void SetSfxVolume(float v)
    {
        sfxVolume = Mathf.Clamp01(v);
        if (_sfxPool == null) return;
        foreach (var src in _sfxPool) src.volume = sfxVolume;
    }

    // ---------- 语义化快捷方法（埋点处直接调用，可读性更好） ----------

    public void PlaySwordSwing() => PlaySfx(swordSwing);
    public void PlayBowShoot() => PlaySfx(bowShoot);
    public void PlayEnemyHit() => PlaySfx(enemyHit);
    public void PlayEnemyDie() => PlaySfx(enemyDie);
    public void PlayCoin() => PlaySfx(coinPickup);
    public void PlayQuestComplete() => PlaySfx(questComplete);
    public void PlayUiClick() => PlaySfx(uiClick);
    public void PlayPurchase() => PlaySfx(purchase);
    public void PlayPlayerHurt() => PlaySfx(playerHurt);
    public void PlayUiHover() => PlaySfx(uiHover);
    public void PlayLevelUp() => PlaySfx(levelUp);
    public void PlayJump() => PlaySfx(jump);

    /// <summary>盾牌格挡成功。shieldBlock 未配置时回退到 enemyHit。</summary>
    public void PlayShieldBlock(float volumeScale = 1f)
    {
        PlaySfx(shieldBlock != null ? shieldBlock : enemyHit, volumeScale);
    }
}
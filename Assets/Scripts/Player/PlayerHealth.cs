using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// 玩家血量与死亡处理。
///
/// 【死亡复活，重要】
/// 原实现血量归零只做 gameObject.SetActive(false)——玩家直接消失，且
/// playerCurrentHealth 停在 0，全工程没有任何复活逻辑，导致死亡后卡死无法继续。
/// 现改为：死亡 → 隐藏玩家 → 延时 → 在出生点复活 → 回满血。
/// 复活点是 Start 时记录的本场景出发点，与塞尔达类惯例一致。
///
/// 【关键实现约束】
/// 复活计时**不能**跑在玩家对象自己的协程上——SetActive(false) 会停掉
/// 该对象所有协程。必须交给常驻的 RespawnDriver 执行，详见该类注释。
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    public TMP_Text healthText;
    public Animator healthTextAnim;

    [Header("死亡与复活")]
    [Tooltip("死亡后到复活之间的等待秒数")]
    public float respawnDelay = 1.5f;

    [Tooltip("复活提示对象（可选）。留空则不显示，不影响功能。")]
    public GameObject respawnIndicator;

    [Tooltip("死亡与复活时清空刚体速度，避免残余动量把玩家带跑")]
    public bool zeroVelocityOnRespawn = true;

    private bool _isDead;
    private Vector3 _spawnPoint;
    private Rigidbody2D _body;
    private PlayerController _controller;

    void Start()
    {
        _spawnPoint = transform.position;
        _body = GetComponent<Rigidbody2D>();
        _controller = GetComponent<PlayerController>();

        // ForestArea 没有 UI 树，healthText 引用会为空，判空避免 NRE
        RefreshHealthText();
    }

    private void RefreshHealthText()
    {
        if (healthText == null) return;
        var stats = StatsManager.GetActive();
        if (stats == null) return;
        healthText.text = "HP: " + stats.playerCurrentHealth
                          + "/" + stats.playerMaxHealth;
    }

    public void ChangeHealth(float amount)
    {
        if (_isDead) return;

        // 取当前**活动场景**里激活的 StatsManager。
        // 注意不能用 GetComponentInParent：StatsManager 挂在 GameRoot/UIManager 之下，
        // 与 Player 是兄弟分支，不是祖先，拿不到（实测返回 null）。
        var stats = StatsManager.GetActive();
        if (stats == null) return;

        stats.playerCurrentHealth += amount;
        stats.playerCurrentHealth = Mathf.Clamp(
            stats.playerCurrentHealth, 0, stats.playerMaxHealth);

        if (healthTextAnim != null) healthTextAnim.Play("TextUpdate");
        RefreshHealthText();

        if (stats.playerCurrentHealth <= 0)
        {
            Die();
        }
    }

    /// <summary>死亡：清速度、停手、隐藏玩家，把复活计时交给常驻驱动器。</summary>
    private void Die()
    {
        if (_isDead) return;
        _isDead = true;

        if (_body != null)
        {
            _body.velocity = Vector2.zero;
            _body.angularVelocity = 0f;
        }

        if (_controller != null && _controller.anim != null)
        {
            _controller.anim.SetBool("isAttacking", false);
            _controller.anim.SetBool("isRunning", false);
        }

        // 交给永不失活的驱动器计时，绝不能StartCoroutine 在自己身上（见类注释）
        var driver = RespawnDriver.Instance;
        if (driver != null)
        {
            driver.Schedule(WaitRespawn(), Respawn);
        }
        else
        {
            // 兜底：驱动器缺失时立即复活，避免玩家永久卡死
            Debug.LogWarning("[PlayerHealth] 缺少 RespawnDriver，立即复活");
            Respawn();
            return;
        }

        gameObject.SetActive(false);
    }

    /// <summary>复活等待计时。用 unscaledDeltaTime，不受 timeScale 影响（商店会置 0）。</summary>
    private IEnumerator WaitRespawn()
    {
        float waited = 0f;
        while (waited < respawnDelay)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    /// <summary>在出生点复活并回满血。</summary>
    private void Respawn()
    {
        // 取当前活动场景里激活的 StatsManager，而不是盲信静态 Instance。
        //
        // 【既存架构坑】两个场景都放了 GameRoot 实例，Awake 时静态 Instance 指向先加载的那个；
        // 玩家所属的那个可能是未激活副本。直接写 Instance 会写到错误实例上，
        // 实测出现过「复活后 HP 仍是 50/100」——血量被写进了另一个实例。
        // GetActive() 会在必要时纠正静态引用，详见 StatsManager.OnEnable 的注释。
        var stats = StatsManager.GetActive();

        if (stats != null)
        {
            stats.playerCurrentHealth = stats.playerMaxHealth;
            if (stats.playerHealthText != null)
                stats.playerHealthText.text = "HP:" + stats.playerCurrentHealth + "/" + stats.playerMaxHealth;
        }

        transform.position = _spawnPoint;

        if (zeroVelocityOnRespawn && _body != null)
        {
            _body.velocity = Vector2.zero;
            _body.angularVelocity = 0f;
        }

        gameObject.SetActive(true);

        _isDead = false;
        RefreshHealthText();
    }

    public bool IsDead => _isDead;
}

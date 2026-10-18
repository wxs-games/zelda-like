using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class StatsManager : MonoBehaviour
{
    public static  StatsManager Instance;
    public StatsUI statsUI;
    public TMP_Text playerHealthText;

    [Header("Combat Stats")]
    public float playerweaponRange;
    public float playerKnockbackForce;
    public float playerKnockbackStun;
    public int playerDamage;


    [Header("Movement Stats")]
    public int playerSpeed = 5;

    [Header("Health Stats")]
    public float enemyMaxHealth;
    public float enemyCurrentHealth;
    public float playerMaxHealth;
    public float playerCurrentHealth;

    // Unlocked Abilities：能力解锁是永久的，区别于 ItemEffectApplier 里的 duration 临时 buff；
    // 静态后备字段让能力跨场景切换不丢失（属性不进 Inspector，状态以代码为准）
    public bool hasSword
    {
        get => _hasSword;
        set => _hasSword = value;
    }
    public bool hasBow
    {
        get => _hasBow;
        set => _hasBow = value;
    }

    // 静态后备字段：每个场景各有一份 StatsManager 实例，但能力状态全局唯一
    static bool _hasSword = false;
    static bool _hasBow = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance=this;
        }
        else if(Instance!=null&&Instance!=this)
        {
            Destroy(this);
        }
    }

    // 两个场景各放了一个 GameRoot，各带一份 StatsManager。
    // Awake 先到的那份会把 Instance 占住，但场景切换后它所在的 GameRoot 被停用，
    // 血量就写进了"看不见的那一份"——表现是复活后 HP 只回一半、HUD 数字不动。
    // 每次启用时抢占 Instance，保证 Instance 始终指向当前活动场景里的那一份。
    private void OnEnable()
    {
        if (Instance == null || !Instance.isActiveAndEnabled || Instance.gameObject.scene != gameObject.scene)
        {
            Instance = this;
        }
    }

    /// <summary>解析当前活动场景里的 StatsManager，并把静态 Instance 纠正到它上面。</summary>
    public static StatsManager GetActive()
    {
        if (Instance != null && Instance.isActiveAndEnabled) return Instance;

        // FindObjectsOfTypeAll 含未激活对象，才能把被停用的那份也捞出来比较
        var all = Resources.FindObjectsOfTypeAll<StatsManager>();
        StatsManager best = null;
        int bestDepth = int.MinValue;
        for (int i = 0; i < all.Length; i++)
        {
            var candidate = all[i];
            if (candidate == null || !candidate.isActiveAndEnabled) continue;
            if (!candidate.gameObject.scene.isLoaded) continue;
            // 同为活动场景时取层级最深的那个：它最可能是当前真正在跑的那份
            int depth = 0;
            var t = candidate.transform;
            while (t.parent != null) { depth++; t = t.parent; }
            if (depth > bestDepth) { bestDepth = depth; best = candidate; }
        }
        if (best != null) Instance = best;
        return best != null ? best : Instance;
    }
    public void UpdateMaxHealth(int amount)
    {
        playerMaxHealth += amount;
        playerHealthText.text = "HP:" + playerCurrentHealth + "/" + playerMaxHealth;

    }
    public void UpdateHealth(int amount)
    {
        playerCurrentHealth += amount;
        if (playerCurrentHealth < enemyMaxHealth)
        {

            playerHealthText.text = "HP:" + playerCurrentHealth + "/" + playerMaxHealth;
        }
        else
        {
            playerCurrentHealth = playerMaxHealth;
            playerHealthText.text = "HP:" + playerCurrentHealth + "/" + playerMaxHealth;

        }
    }
    public void UpdateSpeed(int amount)
    {
        playerSpeed += amount;
        statsUI.UpdateAllStats();
       

    }
}

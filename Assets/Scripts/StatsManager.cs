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

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;


public class ExpManager : MonoBehaviour
{
    public static ExpManager Instance { get; private set; }

    public int level;
    public int currentExp;
    public int expToLevel=10;
    public float expGrowMultiplier = 1.2f;
    public Slider slider;
    public TMP_Text currentLevelText;
    public static event Action<int> OnLevelUp;

    private void Awake()
    {
        // 挂在持久根 ExpCanvas 上；重复场景实例只销毁自己
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        UpdateUI();
    }
    public void GainExperience(int count)
    {
        currentExp += count;
        while(currentExp >= expToLevel)
        {
            LevelUp();
           
        }
        UpdateUI();
    }
    public void LevelUp()
    {
        level++;
        currentExp -= expToLevel;
        expToLevel = Mathf.RoundToInt(expToLevel * expGrowMultiplier);
        OnLevelUp?.Invoke(1);


    }


    private void OnEnable()
    {
        EnemyHealth.OnMonsterDefeated += HandleMonsterDefeated;
    }
    private void OnDisable()
    {
        EnemyHealth.OnMonsterDefeated -= HandleMonsterDefeated;
    }

    // 杀敌事件现在带 enemyId（任务系统用），经验系统只取 exp
    private void HandleMonsterDefeated(string enemyId, int exp)
    {
        GainExperience(exp);
    }
    public void UpdateUI()
    {
        slider.maxValue = expToLevel;
        slider.value = currentExp;
        currentLevelText.text = "Level:" + level;
    }
}

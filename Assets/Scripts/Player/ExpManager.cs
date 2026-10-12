using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;


public class ExpManager : MonoBehaviour
{
    public int level;
    public int currentExp;
    public int expToLevel=10;
    public float expGrowMultiplier = 1.2f;
    public Slider slider;
    public TMP_Text currentLevelText;
    public static event Action<int> OnLevelUp;


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
        EnemyHealth.OnMonsterDefeated += GainExperience;
    }
    private void OnDisable()
    {
        EnemyHealth.OnMonsterDefeated -= GainExperience;
    }
    public void UpdateUI()
    {
        slider.maxValue = expToLevel;
        slider.value = currentExp;
        currentLevelText.text = "Level:" + level;
    }
}

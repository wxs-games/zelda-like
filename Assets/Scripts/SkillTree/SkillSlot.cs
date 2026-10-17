using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
public class SkillSlot : MonoBehaviour
{
    public SkillSO skillSO;
    public int currentLevel;
    public bool isUnlocked;
    public Image skillIcon;
    public TMP_Text skillLevelText;
    public Button skillButton;
    public static event Action<SkillSlot> OnAbilityPointSpent;
    public static event Action<SkillSlot> OnSkillMaxed;
    public List<SkillSlot> prerequisiteSkillSlots;
    public void TryUpgradeSkill()
    {
        if (isUnlocked&&currentLevel<skillSO.maxLevel)
        {
            currentLevel++;
            OnAbilityPointSpent?.Invoke(this);
            if (currentLevel >= skillSO.maxLevel)
            {
                OnSkillMaxed?.Invoke(this);

            }
            OnValidate();
        }
    }
    public bool CanUnlocked()
    {
        foreach (SkillSlot slot in prerequisiteSkillSlots)
        {
            if (!slot.isUnlocked||slot.currentLevel<slot.skillSO.maxLevel )
            {
                return false;
            }
        }
        return true;
    }
  
    public void Unlock()
    {
        isUnlocked = true;
        UpdateUI();
    }
    private void OnValidate()
    {
        if (skillSO != null&&skillLevelText!=null)
        {
            
            UpdateUI();
        }
    }
    private void UpdateUI()
    {
        if (skillSO == null || skillIcon == null) return;

        skillIcon.sprite = skillSO.skillIcon;
        if (isUnlocked)
        {
            skillButton.interactable = true;
            skillLevelText.text = currentLevel.ToString()+"/"+skillSO.maxLevel.ToString();
            skillIcon.color=Color.white;
        }
        else
        {
            skillButton.interactable = false;
            skillIcon.color = Color.grey;
            skillLevelText.text = "Locked";
        }
    }

    /// <summary>对外公开的刷新入口：读档直接改字段后调用，避免依赖私有的 UpdateUI。</summary>
    public void RefreshUI()
    {
        UpdateUI();
    }
}

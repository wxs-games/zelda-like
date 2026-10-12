using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
public class SkillTreeManager : MonoBehaviour
{
    public SkillSlot[] skillSlots;
    public TMP_Text pointsText;
    public int availablePoints;
    void OnEnable()
    {
        SkillSlot.OnAbilityPointSpent += HandleAbilityPointsSpent;
        SkillSlot.OnSkillMaxed += HandleSkillMaxed;
        ExpManager.OnLevelUp += UpdateAbilityPoints;
    }
    void OnDisable()
    {
        SkillSlot.OnAbilityPointSpent -= HandleAbilityPointsSpent;
        SkillSlot.OnSkillMaxed -= HandleSkillMaxed;
        ExpManager.OnLevelUp-=UpdateAbilityPoints;
    }
    void Start()
    {
        foreach (SkillSlot slot in skillSlots)
        {
            slot.skillButton.onClick.AddListener(() => CheckAvailablePoints(slot));
        }
        UpdateAbilityPoints(0);
    }
    public void CheckAvailablePoints(SkillSlot slot)
    {
        if (availablePoints > 0)
        {
            slot.TryUpgradeSkill();
        }
    }
    private void HandleAbilityPointsSpent(SkillSlot skillSlot)
    {
        if (availablePoints > 0)
        {
            UpdateAbilityPoints(-1);
        }
    }
    private void HandleSkillMaxed(SkillSlot skillSlot)
    {
        foreach (SkillSlot slot in skillSlots)
        {
            if (!slot.isUnlocked&&slot.CanUnlocked())
            {
                slot.Unlock();
            }
           


        }
    }
    public void UpdateAbilityPoints(int amount)
    {
        availablePoints += amount;
        pointsText.text = "points: " + availablePoints;
    }
}

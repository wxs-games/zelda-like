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
            if (slot == null || slot.skillButton == null) continue;
            slot.skillButton.onClick.AddListener(() => CheckAvailablePoints(slot));
        }
        UnlockRootSkills();
        UpdateAbilityPoints(0);
    }

    /// <summary>
    /// 无前置的技能是技能树的根，开局即可点。带前置的技能必须靠前置满级来解锁，
    /// 由 <see cref="HandleSkillMaxed"/> 负责。
    /// </summary>
    private void UnlockRootSkills()
    {
        foreach (SkillSlot slot in skillSlots)
        {
            if (slot == null || slot.skillSO == null) continue;
            if (slot.isUnlocked) continue;
            if (slot.prerequisiteSkillSlots != null && slot.prerequisiteSkillSlots.Count > 0) continue;

            slot.Unlock();
        }
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
    /// <summary>
/// 某个技能满级后的连锁解锁：
/// 只解锁「把本次满级技能列为前置」的槽位。
/// 不能遍历全表解锁所有可解锁项——那样任何无前置的根技能都会被其他技能的满级
/// 顺带解锁（例如剑伤害满级会误开本应独立的电闪雷鸣）。
/// </summary>
    private void HandleSkillMaxed(SkillSlot skillSlot)
    {
        if (skillSlot == null) return;

        foreach (SkillSlot slot in skillSlots)
        {
            if (slot == null || slot == skillSlot) continue;
            if (slot.isUnlocked) continue;
            if (slot.prerequisiteSkillSlots == null) continue;
            if (!slot.prerequisiteSkillSlots.Contains(skillSlot)) continue;

            if (slot.CanUnlocked())
            {
                slot.Unlock();
            }
        }
    }
    public void UpdateAbilityPoints(int amount)
    {
        availablePoints += amount;
        if (pointsText != null) pointsText.text = "points: " + availablePoints;
    }
}

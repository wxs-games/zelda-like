using UnityEngine;
using System.Collections.Generic;

public class SkillEffectApplier : MonoBehaviour
{
    public PlayerCombat combat;

    private void OnEnable()
    {
        SkillSlot.OnAbilityPointSpent += HandleAbilityPointSpent;
    }
    private void OnDisable()
    {
        SkillSlot.OnAbilityPointSpent -= HandleAbilityPointSpent;
    }
    private void HandleAbilityPointSpent(SkillSlot slot)
    {
        ApplySlotEffect(slot);
    }

    /// <summary>
    /// 施加单个技能槽的**单级**效果。抽成公开方法是为了让调用方能显式驱动效果，
    /// 而不必依赖「花技能点」事件的隐式触发。
    /// </summary>
    public void ApplySlotEffect(SkillSlot slot)
    {
        if (slot == null || slot.skillSO == null) return;

        string skillName = slot.skillSO.skillName;
        switch (skillName)
        {
            case "Max Health Boost":
                StatsManager.Instance.UpdateMaxHealth(1);
                break;
            // 技能树只负责强化已获得的能力（伤害+1/级）；
            // 挥剑能力本身只能通过拾取剑道具解锁，不能再用技能点绕过探索
            case "Sword Damage+":
                StatsManager.Instance.playerDamage += 1;
                if (combat != null && combat.statsUI != null) combat.statsUI.UpdateDamage();
                break;

            default:
                Debug.LogWarning("Unknown skill: " + skillName);
                break;
        }
    }
}

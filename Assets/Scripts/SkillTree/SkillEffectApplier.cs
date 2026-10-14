using UnityEngine;

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
                combat.statsUI.UpdateDamage();
                break;

            default:
                Debug.LogWarning("Unknown skill: " + skillName);
                break;
        }
    }
}

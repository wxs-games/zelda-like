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
            case "Sword Slash":
                combat.enabled = true;
                break;

            default:
                Debug.LogWarning("Unknown skill: " + skillName);
                break;
        }
    }
}

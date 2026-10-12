using UnityEngine;

public class ItemEffectApplier : MonoBehaviour
{
    public void ApplyItemEffects(ItemSO item)
    {
        if (item.currentHealth > 0)
        {
            StatsManager.Instance.UpdateHealth(item.currentHealth);
        }
        if (item.maxHealth > 0)
        {
            StatsManager.Instance.UpdateMaxHealth(item.maxHealth);
        }
        if (item.speed > 0)
        {
            StatsManager.Instance.UpdateSpeed(item.speed);
        }
        if (item.duration > 0)
        {
            StartCoroutine(EffectTimer(item, item.duration));
        }
    }

    private System.Collections.IEnumerator EffectTimer(ItemSO item, float duration)
    {
        yield return new UnityEngine.WaitForSeconds(duration);
        if (item.currentHealth > 0)
        {
            StatsManager.Instance.UpdateHealth(-item.currentHealth);
        }
        if (item.maxHealth > 0)
        {
            StatsManager.Instance.UpdateMaxHealth(-item.maxHealth);
        }
        if (item.speed > 0)
        {
            StatsManager.Instance.UpdateSpeed(-item.speed);
        }
    }
}

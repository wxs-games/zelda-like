using UnityEngine;

public class ItemEffectApplier : MonoBehaviour
{
    private PlayerController playerController;

    private void Awake()
    {
        playerController = FindObjectOfType<PlayerController>();
    }

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

        // 能力解锁是永久的，不走 EffectTimer 的临时 buff 回滚路径
        ApplyAbilityUnlock(item);
    }

    private void ApplyAbilityUnlock(ItemSO item)
    {
        switch (item.unlocksAbility)
        {
            case Ability.Sword:
                StatsManager.Instance.hasSword = true;
                break;
            case Ability.Bow:
                StatsManager.Instance.hasBow = true;
                break;
            case Ability.None:
            default:
                return;
        }

        // 通知玩家控制器按新解锁的能力启用对应战斗组件
        if (playerController != null)
        {
            playerController.RefreshUnlockedAbilities();
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

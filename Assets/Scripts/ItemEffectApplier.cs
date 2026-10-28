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
        // 【顺序很重要】先把上限抬上去，再回血。
        // 原实现是「先 currentHealth 再 maxHealth」，回血会被旧上限夹断——
        // 心之容器加一颗心并回满时，玩家只能拿到旧上限的血量。
        if (item.maxHealth > 0)
        {
            StatsManager.Instance.UpdateMaxHealth(item.maxHealth);
        }
        if (item.currentHealth > 0)
        {
            StatsManager.Instance.UpdateHealth(item.currentHealth);
        }
        if (item.healsToFull)
        {
            var stats = StatsManager.Instance;
            stats.UpdateHealth(Mathf.RoundToInt(stats.playerMaxHealth - stats.playerCurrentHealth));
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
            case Ability.Shield:
                StatsManager.Instance.hasShield = true;
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

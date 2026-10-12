using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    public int expReward = 3;
    public delegate void MonsterDefeated(int exp);
    public static event MonsterDefeated OnMonsterDefeated;
    private void Start()
    {
        StatsManager.Instance.enemyCurrentHealth = StatsManager.Instance.enemyMaxHealth;
    }
    public void ChangeHealth(int amount)
    {
        StatsManager.Instance.enemyCurrentHealth += amount;
        if (StatsManager.Instance.enemyCurrentHealth > StatsManager.Instance.enemyMaxHealth)
        {
            StatsManager.Instance.enemyCurrentHealth = StatsManager.Instance.enemyMaxHealth;
        }
        else if (StatsManager.Instance.enemyCurrentHealth <= 0)
        {
            Destroy(gameObject);
            OnMonsterDefeated(expReward);
        }
    }
}

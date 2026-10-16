using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [Tooltip("敌人类型标识（全小写英文，如 torch/slime），供任务系统击杀目标匹配")]
    public string enemyId = "enemy";

    public int expReward = 3;
    public delegate void MonsterDefeated(string enemyId, int exp);
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
            OnMonsterDefeated(enemyId, expReward);
        }
    }
}

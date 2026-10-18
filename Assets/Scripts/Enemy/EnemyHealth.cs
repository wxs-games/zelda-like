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
            // 死亡音效盖过受击音效：最后一击只播死亡，避免两声叠在一起发糊
            SoundManager.Instance?.PlayEnemyDie();

            Destroy(gameObject);
            OnMonsterDefeated(enemyId, expReward);
        }
        else
        {
            // 未致死才算受击
            SoundManager.Instance?.PlayEnemyHit();
        }
    }
}

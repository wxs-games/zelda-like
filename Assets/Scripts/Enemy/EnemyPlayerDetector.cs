using UnityEngine;

public class EnemyPlayerDetector : MonoBehaviour
{
    public EnemyController enemyController;

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (collision.gameObject.tag == "Player" && enemyController.canChase)
        {
            if (enemyController.player == null)
            {
                enemyController.player = collision.transform;
            }

            enemyController.ChangeState(EnemyState.Chasing);
        }
    }
    private void OnTriggerExit2D(Collider2D collision)
    {

        if (collision.gameObject.tag == "Player")
        {
            // 敌人被销毁的同帧仍可能补发退出事件，判空避免 MissingReferenceException
            if (enemyController != null && enemyController.rb != null)
            {
                enemyController.rb.velocity = Vector2.zero;
            }

            if (enemyController != null)
            {
                // 配了路点就回巡视，否则回站桩
                enemyController.ChangeState(
                    enemyController.patrolPoints != null && enemyController.patrolPoints.Length >= 2
                        ? EnemyState.Patrolling
                        : EnemyState.Idle);
            }
        }

    }

}

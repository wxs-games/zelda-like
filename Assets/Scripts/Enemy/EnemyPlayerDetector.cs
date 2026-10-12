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
            enemyController.rb.velocity = Vector2.zero;

            enemyController.ChangeState(EnemyState.Idle);
        }

    }

}

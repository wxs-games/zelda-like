using System.Collections;
using UnityEngine;

public class EnemyMovement : MonoBehaviour
{

    public float speed;
    public int facingDirection = 1;
    public EnemyController co;
    [UnityEngine.Serialization.FormerlySerializedAs("enemy_Knockback")]
    public EnemyKnockbackable enemyKnockbackable;

    void Flip()
    {
        facingDirection *= -1;
        transform.localScale = new Vector3(transform.localScale.x * -1, transform.localScale.y, transform.localScale.z);
    }


    public void Chase()
    {
        if (enemyKnockbackable.isKnockback == false)
        {
            if (co.player.position.x > transform.position.x && facingDirection == -1
                    || co.player.position.x < transform.position.x && facingDirection == 1)
            {
                Flip();
            }

            Vector2 direction = (co.player.position - transform.position).normalized;
            co.rb.velocity = direction * speed;
        }

    }


}
public enum EnemyState
{
    Idle,
    Chasing,
    Attacking,

}

using UnityEngine;

public class PlayerKnockbackable : MonoBehaviour
{
    public Rigidbody2D playerrb;

    public void KnockBack(Transform enemy, float force)
    {
        Vector2 direction = (transform.position - enemy.position).normalized;
        playerrb.velocity = direction * force;
    }
}

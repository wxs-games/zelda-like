using System.Collections;
using UnityEngine;

public class EnemyKnockbackable : MonoBehaviour
{
    private Rigidbody2D rb;
    public bool isKnockback = false;
    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }
    public void Knockback(Transform forceTransform, float playerKnockbackForce, float stunDuration)
    {
        isKnockback = true;
        Vector2 direction = (transform.position - forceTransform.position).normalized;
        rb.velocity = direction * playerKnockbackForce;
        StartCoroutine(KnockbackCoolDown(stunDuration));
    }
    public IEnumerator KnockbackCoolDown(float duration)
    {
        yield return new WaitForSeconds(duration);
        isKnockback = false;
    }
}

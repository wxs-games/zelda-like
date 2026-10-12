using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using UnityEngine;

public class Arrow : MonoBehaviour
{
    public int damage;
    public LayerMask enemyLayer;
    public Rigidbody2D rb;
    public Vector2 direction = Vector2.right;
    public float lifeSpawn = 2;
    public float speed;
    public float knockbackForce;
    public float stunTime;
    public LayerMask obstacleLayer;
    public Sprite buriedSprite;
    public SpriteRenderer sr;
    void Start()
    {
        rb.velocity = direction * speed;
        RotateArrow();
        Destroy(gameObject, lifeSpawn);


    }

    private void RotateArrow()
    {
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(new Vector3(0, 0, angle));
    }
    public void OnCollisionEnter2D(Collision2D collision)
    {
        if ((enemyLayer.value & (1 << collision.gameObject.layer)) > 0)
        {
            collision.gameObject.GetComponent<EnemyHealth>().ChangeHealth(-damage);
            collision.gameObject.GetComponent<EnemyKnockbackable>().Knockback(transform, knockbackForce, stunTime);
            AttachToTarget(collision.gameObject.transform);
        }
        else if ((obstacleLayer.value & (1 << collision.gameObject.layer)) > 0)
        {
            AttachToTarget(collision.gameObject.transform);
        }
    }
        private void AttachToTarget(Transform target)
        {
        sr.sprite = buriedSprite;
        rb.velocity = Vector2.zero;
        rb.isKinematic = true;
        transform.SetParent(target);
        }
   
}

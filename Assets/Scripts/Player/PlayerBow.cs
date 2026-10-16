using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using UnityEngine;

public class PlayerBow : MonoBehaviour
{
    public Transform launchPoint;
    public GameObject arrowPrefab;
    public Vector2 aimDirection=Vector2.right;
    public float shootCoolDown = .5f;
    [Header("自动索敌")]
    public float detectRange = 6f;
    public LayerMask enemyLayer;
    private float shootTimer;
    public Animator anim;
    private PlayerController controller;

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
    }

    void Update()
    {
        shootTimer -= Time.deltaTime;

        HandleAiming();
    }
    private void OnEnable()
    {
        anim.SetLayerWeight(0,0);
        anim.SetLayerWeight(1,1);
    }
    private void OnDisable()
    {
        anim.SetLayerWeight(1,0);
        anim.SetLayerWeight(0,1);
    }
    private void HandleAiming()
    {
        // 射击动画播放期间锁定瞄准方向，避免方向键覆盖自动索敌的目标方向
        if (anim.GetBool("isShooting")) return;

        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        if (horizontal!=0||vertical!=0)
        {
            aimDirection = new Vector2(horizontal, vertical).normalized;
            anim.SetFloat("aimX",aimDirection.x);
            anim.SetFloat("aimY", aimDirection.y);
        }
    }

    /// <summary>
    /// 攻击范围内是否存在敌人（用于决定是否进入射击状态）。
    /// </summary>
    public bool HasEnemyInRange()
    {
        return FindNearestEnemy() != null;
    }

    private Collider2D FindNearestEnemy()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, detectRange, enemyLayer);
        Collider2D nearest = null;
        float nearestSqr = float.MaxValue;
        foreach (Collider2D hit in hits)
        {
            float sqr = ((Vector2)hit.transform.position - (Vector2)transform.position).sqrMagnitude;
            if (sqr < nearestSqr)
            {
                nearestSqr = sqr;
                nearest = hit;
            }
        }
        return nearest;
    }

    public bool CanShoot()
    {
        return shootTimer <= 0;
    }

    public void BeginShoot()
    {
        // 射击前重新索敌：范围内有敌人就朝其方向射，否则保持玩家手动瞄准方向
        Collider2D target = FindNearestEnemy();
        if (target != null)
        {
            aimDirection = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
            anim.SetFloat("aimX", aimDirection.x);
            anim.SetFloat("aimY", aimDirection.y);
        }
        anim.SetBool("isShooting", true);
    }

    public void Shoot()
    {
        if (shootTimer > 0) return;
        Arrow arrow=Instantiate(arrowPrefab, launchPoint.position, Quaternion.identity).GetComponent<Arrow>();

        // 生成时忽略箭与玩家自身的碰撞：发射点固定在身体一侧，
        // 自动索敌朝身体另一侧（如面向右、敌人在左上方）射出时箭会穿过玩家碰撞体被弹偏
        Collider2D arrowCollider = arrow.GetComponent<Collider2D>();
        Collider2D playerCollider = GetComponent<Collider2D>();
        if (arrowCollider != null && playerCollider != null)
        {
            Physics2D.IgnoreCollision(arrowCollider, playerCollider);
        }

        arrow.direction = aimDirection;
        shootTimer = shootCoolDown;
        anim.SetBool("isShooting",false);
        if (controller != null)
        {
            controller.ChangeState(PlayerIdleState.Instance);
        }
    }
    
}

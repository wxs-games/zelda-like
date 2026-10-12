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
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        if (horizontal!=0||vertical!=0)
        {
            aimDirection = new Vector2(horizontal, vertical).normalized;
            anim.SetFloat("aimX",aimDirection.x);
            anim.SetFloat("aimY", aimDirection.y);
        }
    }
    public bool CanShoot()
    {
        return shootTimer <= 0;
    }

    public void BeginShoot()
    {
        anim.SetBool("isShooting", true);
    }

    public void Shoot()
    {
        if (shootTimer > 0) return;
        Arrow arrow=Instantiate(arrowPrefab, launchPoint.position, Quaternion.identity).GetComponent<Arrow>();
        arrow.direction = aimDirection;
        shootTimer = shootCoolDown;
        anim.SetBool("isShooting",false);
        if (controller != null)
        {
            controller.ChangeState(PlayerIdleState.Instance);
        }
    }
    
}

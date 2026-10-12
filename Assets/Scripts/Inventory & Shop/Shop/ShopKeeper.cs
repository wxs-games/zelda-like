using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShopKeeper : MonoBehaviour
{
    public static ShopKeeper currentShopKeeper;

    public Animator anim;
    public ShopManager shopManager;
    public CanvasGroup shopCanvasGroup;

    [SerializeField] private List<ShopItems> shopItems;
    [SerializeField] private List<ShopItems> shopWeapons;
    [SerializeField] private List<ShopItems> shopArmour;

    [SerializeField] private Camera shopkeeperCam;
    [SerializeField] private Vector3 cameraOffset = new Vector3(0, 0, -1);


    public static event Action<ShopManager, bool> OnShopStateChanged;
    private bool playerInRange;
    private bool isShopOpen;

    void Update()
    {
        if (playerInRange)
        {
            if (Input.GetButtonDown("Interact"))
            {
                if (!isShopOpen)
                {
                    currentShopKeeper = this;
                    Time.timeScale = 0;
                    isShopOpen = true;
                    OnShopStateChanged?.Invoke(shopManager, true);
                    shopCanvasGroup.alpha = 1;
                    shopCanvasGroup.blocksRaycasts = true;
                    shopCanvasGroup.interactable = true;
                    OpenItemShop();
                }
                else
                {
                    currentShopKeeper = null;
                    Time.timeScale = 1;
                    isShopOpen = false;
                    OnShopStateChanged?.Invoke(shopManager, false);
                    shopCanvasGroup.alpha = 0;
                    shopCanvasGroup.blocksRaycasts = false;
                    shopCanvasGroup.interactable = false;
                }
            }
        }
    }
    public void OpenItemShop()
    {
        shopManager.PopulateShopItems(shopItems);
    }
    public void OpenWeaponShop()
    {
        shopManager.PopulateShopItems(shopWeapons);

    }
    public void OpenArmourShop()
    {
        shopManager.PopulateShopItems(shopArmour);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            anim.SetBool("playerInRange", true);
            playerInRange= true;
        }
    }
    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            anim.SetBool("playerInRange", false);
            playerInRange = false;

            // 玩家离开时若商店仍处于打开状态（绕过了再次按 F 的正常关闭流程），
            // 必须复位面板与时间缩放，否则游戏会永久停在 timeScale=0
            if (isShopOpen)
            {
                isShopOpen = false;
                Time.timeScale = 1;
                if (currentShopKeeper == this) currentShopKeeper = null;
                OnShopStateChanged?.Invoke(shopManager, false);
                shopCanvasGroup.alpha = 0;
                shopCanvasGroup.blocksRaycasts = false;
                shopCanvasGroup.interactable = false;
            }
        }
    }
}

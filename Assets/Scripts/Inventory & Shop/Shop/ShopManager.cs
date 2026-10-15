using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShopManager : MonoBehaviour
{
    

  
    [SerializeField] private ShopSlot[] shopSlots;
    [SerializeField] private InventoryManager inventoryManager;

    private void Awake()
    {
        // 商店面板留在村庄场景内，背包数据随 GameRoot 持久存活。
        // 序列化引用在切场景回来后指向重复 GameRoot 里的副本（Awake 停用、帧末销毁），
        // 不能在这里解析（重复体此刻可能还没停用）；统一延迟到 Start 取存活单例。
    }

    private void Start()
    {
        inventoryManager = InventoryManager.Instance;
    }

    public void TryBuyItem(ItemSO itemSO, int price)
    {
        if (itemSO != null&&inventoryManager.gold>=price)

        {
            if (HasSpaceForItem(itemSO))
            {
                inventoryManager.gold -= price;
                inventoryManager.goldText.text = inventoryManager.gold.ToString();
                inventoryManager.AddItem(itemSO,1);
            }
        }
    }
    private bool HasSpaceForItem(ItemSO itemSO)
    {
        foreach (var slot in inventoryManager.itemSlots)
        {
            if (slot.itemSO == itemSO&&slot.quantity<itemSO.stackSize)
            {
                return true;
            }
            else if (slot.itemSO == null)
            {
                return true;
            }
            
        }
        return false;
    }
    public void SellItem(ItemSO itemSO)
    {
        if (itemSO == null)
        {
            return;
        }
        foreach (var slot in shopSlots)
        {
            if (slot.itemSO == itemSO)
            {
                inventoryManager.gold += slot.price/2;
                inventoryManager.goldText.text = inventoryManager.gold.ToString();
                return;
            }
        }
    }

    public void PopulateShopItems(List<ShopItems> shopItems)
    {
        for (int i = 0; i < shopItems.Count&&i<shopSlots.Length; i++)
        {
            ShopItems shopItem = shopItems[i];
            shopSlots[i].Initialize(shopItem.itemSO, shopItem.price);
            shopSlots[i].gameObject.SetActive(true);
        }
        for (int i = shopItems.Count; i < shopSlots.Length; i++)
        {
            shopSlots[i].gameObject.SetActive(false);
        }
    }

}
[System.Serializable]
public class ShopItems
{
    public ItemSO itemSO;
    public int price;

}
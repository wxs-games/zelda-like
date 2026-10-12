using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShopManager : MonoBehaviour
{
    

  
    [SerializeField] private ShopSlot[] shopSlots;
    [SerializeField] private InventoryManager inventoryManager;


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
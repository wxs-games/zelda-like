using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class InventorySlot : MonoBehaviour,IPointerClickHandler
{
    public ItemSO itemSO;
    public int quantity;

    public Image itemImage;
    public TMP_Text quantityText;

    private InventoryManager inventoryManager;
    private static ShopManager activeShop;


    public void Start()
    {
        inventoryManager = GetComponentInParent<InventoryManager>();
    }
    private void OnEnable()
    {
        ShopKeeper.OnShopStateChanged += HandleShopStateChanged;
    }
    private void OnDisable()
    {
        ShopKeeper.OnShopStateChanged -= HandleShopStateChanged;
    }
    private void HandleShopStateChanged(ShopManager shopManager,bool isOpen)

    {
        activeShop = isOpen ? shopManager : null;
    }
    public void OnPointerClick(PointerEventData eventData)
    {
        if (quantity > 0)
        {
             if (eventData.button == PointerEventData.InputButton.Left)
            {
                if (activeShop != null)
                {
                    activeShop.SellItem(itemSO);
                    quantity--;
                    UpdateUI();
                }
                else
                    inventoryManager.UseItem(this);
            }
            else if (eventData.button == PointerEventData.InputButton.Right)
            {
                inventoryManager.DropItem(this);
            }
        }
    }


    public void UpdateUI()
    {
        if (quantity <= 0)
        {
            itemSO = null;
        }
        if (itemSO != null)
        {
            itemImage.gameObject.SetActive(true);
            itemImage.sprite = itemSO.icon;
            itemImage.color = new Color(1f, 1f, 1f, 1f);
            quantityText.text = quantity.ToString();
        }
        else
        {
            itemImage.gameObject.SetActive(false);
            quantityText.text = "";
        }
    }
}

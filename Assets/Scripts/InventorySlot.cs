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
                    // 能力道具为永久持有，不可出售
                    if (itemSO.unlocksAbility != Ability.None) return;
                    activeShop.SellItem(itemSO);
                    quantity--;
                    UpdateUI();
                }
                else
                {
                    // 能力道具获得即永久生效，无需在背包中手动"使用"
                    if (itemSO.unlocksAbility != Ability.None) return;
                    inventoryManager.UseItem(this);
                }
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
            // 能力道具唯一持有，不显示堆叠数量（对齐塞尔达道具栏）
            quantityText.text = itemSO.unlocksAbility != Ability.None ? "" : quantity.ToString();
        }
        else
        {
            itemImage.gameObject.SetActive(false);
            quantityText.text = "";
        }
    }
}

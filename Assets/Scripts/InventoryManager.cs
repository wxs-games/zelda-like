using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using static UnityEditor.Timeline.Actions.MenuPriority;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;
    public InventorySlot[] itemSlots;
    [UnityEngine.Serialization.FormerlySerializedAs("useItem")]
    public ItemEffectApplier itemEffectApplier;
    public int gold;
    public TMP_Text goldText;
    public GameObject lootPrefab;
    public Transform player;
    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else if (Instance != this)
            Destroy(this);
        goldText.text = gold.ToString();
    }
    private void OnEnable()
    {
        Loot.OnItemLooted += AddItem;
    }
    private void OnDisable()
    {
        Loot.OnItemLooted -= AddItem;
    }
    private void DropLoot(ItemSO itemSO,int quantity, bool canPickup = true)
    {
        Loot loot = Instantiate(lootPrefab, player.position, Quaternion.identity).GetComponent<Loot>();
        loot.Initialize(itemSO, quantity, canPickup);
    }
    public void DropItem(InventorySlot slot)
    {
        if (slot.itemSO == null || slot.quantity <= 0)
            return;
        DropLoot(slot.itemSO, 1, false);
        slot.quantity--;
        if (slot.quantity <= 0)
        {
            slot.itemSO = null;
        }
        slot.UpdateUI();
    }
    public bool CanAddItem(ItemSO itemSO)
    {
        if (itemSO.isGold)
            return true;
        bool hasSlot = false;
        foreach (var slot in itemSlots)
        {
            if (slot.itemSO == itemSO)
            {
                hasSlot = true;
                if (slot.quantity < itemSO.stackSize)
                    return true;
            }
        }
        if (hasSlot)
            return false;
        foreach (var slot in itemSlots)
        {
            if (slot.itemSO == null)
                return true;
        }
        return false;
    }
    public void AddItem(ItemSO itemSO,int quantity)
    {
        if (itemSO.isGold)
        {
            gold += quantity;
            goldText.text = gold.ToString();
            return;
        }
        
        foreach (var slot in itemSlots)
        {
            if (slot.itemSO == itemSO && slot.quantity < itemSO.stackSize)
            {
                int availableSpace = itemSO.stackSize - slot.quantity;
                int amountToAdd = Mathf.Min(availableSpace, quantity);
                slot.quantity += amountToAdd;
                quantity -=amountToAdd;
                
                slot.UpdateUI();
                if (quantity <= 0)
                {
                    return;
                }
                DropLoot( itemSO,quantity);
                return;

            }
        }
           
        
                foreach (var slot in itemSlots)
                {
                    if (slot.itemSO == null)
                    {
                        int amountToAdd = Mathf.Min(itemSO.stackSize, quantity);
                        slot.itemSO = itemSO;
                        slot.quantity = amountToAdd;
                        quantity -= amountToAdd;
                        slot.UpdateUI();
                        if (quantity <= 0)
                            return;
                        DropLoot(itemSO, quantity);
                        return;
                    }

                }
        
            
            
            
        
    }
    public void UseItem(InventorySlot slot)
    {
        if (slot.itemSO != null && slot.quantity >= 0)
        {
            itemEffectApplier.ApplyItemEffects(slot.itemSO);

            slot.quantity--;
            if (slot.quantity <= 0)
            {
                slot.itemSO = null;
            }
            slot.UpdateUI();

        }
    }
}

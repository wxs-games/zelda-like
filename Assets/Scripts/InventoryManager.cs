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

        // 能力道具（剑/弓）永久持有，禁止丢弃——对齐塞尔达道具栏行为
        if (slot.itemSO.unlocksAbility != Ability.None)
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
    /// <summary>增加金币并刷新 UI。任务奖励等系统统一走这里。</summary>
    public void AddGold(int amount)
    {
        if (amount <= 0) return;
        gold += amount;
        if (goldText != null) goldText.text = gold.ToString();
    }
    public void AddItem(ItemSO itemSO,int quantity)
    {
        if (itemSO.isGold)
        {
            AddGold(quantity);
            return;
        }

        // 塞尔达式设计：能力道具碰到即解锁，无需再手动使用
        if (itemSO.unlocksAbility != Ability.None)
        {
            itemEffectApplier.ApplyItemEffects(itemSO);
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

            // 能力道具（剑/弓）永久持有，使用不消耗数量——对齐塞尔达正版的道具栏行为
            if (slot.itemSO.unlocksAbility != Ability.None)
            {
                return;
            }

            slot.quantity--;
            if (slot.quantity <= 0)
            {
                slot.itemSO = null;
            }
            slot.UpdateUI();

        }
    }
}

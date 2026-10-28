using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

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
        // 永久升级道具（心之容器）碰到就用掉，跟背包格子无关，背包满也应能捡
        if (itemSO.applyOnPickup)
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
        // 金币音放在这里而非各处调用点：拾取、任务交付、商店结算都走这一条路径，
        // 只要真的加了钱就该有反馈声，不会漏也不会重复
        SoundManager.Instance?.PlayCoin();
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

        // 永久升级道具（心之容器）：碰到立刻加血上限并回满，用完即消失，不进背包。
        // 为什么必须单独一个分支：心之容器的 unlocksAbility 是 None，走不到上面的
        // 能力解锁路径，原先会被当作普通可堆叠道具塞进背包——捡了却不加血。
        if (itemSO.applyOnPickup)
        {
            itemEffectApplier.ApplyItemEffects(itemSO);
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

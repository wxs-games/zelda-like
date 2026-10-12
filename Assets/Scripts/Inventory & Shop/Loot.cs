using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Loot : MonoBehaviour
{
    public ItemSO itemSO;
    public SpriteRenderer sr;
    public Animator anim;
    public static event Action<ItemSO, int> OnItemLooted;

    public int quantity;
    private bool canPickup = true;
    private bool playerInside = false;


    private void OnValidate()
    {
        if (itemSO == null)
            return;

        UpdateAppearance();

    }
    private void UpdateAppearance()
    {
        sr.sprite = itemSO.icon;
        this.name = itemSO.itemName;
    }
    public void Initialize(ItemSO itemSO,int quantity, bool canPickup = true)
    {
        this.itemSO = itemSO;
        this.quantity = quantity;
        this.canPickup = canPickup;
        if (!canPickup)
            playerInside = true;
        UpdateAppearance();
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            if (!canPickup)
                return;
            if (!InventoryManager.Instance.CanAddItem(itemSO))
                return;
            anim.Play("LootPickup");
            OnItemLooted?.Invoke(itemSO,quantity);
            Destroy(gameObject,.5f);
        }
    }
    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            playerInside = false;
            canPickup = true;
        }
    }
    private void Update()
    {
        if (!canPickup && !playerInside)
        {
            canPickup = true;
        }
    }

}

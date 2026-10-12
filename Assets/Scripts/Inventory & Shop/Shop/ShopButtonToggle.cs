using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShopButtonToggle : MonoBehaviour
{
    public void OpenItemShop()
    {
        if (ShopKeeper.currentShopKeeper != null)
        {
            ShopKeeper.currentShopKeeper.OpenItemShop();
        }
    }
    public void OpenWeaponsShop()
    {
        if (ShopKeeper.currentShopKeeper != null)
        {
            ShopKeeper.currentShopKeeper.OpenWeaponShop();
        }
    }
    public void OpenArmourShop()
    {
        if (ShopKeeper.currentShopKeeper != null)
        {
            ShopKeeper.currentShopKeeper.OpenArmourShop();
        }
    }
}

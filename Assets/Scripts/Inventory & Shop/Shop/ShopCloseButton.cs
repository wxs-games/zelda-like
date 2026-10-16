using UnityEngine;

/// <summary>
/// 商店关闭按钮：点击关闭当前打开的商店。
/// 动态绑定 ShopKeeper.currentShopKeeper，兼容多个商人共享同一 ShopCanvas 的情况
/// （序列化引用只能绑死一个实例，会关错对象的状态）。
/// </summary>
public class ShopCloseButton : MonoBehaviour
{
    public void CloseCurrentShop()
    {
        if (ShopKeeper.currentShopKeeper != null)
            ShopKeeper.currentShopKeeper.CloseShop();
    }
}

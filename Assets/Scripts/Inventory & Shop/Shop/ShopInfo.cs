using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ShopInfo : MonoBehaviour
{
    public CanvasGroup infoPanel;

    public TMP_Text itemNameText;
    public TMP_Text itemDescriptionText;

    [Header ("Stat Fields")]
    public TMP_Text[] statTexts;

    private RectTransform infoPanelRect;

    private void Awake()
    {
        infoPanelRect=GetComponent<RectTransform>();
    }
    public void ShowItemInfo(ItemSO itemSO)
    {
        infoPanel.alpha = 1;
        itemNameText.text = itemSO.itemName;
        itemDescriptionText.text = itemSO.itemDescription;

        List<string> stats = new List<string>();
        if (itemSO.currentHealth > 0)
            stats.Add("Health:" + itemSO.currentHealth.ToString());
        if (itemSO.damage > 0)
            stats.Add("Damage:" + itemSO.damage.ToString());
        if(itemSO.speed>0)
            stats.Add("Speed:"+itemSO.speed.ToString());
        if(itemSO.duration>0)
            stats.Add("Duration:"+itemSO.duration.ToString());

        if (stats.Count <= 0)
            return;

        for (int i = 0; i < statTexts.Length; i++)
        {
            if (i < stats.Count)
            {
                statTexts[i].text = stats[i];
                statTexts[i].gameObject.SetActive(true);
            }
            else
                statTexts[i].gameObject.SetActive(false);
        }

    }
    public void HideItemInfo()
    {
        infoPanel.alpha = 0;

        itemNameText.text = "";

    }
    /// <summary>
    /// 让物品信息面板跟随指针。
    /// 触屏与鼠标统一走 Input System 的 Pointer 设备——工程 Active Input Handling 是 New，
    /// 旧 Input.mousePosition 虽在部分版本仍可用，但与 Touchscreen 的坐标不同源，
    /// 真机上会出现「触摸后信息面板不跟随」。优先取触摸，其次取鼠标。
    /// </summary>
    public void FollowMouse()
    {
        Vector3 pointer = LastPointerPosition();
        if (pointer == Vector3.zero) return;

        Vector3 offset = new Vector3(10, -10, 0);
        infoPanelRect.position = pointer + offset;
    }

    private static Vector3 LastPointerPosition()
    {
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null)
            return mouse.position.ReadValue();

        var touchscreen = UnityEngine.InputSystem.Touchscreen.current;
        if (touchscreen != null)
        {
            var primary = touchscreen.primaryTouch;
            if (primary.press.isPressed)
                return primary.position.ReadValue();
        }
        return Vector3.zero;
    }
}

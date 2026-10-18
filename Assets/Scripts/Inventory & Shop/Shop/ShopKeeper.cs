using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 商店老板：仅负责商店面板的开关和商品列表。
/// 触发交互（靠近检测 + F 键）由 NPCInteractable 基类接管。
/// 对话 → 选"要买东西" → DialogManager 触发 actionId="openShop" → 调本类 OpenShop()。
/// </summary>
public class ShopKeeper : MonoBehaviour
{
    public static ShopKeeper currentShopKeeper;

    public Animator anim;
    public ShopManager shopManager;
    public CanvasGroup shopCanvasGroup;

    [SerializeField] private List<ShopItems> shopItems;
    [SerializeField] private List<ShopItems> shopWeapons;
    [SerializeField] private List<ShopItems> shopArmour;

    [SerializeField] private Camera shopkeeperCam;
    [SerializeField] private Vector3 cameraOffset = new Vector3(0, 0, -1);

    public static event Action<ShopManager, bool> OnShopStateChanged;
    private bool isShopOpen;

    // NPCInteractable 基类已经有 OnTriggerEnter/Exit，但 ShopKeeper 自己
    // 也想知道玩家在不在（anim 参数），保留一份简单逻辑。
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player") && anim != null)
            anim.SetBool("playerInRange", true);
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            if (anim != null) anim.SetBool("playerInRange", false);
            // 玩家离开时强制关闭商店（防止卡住）
            if (isShopOpen) CloseShop();
        }
    }

    /// <summary>启动时向 DialogManager 注册"开商店"动作。</summary>
    void Start()
    {
        DialogManager.RegisterAction("openShop", OpenShop);
        DialogManager.RegisterAction("closeShop", CloseShop);
    }

    void Update()
    {
        if (!isShopOpen) return;

        // Esc 全局关店：timeScale=0 时玩家无法走动触发 OnTriggerExit，
        // 必须提供按键关闭路径（F 关店由 NPCInteractable 处理，这里补 Esc）。
        if (GameInput.CancelPressed)
        {
            CloseShop();
            return;
        }

        // 手机兜底：真机没有键盘，Esc 不可用。
        // 商店打开时点面板外的任意位置即关闭。
        // 用 Input System 的 Touchscreen 而不是旧 Input API——工程已切 New 模式，
        // 旧 Input.touchCount 在该模式下每帧抛 InvalidOperationException。
        if (IsTouchOutsideShopPanel())
        {
            CloseShop();
        }
    }

    /// <summary>是否有触点落在商店面板外部（面板外部才触发兜底关闭）。</summary>
    private bool IsTouchOutsideShopPanel()
    {
        if (shopCanvasGroup == null) return false;

        var panelTransform = shopCanvasGroup.transform as RectTransform;
        if (panelTransform == null) return false;

        var touchscreen = UnityEngine.InputSystem.Touchscreen.current;
        if (touchscreen == null) return false;

        foreach (var touch in touchscreen.touches)
        {
            // phase / position 都是 Control（不是值），必须 ReadValue() 取实际值
            var phase = touch.phase.ReadValue();
            if (phase != UnityEngine.InputSystem.TouchPhase.Began
                && phase != UnityEngine.InputSystem.TouchPhase.Ended
                && phase != UnityEngine.InputSystem.TouchPhase.Stationary)
                continue;

            Vector2 pos = touch.position.ReadValue();
            if (!RectTransformUtility.RectangleContainsScreenPoint(panelTransform, pos))
                return true;   // 面板外被按下 → 返回 true
        }
        return false;
    }

    /// <summary>由 DialogManager 在玩家点"要买东西"时触发。</summary>
    public void OpenShop()
    {
        currentShopKeeper = this;
        Time.timeScale = 0;
        isShopOpen = true;
        OnShopStateChanged?.Invoke(shopManager, true);
        shopCanvasGroup.alpha = 1;
        shopCanvasGroup.blocksRaycasts = true;
        shopCanvasGroup.interactable = true;
        OpenItemShop();
    }

    /// <summary>商店关闭：可由玩家按 F、点关闭按钮、或玩家离开触发。</summary>
    public void CloseShop()
    {
        currentShopKeeper = null;
        Time.timeScale = 1;
        isShopOpen = false;
        OnShopStateChanged?.Invoke(shopManager, false);
        shopCanvasGroup.alpha = 0;
        shopCanvasGroup.blocksRaycasts = false;
        shopCanvasGroup.interactable = false;
    }

    public void OpenItemShop()
    {
        shopManager.PopulateShopItems(shopItems);
    }

    public void OpenWeaponShop()
    {
        shopManager.PopulateShopItems(shopWeapons);
    }

    public void OpenArmourShop()
    {
        shopManager.PopulateShopItems(shopArmour);
    }
}

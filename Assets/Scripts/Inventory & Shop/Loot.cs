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

    // 地面逐帧动画状态（精灵图集帧序列；没有配 worldFrames 的道具仍用 icon 单图）
    private Sprite[] worldFrames;
    private float worldFrameRate = 8f;
    private int frameIndex;
    private float frameTimer;
    private bool pickedUp;

    // 地面掉落物的最大世界尺寸（长边）。不同来源的图标像素/PPU 差异很大
    // （例如 AI 生成的 1024px 图默认会显示成 10 个世界单位），统一归一化避免尺寸失控
    private const float MaxWorldSize = 0.8f;
    private const float BaseColliderRadius = 0.5f;

    private void Start()
    {
        NormalizeWorldSize();
    }

    private void OnValidate()
    {
        if (itemSO == null)
            return;

        UpdateAppearance();

    }
    private void UpdateAppearance()
    {
        // 地面优先播放精灵图集帧动画；普通道具没有配帧序列时回退到背包 icon 单图
        worldFrames = itemSO.worldFrames != null && itemSO.worldFrames.Length > 0
            ? itemSO.worldFrames : null;
        worldFrameRate = itemSO.worldFrameRate;
        frameIndex = 0;
        frameTimer = 0f;
        sr.sprite = worldFrames != null ? worldFrames[0] : itemSO.icon;
        // 层级面板用英文内部名（Loot_sword / Loot_bow ...）；中文 itemName 只用于 UI 显示。
        // id 未配置时回退到资产文件名（也是英文），保证层级永远不出现中文物体名。
        string id = string.IsNullOrWhiteSpace(itemSO.id) ? itemSO.name : itemSO.id;
        this.name = "Loot_" + id;
        NormalizeWorldSize();
    }

    // 过大的图标按比例缩小根物体；正常尺寸的素材保持 1:1。幂等，可重复调用。
    // 缩放放根物体而非 Sprite 子物体——子物体的 localScale 由 Idle 呼吸动画每帧写入。
    private void NormalizeWorldSize()
    {
        if (sr == null || sr.sprite == null) return;

        transform.localScale = Vector3.one;
        // 像素扫描只用于图集切割帧（图集已开启 Read/Write）；
        // 普通道具的 icon 纹理不可读，直接用精灵包围盒，避免抛 GetPixels 异常
        float spriteSize = worldFrames != null
            ? GetSpriteContentSize(sr.sprite)
            : Mathf.Max(sr.sprite.bounds.size.x, sr.sprite.bounds.size.y);
        float factor = spriteSize > MaxWorldSize ? MaxWorldSize / spriteSize : 1f;
        transform.localScale = Vector3.one * factor;

        // 补偿碰撞器：根物体缩小后保持世界中的拾取半径不变
        var circle = GetComponent<CircleCollider2D>();
        if (circle != null)
            circle.radius = BaseColliderRadius / factor;
    }

    // 计算精灵"实际内容"的世界尺寸（不透明像素包围盒的长边）。
    // 图集切割帧四周带有透明留白，直接用 sprite.bounds 会把留白算进去导致武器显示偏小。
    private float GetSpriteContentSize(Sprite sp)
    {
        try
        {
            var tex = sp.texture;
            Rect r = sp.textureRect;
            int ox = (int)r.x, oy = (int)r.y, rw = (int)r.width, rh = (int)r.height;
            Color[] colors = tex.GetPixels(ox, oy, rw, rh);
            int minX = rw, minY = rh, maxX = -1, maxY = -1;
            for (int y = 0; y < rh; y++)
            {
                int row = y * rw;
                for (int x = 0; x < rw; x++)
                {
                    if (colors[row + x].a > 0.1f)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }
            if (maxX < 0) return Mathf.Max(sp.bounds.size.x, sp.bounds.size.y);
            float worldW = (maxX - minX + 1) / sp.pixelsPerUnit;
            float worldH = (maxY - minY + 1) / sp.pixelsPerUnit;
            return Mathf.Max(worldW, worldH);
        }
        catch (UnityException)
        {
            // 纹理未开启 Read/Write 时的兜底
            return Mathf.Max(sp.bounds.size.x, sp.bounds.size.y);
        }
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
            pickedUp = true;   // 停止逐帧切换，Sprite 停在当前帧由 LootPickup 动画上浮淡出
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

        TickWorldFrames();
    }

    // 按配置帧率循环切换精灵帧。位移/呼吸由 Animator 的 LootIdle 曲线负责，两者叠加。
    private void TickWorldFrames()
    {
        if (pickedUp || worldFrames == null || worldFrames.Length == 0)
            return;

        frameTimer += Time.deltaTime;
        float interval = 1f / Mathf.Max(1f, worldFrameRate);
        while (frameTimer >= interval)
        {
            frameTimer -= interval;
            frameIndex = (frameIndex + 1) % worldFrames.Length;
            sr.sprite = worldFrames[frameIndex];
        }
    }

}

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 塞尔达风格红心血条。
///
/// 【为什么程序化生成贴图】
/// 工程里没有像素红心素材（Layer Lab 那个是扁平图标，风格不搭）。这里直接在运行时
/// 生成三态像素心（满 / 半 / 空），走 Point 过滤，放大后仍是硬边像素块，和 2D 像素画风一致；
/// 也不需要额外的 .meta 导入配置，避免手写导入参数出错。
///
/// 【血量换算】
/// 心形按「半颗」为最小单位：hpPerHalfHeart 默认 10 点。
///   100 血 / 10 = 10 个半颗 = 5 颗心
/// 敌人单次伤害也应为 10（EnemyController.damageAmount），才能做到「一次受击掉半颗心」。
/// 上限或当前值变化（技能树加血上限、药水回血）时自动重算心数，Update 里只做数值比较，
/// 没有变化就直接 return，开销可以忽略。
/// </summary>
public class HeartHealthUI : MonoBehaviour
{
    [Header("血量换算")]
    [Tooltip("每半颗心对应多少点生命值。100 血 / 10 = 5 颗心；敌人单次伤害应与此值一致")]
    public float hpPerHalfHeart = 10f;

    [Header("外观")]
    [Tooltip("每颗心的显示宽度（参考分辨率像素）")]
    public int heartWidth = 48;

    [Tooltip("相邻两颗心之间的间距（像素）")]
    public int heartSpacing = 8;

    [Tooltip("血条相对锚点（左上角）的偏移")]
    public Vector2 anchoredOffset = new Vector2(24f, -18f);

    // 16 x 11 像素心形母版：'.'=透明  'X'=描边  'R'=红色填充  'W'=高光
    private static readonly string[] HeartPattern =
    {
        "...XXX....XXX...",
        "..XRRRX..XRRRX..",
        ".XWRRRRXXRRRRWX.",
        ".XRRRRRRRRRRRRX.",
        ".XRRRRRRRRRRRRX.",
        "..XRRRRRRRRRRX..",
        "...XRRRRRRRRX...",
        "....XRRRRRRX....",
        ".....XRRRRX.....",
        "......XRRX......",
        ".......XX.......",
    };

    private static readonly Color32 ColorOutline = new Color32(58, 26, 20, 255);   // 深棕描边
    private static readonly Color32 ColorRed = new Color32(226, 35, 32, 255);      // 心红
    private static readonly Color32 ColorHighlight = new Color32(255, 255, 255, 255);
    private static readonly Color32 ColorEmpty = new Color32(70, 56, 52, 255);     // 空心填充
    private static readonly Color32 ColorClear = new Color32(0, 0, 0, 0);

    // 每个逻辑像素放大 3 倍烘焙：16 x 11 的母版 → 48 x 33 贴图，
    // 与默认 heartWidth=48 正好 1:1，Point 过滤下像素块大小均匀、不发虚。
    private const int BakeScale = 3;

    private RectTransform _row;
    private Image[] _hearts;
    private int _lastHalves = int.MinValue;
    private int _lastMaxHalves = int.MinValue;

    private static Sprite _spriteFull, _spriteHalf, _spriteEmpty;

    private void Start()
    {
        Refresh(true);
    }

    private void Update()
    {
        // 只在数值真的变化时才重绘（受伤、回血、加血上限都会走到这里）
        Refresh(false);
    }

    /// <summary>按当前血量刷新心形。force=true 时忽略缓存强制重绘。</summary>
    public void Refresh(bool force)
    {
        var stats = StatsManager.GetActive();
        if (stats == null) return;

        float perHalf = Mathf.Max(0.01f, hpPerHalfHeart);
        int maxHalves = Mathf.Max(1, Mathf.RoundToInt(stats.playerMaxHealth / perHalf));
        int halves = Mathf.Clamp(
            Mathf.RoundToInt(stats.playerCurrentHealth / perHalf), 0, maxHalves);

        if (!force && halves == _lastHalves && maxHalves == _lastMaxHalves) return;
        _lastHalves = halves;
        _lastMaxHalves = maxHalves;

        EnsureSprites();
        EnsureHearts(Mathf.CeilToInt(maxHalves / 2f));

        for (int i = 0; i < _hearts.Length; i++)
        {
            int heartHalves = halves - i * 2;
            Sprite s = heartHalves >= 2 ? _spriteFull
                     : heartHalves == 1 ? _spriteHalf
                     : _spriteEmpty;
            if (_hearts[i].sprite != s) _hearts[i].sprite = s;
        }
    }

    /// <summary>按需要创建/复用一行红心 Image。</summary>
    private void EnsureHearts(int count)
    {
        int heartHeight = Mathf.Max(1, Mathf.RoundToInt(heartWidth * (float)HeartPattern.Length / HeartPattern[0].Length));

        if (_row == null)
        {
            var rowGo = new GameObject("Hearts Row", typeof(RectTransform));
            rowGo.transform.SetParent(transform, false);
            _row = (RectTransform)rowGo.transform;
            _row.anchorMin = new Vector2(0f, 1f);
            _row.anchorMax = new Vector2(0f, 1f);
            _row.pivot = new Vector2(0f, 1f);
            _row.anchoredPosition = anchoredOffset;
            _row.sizeDelta = new Vector2(
                count * heartWidth + Mathf.Max(0, count - 1) * heartSpacing,
                heartHeight);
        }

        if (_hearts != null && _hearts.Length == count) return;

        // 心数变化（加血上限）：整行重建
        if (_hearts != null)
        {
            foreach (var old in _hearts)
                if (old != null) Destroy(old.gameObject);
        }

        _hearts = new Image[count];
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Heart " + (i + 1), typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_row, false);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(heartWidth, heartHeight);
            rt.anchoredPosition = new Vector2(i * (heartWidth + heartSpacing), 0f);

            var img = go.GetComponent<Image>();
            img.raycastTarget = false;   // 纯显示，别挡触屏
            img.preserveAspect = true;
            _hearts[i] = img;
        }

        _row.sizeDelta = new Vector2(
            count * heartWidth + Mathf.Max(0, count - 1) * heartSpacing,
            heartHeight);
    }

    private static void EnsureSprites()
    {
        if (_spriteFull != null && _spriteHalf != null && _spriteEmpty != null) return;
        _spriteFull = BuildSprite(HeartFill.Full);
        _spriteHalf = BuildSprite(HeartFill.Half);
        _spriteEmpty = BuildSprite(HeartFill.Empty);
    }

    private enum HeartFill { Full, Half, Empty }

    /// <summary>把字符母版烘焙成一张 Point 过滤的像素贴图。</summary>
    private static Sprite BuildSprite(HeartFill fill)
    {
        int cols = HeartPattern[0].Length;
        int rows = HeartPattern.Length;
        int w = cols * BakeScale;
        int h = rows * BakeScale;

        var pixels = new Color32[w * h];

        for (int y = 0; y < h; y++)
        {
            // Texture2D 原点在左下，母版按从上往下的行定义，这里翻转
            int patternRow = rows - 1 - (y / BakeScale);

            for (int x = 0; x < w; x++)
            {
                int patternCol = x / BakeScale;
                char c = HeartPattern[patternRow][patternCol];
                pixels[y * w + x] = ResolveColor(c, patternCol, cols, fill);
            }
        }

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            name = "Heart_" + fill,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        tex.SetPixels32(pixels);
        tex.Apply(false, true);   // 上传后不留 CPU 副本

        var sprite = Sprite.Create(
            tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect);
        sprite.name = "Heart_" + fill;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    /// <summary>半颗心按中线切：左半红、右半空；空心整颗都是暗色填充。</summary>
    private static Color32 ResolveColor(char c, int col, int cols, HeartFill fill)
    {
        bool leftSide = col < cols / 2;

        switch (c)
        {
            case 'X':
                return ColorOutline;

            case 'W':
                // 高光只出现在有血的一侧，空心/半心的空侧不留白点
                return IsFilledSide(fill, leftSide) ? ColorHighlight : ColorEmpty;

            case 'R':
                return IsFilledSide(fill, leftSide) ? ColorRed : ColorEmpty;

            default:
                return ColorClear;
        }
    }

    private static bool IsFilledSide(HeartFill fill, bool leftSide)
    {
        switch (fill)
        {
            case HeartFill.Full: return true;
            case HeartFill.Half: return leftSide;
            default: return false;
        }
    }
}

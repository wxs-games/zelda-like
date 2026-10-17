using UnityEngine;

/// <summary>
/// 把 Screen.safeArea 应用到自身 RectTransform 的锚点，
/// 让触屏控件自动避开刘海/圆角屏区域。分辨率或安全区变化时自动重算。
/// </summary>
public class SafeAreaFitter : MonoBehaviour
{
    [Tooltip("仅编辑器生效：用模拟安全区预览刘海内缩效果（真机自动使用真实安全区）")]
    public bool simulateInEditor;

    [Tooltip("模拟安全区（0~1 归一化矩形：x/y/width/height），仅编辑器生效")]
    public Rect simulatedSafeArea = new Rect(0f, 0.07f, 1f, 0.86f);

    private RectTransform _rt;
    private Rect _lastSafeArea = new Rect(-1f, -1f, -1f, -1f);
    private int _lastWidth, _lastHeight;

    void Awake()
    {
        _rt = (RectTransform)transform;
        Apply();
    }

    void Update()
    {
        if (_lastSafeArea != CurrentSafeArea() ||
            _lastWidth != Screen.width || _lastHeight != Screen.height)
        {
            Apply();
        }
    }

    private Rect CurrentSafeArea()
    {
#if UNITY_EDITOR
        if (simulateInEditor)
        {
            float w = Screen.width, h = Screen.height;
            return new Rect(
                simulatedSafeArea.x * w,
                simulatedSafeArea.y * h,
                simulatedSafeArea.width * w,
                simulatedSafeArea.height * h);
        }
#endif
        return Screen.safeArea;
    }

    private void Apply()
    {
        if (_rt == null) return;

        Rect sa = CurrentSafeArea();
        _lastSafeArea = sa;
        _lastWidth = Screen.width;
        _lastHeight = Screen.height;

        Vector2 min = sa.position;
        Vector2 max = sa.position + sa.size;
        min.x /= Screen.width;
        min.y /= Screen.height;
        max.x /= Screen.width;
        max.y /= Screen.height;

        _rt.anchorMin = min;
        _rt.anchorMax = max;
        _rt.offsetMin = Vector2.zero;
        _rt.offsetMax = Vector2.zero;
    }
}

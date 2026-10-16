using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 底部对话框：织梦岛/旷野之息风格（黑条 + 头像 + 名字 + 文字 + 选项按钮）。
/// 由 DialogManager 驱动，通过 RenderNode 刷新内容。
/// </summary>
public class DialogBox : MonoBehaviour
{
    [Header("组件引用")]
    [Tooltip("说话者名字文本")]
    public Text nameText;
    [Tooltip("头像图片")]
    public Image portraitImage;
    [Tooltip("台词正文文本")]
    public Text bodyText;
    [Tooltip("选项按钮的父物体（VerticalLayoutGroup 或 ContentSizeFitter 子物体）")]
    public Transform optionContainer;
    [Tooltip("选项按钮模板（Prefab），或直接用场景上的 Button")]
    public GameObject optionButtonTemplate;

    [Header("提示")]
    [Tooltip("无选项时显示在右下角的\"按 F 继续\"提示")]
    public Text continueHint;

    [Header("颜色/样式")]
    public Color hiddenColor = new Color(0, 0, 0, 0);
    public Color visibleColor = new Color(0, 0, 0, 0.9f);

    private CanvasGroup _cg;
    private readonly List<GameObject> _activeOptionButtons = new List<GameObject>();

    void Awake()
    {
        _cg = GetComponent<CanvasGroup>();
        if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();
        SetupOptionLayout();
        HideDialog();
    }

    /// <summary>
    /// 修复选项按钮被排到屏幕外的问题：原来 Options 容器高度为 0，
    /// VerticalLayoutGroup 把按钮排到对话框下方（世界坐标 y 为负，超出屏幕底边），
    /// 玩家看不到也点不到按钮。这里把容器固定到对话框右侧、从底部向上排布
    /// （JRPG 标准选项列表位），与左侧正文互不遮挡。
    /// </summary>
    private void SetupOptionLayout()
    {
        if (optionContainer == null) return;

        var rt = optionContainer as RectTransform;
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            // 对话框宽 1240，右侧留出 320 放选项列，正文显示在左侧
            rt.anchoredPosition = new Vector2(900f, 8f);
            // 高度容纳 5 个选项（5*40 + 4*2 = 208 ≤ 框高 220）
            rt.sizeDelta = new Vector2(320f, 210f);
        }

        var vlg = optionContainer.GetComponent<VerticalLayoutGroup>();
        if (vlg != null)
        {
            vlg.spacing = 2;
            vlg.childAlignment = TextAnchor.LowerLeft;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = false;
            vlg.childControlWidth = false;
            vlg.childControlHeight = false;
        }
    }

    public void ShowDialog()
    {
        _cg.alpha = 1;
        _cg.blocksRaycasts = true;
        _cg.interactable = true;
    }

    public void HideDialog()
    {
        _cg.alpha = 0;
        _cg.blocksRaycasts = false;
        _cg.interactable = false;
        // 直接清 optionContainer（最保险，不依赖 ClearOptions 的调用时机）
        if (optionContainer != null)
        {
            for (int i = optionContainer.childCount - 1; i >= 0; i--)
                DestroyImmediate(optionContainer.GetChild(i).gameObject);
        }
        _activeOptionButtons.Clear();
    }

    /// <summary>由 DialogManager.GoToNode 调用，渲染节点到 UI 上。</summary>
    public void RenderNode(DialogNodeSO node, List<DialogOption> visibleOptions)
    {
        // 名字
        if (nameText != null)
        {
            if (string.IsNullOrEmpty(node.speakerName))
            {
                nameText.gameObject.SetActive(false);
            }
            else
            {
                nameText.gameObject.SetActive(true);
                nameText.text = node.speakerName;
            }
        }

        // 头像
        if (portraitImage != null)
        {
            if (node.speakerPortrait == null)
            {
                portraitImage.gameObject.SetActive(false);
            }
            else
            {
                portraitImage.gameObject.SetActive(true);
                portraitImage.sprite = node.speakerPortrait;
            }
        }

        // 正文
        if (bodyText != null)
        {
            bodyText.gameObject.SetActive(true);
            bodyText.text = node.text ?? "";
        }

        // 选项
        ClearOptions();
        if (visibleOptions != null && visibleOptions.Count > 0)
        {
            if (continueHint != null) continueHint.gameObject.SetActive(false);
            SpawnOptions(visibleOptions);
        }
        else
        {
            // 无选项 → 显示继续提示
            if (continueHint != null) continueHint.gameObject.SetActive(true);
        }
    }

    private void ClearOptions()
    {
        // 先手动清 optionContainer 所有子物体（最保险）
        if (optionContainer != null)
        {
            for (int i = optionContainer.childCount - 1; i >= 0; i--)
                DestroyImmediate(optionContainer.GetChild(i).gameObject);
        }
        _activeOptionButtons.Clear();
    }

    private void SpawnOptions(List<DialogOption> options)
    {
        // 如果没提供模板，动态生成简单按钮
        GameObject template = optionButtonTemplate;
        if (template == null && optionContainer != null)
        {
            template = CreateDefaultOptionTemplate();
        }
        if (template == null || optionContainer == null) return;

        for (int i = 0; i < options.Count; i++)
        {
            var opt = options[i];
            var btnGO = Instantiate(template, optionContainer);
            btnGO.name = $"Option_{i}_{opt.label}";
            btnGO.SetActive(true);

            // 找 Text 组件（子物体）
            var txt = btnGO.GetComponentInChildren<Text>();
            if (txt != null) txt.text = opt.label;

            // 绑定点击回调：SelectOption(i)
            var button = btnGO.GetComponent<Button>();
            if (button != null)
            {
                int idx = i; // 捕获循环变量
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => DialogManager.Instance.SelectOption(idx));
            }

            _activeOptionButtons.Add(btnGO);
        }
    }

    private GameObject CreateDefaultOptionTemplate()
    {
        // 运行时生成一个简单的按钮（Image + Text）
        var go = new GameObject("OptionTemplate", typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(280, 40);
        var img = go.GetComponent<Image>();
        img.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);

        var txtGO = new GameObject("Label", typeof(RectTransform), typeof(Text));
        txtGO.transform.SetParent(go.transform, false);
        var txtRT = txtGO.GetComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = Vector2.zero;
        txtRT.offsetMax = Vector2.zero;
        var txt = txtGO.GetComponent<Text>();
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;
        txt.fontSize = 24;
        // 用 Arial.ttf，Unity 自带
        txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        go.SetActive(false); // 模板隐藏
        return go;
    }
}

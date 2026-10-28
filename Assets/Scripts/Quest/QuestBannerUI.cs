using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 任务预告横幅：接取「新任务」/达成「任务完成」/交付「任务交付」时在屏幕顶部弹出提示。
/// 订阅 QuestManager.OnQuestChanged，与 QuestLogUI 同事件源但不拦截输入、不暂停游戏。
/// 动画用 Unscaled 时间驱动——接取发生在对话中（timeScale=0），横幅照样能播完。
/// UI 结构由本组件启动时动态生成，只需把组件挂到 GameRoot 即可。
/// </summary>
public class QuestBannerUI : MonoBehaviour
{
    private const float FadeIn = 0.2f;
    private const float HoldTime = 2.2f;
    private const float FadeOut = 0.4f;

    // 每个任务上次见过的状态：Start 时初始化为当前值，避免启动瞬间弹历史状态
    private readonly Dictionary<string, QuestState> _lastSeen = new Dictionary<string, QuestState>();

    // 预告队列：连续状态变化时排队逐条显示
    private readonly Queue<(string title, string banner, Color color)> _queue = new Queue<(string, string, Color)>();

    private CanvasGroup _cg;
    private Text _titleText;
    private Text _bannerText;
    private Image _bg;

    private float _state;      // 0=淡入 1=停留 2=淡出 3=空闲
    private float _timer;

    void Start()
    {
        BuildUI();
        if (QuestManager.Instance != null)
        {
            foreach (var q in QuestManager.Instance.quests)
            {
                if (q != null && !_lastSeen.ContainsKey(q.questId))
                    _lastSeen[q.questId] = QuestManager.Instance.GetState(q.questId);
            }
        }
        SetAlpha(0f);
    }

    void OnEnable() => QuestManager.OnQuestChanged += HandleQuestChanged;
    void OnDisable() => QuestManager.OnQuestChanged -= HandleQuestChanged;

    void Update()
    {
        if (_state >= 3)
        {
            // 空闲：有排队的预告就开播
            if (_queue.Count > 0) { _state = 0; _timer = 0f; PlayNext(); }
            return;
        }

        _timer += Time.unscaledDeltaTime;
        switch (_state)
        {
            case 0: // 淡入
                SetAlpha(Mathf.Clamp01(_timer / FadeIn));
                if (_timer >= FadeIn) { _state = 1; _timer = 0f; }
                break;
            case 1: // 停留
                if (_timer >= HoldTime) { _state = 2; _timer = 0f; }
                break;
            case 2: // 淡出
                SetAlpha(1f - Mathf.Clamp01(_timer / FadeOut));
                if (_timer >= FadeOut)
                {
                    _state = 3;
                    SetAlpha(0f);
                    // 清空文本：非预告期画面上不残留任何字
                    _titleText.text = "";
                    _bannerText.text = "";
                }
                break;
        }
    }

    private void HandleQuestChanged(QuestSO quest)
    {
        if (quest == null) return;

        QuestState prev;
        if (!_lastSeen.TryGetValue(quest.questId, out prev)) prev = QuestState.NotAccepted;
        QuestState cur = QuestManager.Instance.GetState(quest.questId);
        _lastSeen[quest.questId] = cur;
        if (cur == prev) return;

        switch (cur)
        {
            case QuestState.InProgress:
                _queue.Enqueue(("新任务", quest.title, new Color(0.55f, 0.85f, 1f)));
                break;
            case QuestState.Completed:
                _queue.Enqueue(("任务完成", quest.title, new Color(1f, 0.85f, 0.3f)));
                break;
            case QuestState.Delivered:
                string reward = "";
                if (quest.rewardGold > 0) reward += $"  金币 +{quest.rewardGold}";
                _queue.Enqueue(("任务交付", quest.title + reward, new Color(0.55f, 1f, 0.65f)));
                break;
        }
    }

    private void PlayNext()
    {
        var (title, banner, color) = _queue.Dequeue();
        _titleText.text = title;
        _bannerText.text = banner;
        _titleText.color = color;
        _bg.color = new Color(0.08f, 0.08f, 0.1f, 0.92f);
    }

    private void SetAlpha(float a)
    {
        if (_cg != null) _cg.alpha = a;
    }

    // ========== UI 生成 ==========

    private void BuildUI()
    {
        // 顶层 Canvas（Overlay，排在日志面板之上）
        var canvasGO = new GameObject("QuestBannerCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60; // 高于 DialogCanvas(10)，预告永远压在最上层
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // 横幅底板：屏幕左上角——顶部中央是背包格子、右上是金币面板，两处都不能压
        var bannerGO = new GameObject("Banner", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        bannerGO.transform.SetParent(canvasGO.transform, false);
        var rt = (RectTransform)bannerGO.transform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(24f, -12f);
        rt.sizeDelta = new Vector2(660f, 92f);
        _bg = bannerGO.GetComponent<Image>();
        _bg.color = new Color(0.08f, 0.08f, 0.1f, 0.92f);
        _cg = bannerGO.GetComponent<CanvasGroup>();
        _cg.alpha = 0f; // 创建即隐藏，避免启动首帧闪出深色空条

        // 标题（「新任务」等）：横幅上半区
        var titleGO = new GameObject("Title", typeof(RectTransform), typeof(Text));
        titleGO.transform.SetParent(bannerGO.transform, false);
        Place(titleGO, new Vector2(0f, 0f), new Vector2(1f, 1f), new VectorOffset(16, 52, 16, 6));
        _titleText = titleGO.GetComponent<Text>();
        _titleText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        _titleText.fontSize = 24;
        _titleText.fontStyle = FontStyle.Bold;
        _titleText.alignment = TextAnchor.MiddleLeft;

        // 任务名/奖励行：横幅下半区
        var nameGO = new GameObject("QuestName", typeof(RectTransform), typeof(Text));
        nameGO.transform.SetParent(bannerGO.transform, false);
        Place(nameGO, new Vector2(0f, 0f), new Vector2(1f, 1f), new VectorOffset(16, 8, 16, 46));
        _bannerText = nameGO.GetComponent<Text>();
        _bannerText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        _bannerText.fontSize = 30;
        _bannerText.fontStyle = FontStyle.Bold;
        _bannerText.alignment = TextAnchor.MiddleLeft;
        _bannerText.color = Color.white;
        _bannerText.horizontalOverflow = HorizontalWrapMode.Overflow;
    }

    private struct VectorOffset
    {
        public float left, bottom, right, top;
        public VectorOffset(float l, float b, float r, float t) { left = l; bottom = b; right = r; top = t; }
    }

    private static void Place(GameObject go, Vector2 aMin, Vector2 aMax, VectorOffset off)
    {
        var rt = (RectTransform)go.transform;
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.offsetMin = new Vector2(off.left, off.bottom);
        rt.offsetMax = new Vector2(-off.right, -off.top);
    }
}

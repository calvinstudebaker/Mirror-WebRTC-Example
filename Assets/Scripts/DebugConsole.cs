using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DebugConsole : MonoBehaviour
{
    [SerializeField] int maxLines = 50;

    Text _text;
    ScrollRect _scrollRect;
    readonly List<string> _lines = new List<string>();

    void Awake()
    {
        BuildUI();
        Application.logMessageReceived += OnLogMessage;
    }

    void OnDestroy()
    {
        Application.logMessageReceived -= OnLogMessage;
    }

    void OnLogMessage(string message, string stackTrace, LogType type)
    {
        string prefix = type switch
        {
            LogType.Error => "<color=red>[ERR] ",
            LogType.Exception => "<color=red>[EXC] ",
            LogType.Warning => "<color=yellow>[WRN] ",
            _ => "<color=white>"
        };

        _lines.Add(prefix + message + "</color>");

        while (_lines.Count > maxLines)
            _lines.RemoveAt(0);

        _text.text = string.Join("\n", _lines);

        Canvas.ForceUpdateCanvases();
        _scrollRect.verticalNormalizedPosition = 0f;
    }

    void BuildUI()
    {
        // Canvas
        var canvasGo = new GameObject("DebugConsoleCanvas");
        canvasGo.transform.SetParent(transform);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.AddComponent<GraphicRaycaster>();

        // Panel (bottom third of screen)
        var panelGo = new GameObject("Panel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        var panelImage = panelGo.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.8f);
        var panelRect = panelGo.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0.33f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        // Mask so content clips inside the panel
        panelGo.AddComponent<Mask>().showMaskGraphic = true;

        // ScrollRect
        _scrollRect = panelGo.AddComponent<ScrollRect>();
        _scrollRect.horizontal = false;

        // Content
        var contentGo = new GameObject("Content");
        contentGo.transform.SetParent(panelGo.transform, false);
        var contentRect = contentGo.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0f, 1f);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;
        var fitter = contentGo.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _scrollRect.content = contentRect;
        _scrollRect.viewport = panelRect;

        // Text
        _text = contentGo.AddComponent<Text>();
        _text.font = Font.CreateDynamicFontFromOSFont("Courier New", 14);
        _text.fontSize = 14;
        _text.color = Color.white;
        _text.supportRichText = true;
        _text.horizontalOverflow = HorizontalWrapMode.Wrap;
        _text.verticalOverflow = VerticalWrapMode.Overflow;
        _text.alignment = TextAnchor.LowerLeft;
        _text.raycastTarget = false;

        // Padding
        var padding = contentGo.AddComponent<LayoutElement>();
        padding.minHeight = 0;

        // Clear button (top-right of panel)
        var btnGo = new GameObject("ClearButton");
        btnGo.transform.SetParent(canvasGo.transform, false);
        var btnImage = btnGo.AddComponent<Image>();
        btnImage.color = new Color(0.3f, 0.3f, 0.3f, 0.9f);
        var btnRect = btnGo.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(1f, 0.33f);
        btnRect.anchorMax = new Vector2(1f, 0.33f);
        btnRect.pivot = new Vector2(1f, 0f);
        btnRect.anchoredPosition = new Vector2(-10f, 5f);
        btnRect.sizeDelta = new Vector2(60f, 30f);
        var btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = btnImage;
        btn.onClick.AddListener(() =>
        {
            _lines.Clear();
            _text.text = "";
        });

        var btnTextGo = new GameObject("Text");
        btnTextGo.transform.SetParent(btnGo.transform, false);
        var btnText = btnTextGo.AddComponent<Text>();
        btnText.text = "Clear";
        btnText.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
        btnText.fontSize = 14;
        btnText.color = Color.white;
        btnText.alignment = TextAnchor.MiddleCenter;
        btnText.raycastTarget = false;
        var btnTextRect = btnTextGo.GetComponent<RectTransform>();
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.offsetMin = Vector2.zero;
        btnTextRect.offsetMax = Vector2.zero;
    }
}

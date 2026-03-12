using UnityEngine;
using UnityEngine.UI;
using Mirror;

public class NetworkedCounter : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnCounterChanged))]
    int counter = 0;

    Text _counterText;
    GameObject _canvasGo;

    void Awake()
    {
        var ni = GetComponent<NetworkIdentity>();
        Debug.Log($"[NetworkedCounter] Awake — sceneId={ni.sceneId}, netId={ni.netId}, isClient={ni.isClient}, isServer={ni.isServer}");
    }

    public override void OnStartServer()
    {
        Debug.Log("[NetworkedCounter] OnStartServer fired");
    }

    public override void OnStartClient()
    {
        Debug.Log("[NetworkedCounter] OnStartClient fired");
        BuildUI();
        _counterText.text = counter.ToString();
    }

    public override void OnStopClient()
    {
        if (_canvasGo != null)
            Destroy(_canvasGo);
    }

    void OnCounterChanged(int oldVal, int newVal)
    {
        if (_counterText != null)
            _counterText.text = newVal.ToString();
    }

    [Command(requiresAuthority = false)]
    void CmdIncrement()
    {
        counter++;
    }

    [Command(requiresAuthority = false)]
    void CmdDecrement()
    {
        counter--;
    }

    void BuildUI()
    {
        // Canvas
        _canvasGo = new GameObject("CounterCanvas");
        _canvasGo.transform.SetParent(transform);
        var canvas = _canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        _canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        _canvasGo.AddComponent<GraphicRaycaster>();

        // Panel (top-center strip)
        var panelGo = new GameObject("Panel");
        panelGo.transform.SetParent(_canvasGo.transform, false);
        var panelImage = panelGo.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.7f);
        var panelRect = panelGo.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(-10f, -10f);
        panelRect.sizeDelta = new Vector2(240f, 60f);

        // HorizontalLayoutGroup for tidy arrangement
        var hlg = panelGo.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 10f;
        hlg.padding = new RectOffset(10, 10, 5, 5);
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        // "-" button
        CreateButton(panelGo.transform, "-", () => CmdDecrement());

        // Counter text
        var textGo = new GameObject("CounterText");
        textGo.transform.SetParent(panelGo.transform, false);
        _counterText = textGo.AddComponent<Text>();
        _counterText.text = "0";
        _counterText.font = Font.CreateDynamicFontFromOSFont("Arial", 28);
        _counterText.fontSize = 28;
        _counterText.color = Color.white;
        _counterText.alignment = TextAnchor.MiddleCenter;
        _counterText.raycastTarget = false;
        var textLayout = textGo.AddComponent<LayoutElement>();
        textLayout.minWidth = 60f;
        textLayout.preferredWidth = 60f;

        // "+" button
        CreateButton(panelGo.transform, "+", () => CmdIncrement());
    }

    void CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        var btnGo = new GameObject("Button_" + label);
        btnGo.transform.SetParent(parent, false);
        var btnImage = btnGo.AddComponent<Image>();
        btnImage.color = new Color(0.3f, 0.3f, 0.3f, 0.9f);
        var btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = btnImage;
        btn.onClick.AddListener(onClick);
        var btnLayout = btnGo.AddComponent<LayoutElement>();
        btnLayout.minWidth = 50f;
        btnLayout.minHeight = 50f;
        btnLayout.preferredWidth = 50f;
        btnLayout.preferredHeight = 50f;

        var btnTextGo = new GameObject("Text");
        btnTextGo.transform.SetParent(btnGo.transform, false);
        var btnText = btnTextGo.AddComponent<Text>();
        btnText.text = label;
        btnText.font = Font.CreateDynamicFontFromOSFont("Arial", 24);
        btnText.fontSize = 24;
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

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Screen-space HUD for the run. Shows player health, magazine ammo, and completed scenario loops,
/// scaled for portrait phones and inset into the device safe area (notch / home indicator).
/// </summary>
[DisallowMultipleComponent]
public class GameHud : MonoBehaviour
{
    [SerializeField] ScenarioPathRunner pathRunner;
    [SerializeField] PlayerHealth playerHealth;
    [SerializeField] PlayerShooting playerShooting;
    [SerializeField] string healthLabelFormat = "{0}/{1}";
    [SerializeField] string ammoLabelFormat = "{0}/{1}";
    [SerializeField] string loopLabelFormat = "Loop: {0}";

    [Header("Mobile Layout")]
    [SerializeField] Vector2 referenceResolution = new Vector2(1080f, 1920f);
    [Tooltip("0 = match width (best for portrait Fit-to-Width phones). 1 = match height.")]
    [SerializeField] [Range(0f, 1f)] float matchWidthOrHeight = 0f;
    [SerializeField] int fontSize = 44;
    [Tooltip("Inset from the safe-area edges, in reference resolution units.")]
    [SerializeField] Vector2 padding = new Vector2(48f, 56f);

    static readonly Color LabelColor = new Color(0.92f, 0.95f, 1f, 1f);

    Text _healthText;
    Text _ammoText;
    Text _loopText;
    RectTransform _safeAreaRoot;
    Rect _lastSafeArea;
    Vector2Int _lastScreenSize;

    void Awake()
    {
        if (pathRunner == null)
        {
            pathRunner = FindFirstObjectByType<ScenarioPathRunner>();
        }

        if (playerHealth == null)
        {
            playerHealth = FindFirstObjectByType<PlayerHealth>();
        }

        if (playerShooting == null)
        {
            playerShooting = FindFirstObjectByType<PlayerShooting>();
        }

        BuildUi();
        ApplySafeArea(force: true);
        RefreshHealthLabel(
            playerHealth != null ? playerHealth.CurrentHealth : 100,
            playerHealth != null ? playerHealth.MaxHealth : 100);
        RefreshLoopLabel(pathRunner != null ? pathRunner.LoopIndex : 0);
    }

    void Start()
    {
        if (playerShooting != null)
        {
            RefreshAmmoLabel(playerShooting.ShotsRemaining, playerShooting.MagazineSize);
        }
    }

    void OnEnable()
    {
        if (pathRunner != null)
        {
            pathRunner.OnLoopStarted += HandleLoopStarted;
        }

        if (playerHealth != null)
        {
            playerHealth.HealthChanged += HandleHealthChanged;
        }

        if (playerShooting != null)
        {
            playerShooting.AmmoChanged += HandleAmmoChanged;
        }
    }

    void OnDisable()
    {
        if (pathRunner != null)
        {
            pathRunner.OnLoopStarted -= HandleLoopStarted;
        }

        if (playerHealth != null)
        {
            playerHealth.HealthChanged -= HandleHealthChanged;
        }

        if (playerShooting != null)
        {
            playerShooting.AmmoChanged -= HandleAmmoChanged;
        }
    }

    void LateUpdate()
    {
        ApplySafeArea(force: false);
    }

    void HandleLoopStarted(int loopIndex)
    {
        RefreshLoopLabel(loopIndex);
    }

    void HandleHealthChanged(int current, int max)
    {
        RefreshHealthLabel(current, max);
    }

    void HandleAmmoChanged(int current, int max)
    {
        RefreshAmmoLabel(current, max);
    }

    void RefreshHealthLabel(int current, int max)
    {
        if (_healthText == null)
        {
            return;
        }

        _healthText.text = string.Format(healthLabelFormat, current, max);
    }

    void RefreshAmmoLabel(int current, int max)
    {
        if (_ammoText == null)
        {
            return;
        }

        _ammoText.text = string.Format(ammoLabelFormat, current, max);
    }

    void RefreshLoopLabel(int loopIndex)
    {
        if (_loopText == null)
        {
            return;
        }

        _loopText.text = string.Format(loopLabelFormat, Mathf.Max(0, loopIndex));
    }

    void BuildUi()
    {
        var canvasObject = new GameObject("GameHudCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        // Prefer width so tall 18:9 / 20:9 phones keep readable HUD size, like CameraFitToWidth.
        scaler.matchWidthOrHeight = matchWidthOrHeight;

        canvasObject.GetComponent<GraphicRaycaster>().enabled = false;

        var safeAreaObject = new GameObject("SafeArea", typeof(RectTransform));
        safeAreaObject.transform.SetParent(canvasObject.transform, false);
        _safeAreaRoot = safeAreaObject.GetComponent<RectTransform>();
        _safeAreaRoot.anchorMin = Vector2.zero;
        _safeAreaRoot.anchorMax = Vector2.one;
        _safeAreaRoot.offsetMin = Vector2.zero;
        _safeAreaRoot.offsetMax = Vector2.zero;

        _healthText = CreateHudLabel("HealthLabel", new Vector2(0f, 1f), TextAnchor.UpperLeft,
            new Vector2(Mathf.Abs(padding.x), -Mathf.Abs(padding.y)), "100/100");

        _ammoText = CreateHudLabel("AmmoLabel", new Vector2(0f, 0f), TextAnchor.LowerLeft,
            new Vector2(Mathf.Abs(padding.x), Mathf.Abs(padding.y)), "15/15");

        _loopText = CreateHudLabel("LoopLabel", new Vector2(1f, 0f), TextAnchor.LowerRight,
            new Vector2(-Mathf.Abs(padding.x), Mathf.Abs(padding.y)), string.Format(loopLabelFormat, 0));
    }

    Text CreateHudLabel(string objectName, Vector2 anchor, TextAnchor alignment, Vector2 anchoredPosition, string initialText)
    {
        var labelObject = new GameObject(objectName, typeof(RectTransform), typeof(Text), typeof(ContentSizeFitter));
        labelObject.transform.SetParent(_safeAreaRoot, false);

        var rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = Vector2.zero;

        var fitter = labelObject.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var text = labelObject.GetComponent<Text>();
        text.text = initialText;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = alignment;
        text.color = LabelColor;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;

        return text;
    }

    void ApplySafeArea(bool force)
    {
        if (_safeAreaRoot == null)
        {
            return;
        }

        Rect safeArea = Screen.safeArea;
        var screenSize = new Vector2Int(Screen.width, Screen.height);

        if (!force
            && safeArea.Equals(_lastSafeArea)
            && screenSize == _lastScreenSize)
        {
            return;
        }

        _lastSafeArea = safeArea;
        _lastScreenSize = screenSize;

        float width = Mathf.Max(1, screenSize.x);
        float height = Mathf.Max(1, screenSize.y);

        _safeAreaRoot.anchorMin = new Vector2(safeArea.xMin / width, safeArea.yMin / height);
        _safeAreaRoot.anchorMax = new Vector2(safeArea.xMax / width, safeArea.yMax / height);
        _safeAreaRoot.offsetMin = Vector2.zero;
        _safeAreaRoot.offsetMax = Vector2.zero;
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// After every N completed scenarios, pauses the run and lets the player pick
/// one random upgrade from the remaining pool.
/// </summary>
[DisallowMultipleComponent]
public class UpgradeOfferController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] ScenarioPathRunner pathRunner;
    [SerializeField] PlayerUpgradeController upgradeController;
    [SerializeField] PlayerMovement playerMovement;
    [SerializeField] PlayerShooting playerShooting;

    [Header("Offer")]
    [SerializeField] [Min(1)] int scenariosPerOffer = 3;
    [SerializeField] [Min(1)] int choicesPerOffer = 3;

    [Header("UI")]
    [SerializeField] Vector2 referenceResolution = new Vector2(1080f, 1920f);
    [SerializeField] [Range(0f, 1f)] float matchWidthOrHeight = 0f;

    static readonly Color OverlayColor = new Color(0.02f, 0.04f, 0.08f, 0.78f);
    static readonly Color PanelColor = new Color(0.1f, 0.14f, 0.22f, 0.96f);
    static readonly Color ButtonColor = new Color(0.14f, 0.22f, 0.34f, 1f);
    static readonly Color ButtonHighlight = new Color(0.24f, 0.4f, 0.62f, 1f);
    static readonly Color ButtonPressed = new Color(0.08f, 0.12f, 0.2f, 1f);
    static readonly Color TitleColor = new Color(0.92f, 0.95f, 1f, 1f);
    static readonly Color BodyColor = new Color(0.78f, 0.84f, 0.92f, 1f);
    static readonly Color CategoryColor = new Color(0.55f, 0.78f, 1f, 1f);

    readonly List<UpgradeDefinition> _availableBuffer = new();
    readonly List<UpgradeDefinition> _rolledBuffer = new();
    readonly List<Button> _choiceButtons = new();

    GameObject _root;
    Text _titleText;
    int _completedScenarios;
    bool _seenFirstScenario;
    bool _isOffering;
    float _timeScaleBeforePause = 1f;

    public bool IsOffering => _isOffering;

    void Awake()
    {
        ResolveReferences();
        EnsureEventSystem();
        BuildUi();
        SetOfferVisible(false);
    }

    void OnEnable()
    {
        if (pathRunner != null)
        {
            pathRunner.OnScenarioStarted += HandleScenarioStarted;
        }
    }

    void OnDisable()
    {
        if (pathRunner != null)
        {
            pathRunner.OnScenarioStarted -= HandleScenarioStarted;
        }

        if (_isOffering)
        {
            ResumeGameplay();
        }
    }

    void ResolveReferences()
    {
        if (pathRunner == null)
        {
            pathRunner = FindFirstObjectByType<ScenarioPathRunner>();
        }

        if (upgradeController == null)
        {
            // Owned by GameLoopController (or the Systems object), not this offer UI host.
            upgradeController = FindFirstObjectByType<PlayerUpgradeController>();
        }

        if (playerMovement == null)
        {
            playerMovement = FindFirstObjectByType<PlayerMovement>();
        }

        if (playerShooting == null)
        {
            playerShooting = FindFirstObjectByType<PlayerShooting>();
        }
    }

    void HandleScenarioStarted(int scenarioIndex)
    {
        if (!_seenFirstScenario)
        {
            _seenFirstScenario = true;
            return;
        }

        if (_isOffering)
        {
            return;
        }

        _completedScenarios++;
        if (_completedScenarios % Mathf.Max(1, scenariosPerOffer) != 0)
        {
            return;
        }

        BeginOffer();
    }

    void BeginOffer()
    {
        ResolveReferences();
        if (upgradeController == null)
        {
            return;
        }

        upgradeController.CollectAvailable(_availableBuffer);
        if (_availableBuffer.Count == 0)
        {
            return;
        }

        RollChoices();
        if (_rolledBuffer.Count == 0)
        {
            return;
        }

        _isOffering = true;
        PauseGameplay();
        PopulateChoiceButtons();
        SetOfferVisible(true);
    }

    void RollChoices()
    {
        _rolledBuffer.Clear();
        int take = Mathf.Min(choicesPerOffer, _availableBuffer.Count);

        for (int i = 0; i < take; i++)
        {
            int index = UnityEngine.Random.Range(0, _availableBuffer.Count);
            _rolledBuffer.Add(_availableBuffer[index]);
            _availableBuffer.RemoveAt(index);
        }
    }

    void PauseGameplay()
    {
        _timeScaleBeforePause = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;

        if (playerMovement != null)
        {
            playerMovement.enabled = false;
        }

        if (playerShooting != null)
        {
            playerShooting.enabled = false;
        }
    }

    void ResumeGameplay()
    {
        Time.timeScale = _timeScaleBeforePause > 0f ? _timeScaleBeforePause : 1f;

        if (playerMovement != null)
        {
            playerMovement.enabled = true;
        }

        if (playerShooting != null)
        {
            playerShooting.enabled = true;
        }
    }

    void ChooseUpgrade(UpgradeId id)
    {
        if (!_isOffering || upgradeController == null)
        {
            return;
        }

        upgradeController.TryApply(id);
        SetOfferVisible(false);
        _isOffering = false;
        ResumeGameplay();
    }

    void SetOfferVisible(bool visible)
    {
        if (_root != null)
        {
            _root.SetActive(visible);
        }
    }

    void PopulateChoiceButtons()
    {
        for (int i = 0; i < _choiceButtons.Count; i++)
        {
            Button button = _choiceButtons[i];
            bool active = i < _rolledBuffer.Count;
            button.gameObject.SetActive(active);
            if (!active)
            {
                continue;
            }

            UpgradeDefinition definition = _rolledBuffer[i];
            Text[] labels = button.GetComponentsInChildren<Text>(true);
            if (labels.Length >= 3)
            {
                labels[0].text = definition.CategoryLabel.ToUpperInvariant();
                labels[1].text = definition.displayName;
                labels[2].text = definition.description;
            }

            button.onClick.RemoveAllListeners();
            UpgradeId capturedId = definition.id;
            button.onClick.AddListener(() => ChooseUpgrade(capturedId));
        }

        if (_titleText != null)
        {
            _titleText.text = "Escolha uma melhoria";
        }
    }

    void BuildUi()
    {
        var canvasObject = new GameObject(
            "UpgradeOfferCanvas",
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = matchWidthOrHeight;

        _root = CreateStretchPanel(canvasObject.transform, "OfferRoot", OverlayColor);

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panel.transform.SetParent(_root.transform, false);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(920f, 0f);

        var panelImage = panel.GetComponent<Image>();
        panelImage.color = PanelColor;
        panelImage.raycastTarget = true;

        var layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(48, 48, 56, 56);
        layout.spacing = 28f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        var fitter = panel.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _titleText = CreateText(panel.transform, "Title", "Escolha uma melhoria", 56, FontStyle.Bold, TitleColor);

        int slots = Mathf.Max(1, choicesPerOffer);
        for (int i = 0; i < slots; i++)
        {
            _choiceButtons.Add(CreateChoiceButton(panel.transform, i));
        }
    }

    Button CreateChoiceButton(Transform parent, int index)
    {
        var buttonObject = new GameObject(
            $"Choice_{index}",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(VerticalLayoutGroup),
            typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);

        var image = buttonObject.GetComponent<Image>();
        image.color = ButtonColor;

        var button = buttonObject.GetComponent<Button>();
        var colors = button.colors;
        colors.normalColor = ButtonColor;
        colors.highlightedColor = ButtonHighlight;
        colors.pressedColor = ButtonPressed;
        colors.selectedColor = ButtonHighlight;
        colors.disabledColor = new Color(0.2f, 0.2f, 0.22f, 0.7f);
        button.colors = colors;
        button.targetGraphic = image;

        var layout = buttonObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(28, 28, 24, 24);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        var element = buttonObject.GetComponent<LayoutElement>();
        element.minHeight = 190f;
        element.preferredHeight = 190f;

        CreateText(buttonObject.transform, "Category", "CASCO", 28, FontStyle.Bold, CategoryColor);
        CreateText(buttonObject.transform, "Name", "Upgrade", 42, FontStyle.Bold, TitleColor);
        CreateText(buttonObject.transform, "Description", "Description", 30, FontStyle.Normal, BodyColor);

        return button;
    }

    static GameObject CreateStretchPanel(Transform parent, string name, Color color)
    {
        var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = color;
        return panel;
    }

    static Text CreateText(Transform parent, string name, string value, int size, FontStyle style, Color color)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        textObject.transform.SetParent(parent, false);

        var text = textObject.GetComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAnchor.UpperLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;

        var element = textObject.GetComponent<LayoutElement>();
        element.minHeight = size + 8;
        return text;
    }

    static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        var eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<InputSystemUIInputModule>();
    }
}

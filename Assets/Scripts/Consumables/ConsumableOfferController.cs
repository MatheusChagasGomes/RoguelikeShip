using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// At the start of the third scenario of each loop, pauses the run and offers
/// consumables rolled by rarity. Alternates with upgrades (loop start).
/// </summary>
[DisallowMultipleComponent]
public class ConsumableOfferController : MonoBehaviour
{
    const int ConsumableScenarioIndex = 2;

    [Header("References")]
    [SerializeField] ScenarioPathRunner pathRunner;
    [SerializeField] PlayerConsumableController consumableController;
    [SerializeField] UpgradeOfferController upgradeOfferController;
    [SerializeField] PlayerMovement playerMovement;
    [SerializeField] PlayerShooting playerShooting;

    [Header("Offer")]
    [SerializeField] [Min(1)] int choicesPerOffer = 3;

    [Header("Rarity Weights")]
    [SerializeField] [Min(0f)] float commonWeight = 70f;
    [SerializeField] [Min(0f)] float rareWeight = 25f;
    [SerializeField] [Min(0f)] float ultraRareWeight = 5f;

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
    static readonly Color CommonColor = new Color(0.75f, 0.8f, 0.85f, 1f);
    static readonly Color RareColor = new Color(0.45f, 0.75f, 1f, 1f);
    static readonly Color UltraRareColor = new Color(1f, 0.75f, 0.3f, 1f);

    readonly List<ConsumableDefinition> _rarityBuffer = new();
    readonly List<ConsumableDefinition> _rolledBuffer = new();
    readonly List<Button> _choiceButtons = new();
    readonly HashSet<ConsumableId> _rolledIds = new();

    GameObject _root;
    Text _titleText;
    bool _isOffering;
    bool _pendingOffer;
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
        ResolveReferences();
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

        if (consumableController == null)
        {
            consumableController = FindFirstObjectByType<PlayerConsumableController>();
        }

        if (upgradeOfferController == null)
        {
            upgradeOfferController = FindFirstObjectByType<UpgradeOfferController>();
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
        if (scenarioIndex != ConsumableScenarioIndex)
        {
            return;
        }

        RequestOffer();
    }

    /// <summary>Called by the upgrade offer UI when it finishes, so a queued consumable can open.</summary>
    public void NotifyUpgradeOfferClosed()
    {
        if (_pendingOffer)
        {
            _pendingOffer = false;
            BeginOffer();
        }
    }

    void RequestOffer()
    {
        if (_isOffering)
        {
            return;
        }

        ResolveReferences();
        if (upgradeOfferController != null && upgradeOfferController.IsOffering)
        {
            _pendingOffer = true;
            return;
        }

        BeginOffer();
    }

    void BeginOffer()
    {
        ResolveReferences();
        if (consumableController == null || _isOffering)
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
        _rolledIds.Clear();
        int take = Mathf.Max(1, choicesPerOffer);

        for (int i = 0; i < take; i++)
        {
            if (!TryRollUniqueConsumable(out ConsumableDefinition definition))
            {
                break;
            }

            _rolledBuffer.Add(definition);
            _rolledIds.Add(definition.id);
        }
    }

    bool TryRollUniqueConsumable(out ConsumableDefinition definition)
    {
        definition = null;

        for (int attempt = 0; attempt < 8; attempt++)
        {
            ConsumableRarity rarity = RollRarity();
            consumableController.CollectByRarity(rarity, _rarityBuffer);
            RemoveAlreadyRolled(_rarityBuffer);
            if (_rarityBuffer.Count == 0)
            {
                continue;
            }

            definition = _rarityBuffer[Random.Range(0, _rarityBuffer.Count)];
            return true;
        }

        // Fallback: any remaining consumable ignoring rarity weights.
        for (int rarityIndex = 0; rarityIndex < 3; rarityIndex++)
        {
            consumableController.CollectByRarity((ConsumableRarity)rarityIndex, _rarityBuffer);
            RemoveAlreadyRolled(_rarityBuffer);
            if (_rarityBuffer.Count == 0)
            {
                continue;
            }

            definition = _rarityBuffer[Random.Range(0, _rarityBuffer.Count)];
            return true;
        }

        return false;
    }

    void RemoveAlreadyRolled(List<ConsumableDefinition> buffer)
    {
        for (int i = buffer.Count - 1; i >= 0; i--)
        {
            if (_rolledIds.Contains(buffer[i].id))
            {
                buffer.RemoveAt(i);
            }
        }
    }

    ConsumableRarity RollRarity()
    {
        float total = commonWeight + rareWeight + ultraRareWeight;
        if (total <= 0f)
        {
            return ConsumableRarity.Common;
        }

        float roll = Random.Range(0f, total);
        if (roll < commonWeight)
        {
            return ConsumableRarity.Common;
        }

        roll -= commonWeight;
        if (roll < rareWeight)
        {
            return ConsumableRarity.Rare;
        }

        return ConsumableRarity.UltraRare;
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

    void ChooseConsumable(ConsumableId id)
    {
        if (!_isOffering || consumableController == null)
        {
            return;
        }

        consumableController.TryApply(id);
        SetOfferVisible(false);
        _isOffering = false;
        ResumeGameplay();
        upgradeOfferController?.NotifyConsumableOfferClosed();
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

            ConsumableDefinition definition = _rolledBuffer[i];
            Text[] labels = button.GetComponentsInChildren<Text>(true);
            if (labels.Length >= 3)
            {
                labels[0].text = definition.RarityLabel.ToUpperInvariant();
                labels[0].color = RarityColor(definition.rarity);
                labels[1].text = definition.displayName;
                labels[2].text = definition.description;
            }

            button.onClick.RemoveAllListeners();
            ConsumableId capturedId = definition.id;
            button.onClick.AddListener(() => ChooseConsumable(capturedId));
        }

        if (_titleText != null)
        {
            _titleText.text = "Escolha um consumível";
        }
    }

    static Color RarityColor(ConsumableRarity rarity)
    {
        return rarity switch
        {
            ConsumableRarity.Rare => RareColor,
            ConsumableRarity.UltraRare => UltraRareColor,
            _ => CommonColor,
        };
    }

    void BuildUi()
    {
        var canvasObject = new GameObject(
            "ConsumableOfferCanvas",
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 210;

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

        _titleText = CreateText(panel.transform, "Title", "Escolha um consumível", 56, FontStyle.Bold, TitleColor);

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

        CreateText(buttonObject.transform, "Rarity", "COMUM", 28, FontStyle.Bold, CommonColor);
        CreateText(buttonObject.transform, "Name", "Consumable", 42, FontStyle.Bold, TitleColor);
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

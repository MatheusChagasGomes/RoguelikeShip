using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Builds and drives the portrait main menu: Play, Settings, Credits, Quit.
/// </summary>
[DisallowMultipleComponent]
public class MainMenuController : MonoBehaviour
{
    [SerializeField] string gameplaySceneName = "GameplayScene";
    [SerializeField] string gameTitle = "RoguelikeShip";

    static readonly Color BackgroundColor = new Color(0.05f, 0.07f, 0.12f, 1f);
    static readonly Color ButtonColor = new Color(0.12f, 0.18f, 0.28f, 0.92f);
    static readonly Color ButtonHighlight = new Color(0.22f, 0.38f, 0.58f, 1f);
    static readonly Color ButtonPressed = new Color(0.08f, 0.12f, 0.2f, 1f);
    static readonly Color TitleColor = new Color(0.85f, 0.92f, 1f, 1f);
    static readonly Color LabelColor = new Color(0.92f, 0.95f, 1f, 1f);

    GameObject _mainPanel;
    GameObject _settingsPanel;
    GameObject _creditsPanel;

    void Awake()
    {
        // Keep Device Simulator / mobile orientation aligned with Game (+Y = up).
        Screen.orientation = ScreenOrientation.Portrait;

        EnsureEventSystem();
        BuildUi();
        ShowMain();
    }

    void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        var eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<InputSystemUIInputModule>();
    }

    void BuildUi()
    {
        var canvasObject = new GameObject("MainMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        var root = CreateStretchPanel(canvasObject.transform, "Root", BackgroundColor);

        _mainPanel = CreateCenteredColumn(root.transform, "MainPanel");
        CreateTitle(_mainPanel.transform, gameTitle);
        CreateMenuButton(_mainPanel.transform, "Jogar", Play);
        CreateMenuButton(_mainPanel.transform, "Configurações", ShowSettings);
        CreateMenuButton(_mainPanel.transform, "Créditos", ShowCredits);
        CreateMenuButton(_mainPanel.transform, "Sair do Jogo", Quit);

        _settingsPanel = CreateSubPanel(
            root.transform,
            "SettingsPanel",
            "Configurações",
            "Opções de áudio e controles em breve.");

        _creditsPanel = CreateSubPanel(
            root.transform,
            "CreditsPanel",
            "Créditos",
            "RoguelikeShip\nDesenvolvido com Unity");
    }

    GameObject CreateSubPanel(Transform parent, string name, string title, string body)
    {
        var panel = CreateCenteredColumn(parent, name);
        CreateTitle(panel.transform, title);

        var bodyObject = new GameObject("Body", typeof(RectTransform), typeof(Text));
        bodyObject.transform.SetParent(panel.transform, false);

        var bodyRect = bodyObject.GetComponent<RectTransform>();
        bodyRect.sizeDelta = new Vector2(780f, 280f);

        var bodyText = bodyObject.GetComponent<Text>();
        ConfigureText(bodyText, body, 42, TextAnchor.MiddleCenter, LabelColor);

        CreateMenuButton(panel.transform, "Voltar", ShowMain);
        return panel;
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

    static GameObject CreateCenteredColumn(Transform parent, string name)
    {
        var panel = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panel.transform.SetParent(parent, false);

        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(820f, 0f);

        var layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        layout.spacing = 28f;
        layout.padding = new RectOffset(40, 40, 40, 40);

        var fitter = panel.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        return panel;
    }

    static void CreateTitle(Transform parent, string text)
    {
        var titleObject = new GameObject("Title", typeof(RectTransform), typeof(LayoutElement), typeof(Text));
        titleObject.transform.SetParent(parent, false);

        titleObject.GetComponent<LayoutElement>().preferredHeight = 140f;

        var titleText = titleObject.GetComponent<Text>();
        ConfigureText(titleText, text, 72, TextAnchor.MiddleCenter, TitleColor);
        titleText.fontStyle = FontStyle.Bold;
    }

    void CreateMenuButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        var buttonObject = new GameObject(label + "Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);

        buttonObject.GetComponent<LayoutElement>().preferredHeight = 110f;

        var image = buttonObject.GetComponent<Image>();
        image.color = ButtonColor;

        var button = buttonObject.GetComponent<Button>();
        var colors = button.colors;
        colors.normalColor = ButtonColor;
        colors.highlightedColor = ButtonHighlight;
        colors.pressedColor = ButtonPressed;
        colors.selectedColor = ButtonHighlight;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(buttonObject.transform, false);

        var labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        ConfigureText(labelObject.GetComponent<Text>(), label, 48, TextAnchor.MiddleCenter, LabelColor);
    }

    static void ConfigureText(Text text, string value, int fontSize, TextAnchor alignment, Color color)
    {
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
    }

    void ShowMain()
    {
        SetPanelActive(_mainPanel, true);
        SetPanelActive(_settingsPanel, false);
        SetPanelActive(_creditsPanel, false);
    }

    void ShowSettings()
    {
        SetPanelActive(_mainPanel, false);
        SetPanelActive(_settingsPanel, true);
        SetPanelActive(_creditsPanel, false);
    }

    void ShowCredits()
    {
        SetPanelActive(_mainPanel, false);
        SetPanelActive(_settingsPanel, false);
        SetPanelActive(_creditsPanel, true);
    }

    static void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null)
        {
            panel.SetActive(active);
        }
    }

    void Play()
    {
        SceneManager.LoadScene(gameplaySceneName);
    }

    void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

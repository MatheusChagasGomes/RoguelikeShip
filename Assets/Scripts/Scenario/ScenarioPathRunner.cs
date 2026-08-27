using System;
using UnityEngine;

/// <summary>
/// Colored scenario screens scroll past a fixed camera/player.
/// Current screen slides toward the bottom of the game view; the next fills
/// the view behind it and is revealed from the top.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public class ScenarioPathRunner : MonoBehaviour
{
    [Header("Scenarios")]
    [SerializeField]
    ScenarioDefinition[] scenarios = CreateDefaultScenarios();

    [Header("Scroll")]
    [SerializeField]
    [Min(0.01f)]
    [Tooltip("World units per second.")]
    float scrollSpeed = 3f;

    [SerializeField]
    [Min(0f)]
    float widthPadding = 1f;

    [SerializeField]
    int sortingOrder = -100;

    [SerializeField]
    Camera worldCamera;

    [Header("Runtime")]
    [SerializeField]
    bool autoStart = true;

    [SerializeField]
    [Tooltip("When all scenarios finish, restart from the first and raise the loop index.")]
    bool loopOnComplete = true;

    SpriteRenderer _currentScreen;
    SpriteRenderer _incomingScreen;
    float _segmentTravel;
    float _currentDurationDistance;
    int _scenarioIndex;
    int _loopIndex;
    bool _isRunning;
    bool _completed;
    bool _isTransitioning;
    Sprite _whiteSprite;
    Texture2D _whiteTexture;

    public event Action<int> OnScenarioStarted;
    public event Action OnPathCompleted;
    /// <summary>Fired when a full scenario cycle begins. Arg is the 0-based loop index.</summary>
    public event Action<int> OnLoopStarted;

    public bool IsRunning => _isRunning;
    public bool IsCompleted => _completed;
    public bool LoopOnComplete => loopOnComplete;
    public int CurrentScenarioIndex => _scenarioIndex;
    public int LoopIndex => _loopIndex;
    public int ScenarioCount => scenarios != null ? scenarios.Length : 0;
    public float Progress01
    {
        get
        {
            if (scenarios == null || scenarios.Length == 0)
            {
                return 0f;
            }

            float total = 0f;
            float done = 0f;
            for (int i = 0; i < scenarios.Length; i++)
            {
                float distance = scrollSpeed * Mathf.Max(0.1f, scenarios[i].durationSeconds);
                total += distance;
                if (i < _scenarioIndex)
                {
                    done += distance;
                }
                else if (i == _scenarioIndex)
                {
                    done += Mathf.Min(_segmentTravel, distance);
                }
            }

            return total <= 0f ? 0f : Mathf.Clamp01(done / total);
        }
    }

    static ScenarioDefinition[] CreateDefaultScenarios()
    {
        return new[]
        {
            new ScenarioDefinition { displayName = "Azul", color = new Color(0.25f, 0.55f, 0.95f), durationSeconds = 8f },
            new ScenarioDefinition { displayName = "Amarelo", color = new Color(0.95f, 0.85f, 0.25f), durationSeconds = 8f },
            new ScenarioDefinition { displayName = "Roxo", color = new Color(0.65f, 0.30f, 0.90f), durationSeconds = 8f },
            new ScenarioDefinition { displayName = "Laranja", color = new Color(0.95f, 0.50f, 0.20f), durationSeconds = 8f },
            new ScenarioDefinition { displayName = "Cinza", color = new Color(0.45f, 0.45f, 0.48f), durationSeconds = 8f },
            new ScenarioDefinition { displayName = "Verde", color = new Color(0.25f, 0.75f, 0.35f), durationSeconds = 8f },
        };
    }

    void Awake()
    {
        // Keep Device Simulator / mobile orientation aligned with Game & Scene (+Y = up).
        Screen.orientation = ScreenOrientation.Portrait;

        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }

        EnsureWhiteSprite();
    }

    void Start()
    {
        CreateScreens();

        if (autoStart)
        {
            StartPath();
        }
    }

    void OnDestroy()
    {
        if (_whiteSprite != null)
        {
            Destroy(_whiteSprite);
            _whiteSprite = null;
        }

        if (_whiteTexture != null)
        {
            Destroy(_whiteTexture);
            _whiteTexture = null;
        }
    }

    void LateUpdate()
    {
        if (!_isRunning || worldCamera == null || scrollSpeed <= 0f)
        {
            return;
        }

        float delta = scrollSpeed * Time.deltaTime;
        _segmentTravel += delta;

        if (_isTransitioning && _currentScreen != null)
        {
            // Slide the old screen toward the bottom of the view. The next screen
            // is already centered behind it, so it is revealed from the top.
            _currentScreen.transform.position += Vector3.down * delta;

            if (_segmentTravel >= _currentDurationDistance)
            {
                FinishTransition();
            }
        }
        else if (_segmentTravel >= _currentDurationDistance)
        {
            BeginNextTransition();
        }
    }

    public void StartPath()
    {
        BeginLoop(_loopIndex, resetLoopIndex: true);
    }

    /// <summary>
    /// Starts (or restarts) the scenario sequence. Pass resetLoopIndex to clear the run cycle count.
    /// </summary>
    public void BeginLoop(int loopIndex, bool resetLoopIndex = false)
    {
        if (_currentScreen == null)
        {
            CreateScreens();
        }

        if (scenarios == null || scenarios.Length == 0)
        {
            scenarios = CreateDefaultScenarios();
        }

        _loopIndex = resetLoopIndex ? 0 : Mathf.Max(0, loopIndex);
        _scenarioIndex = 0;
        _completed = false;
        _isTransitioning = false;
        _segmentTravel = 0f;
        _isRunning = true;

        ApplyScreenTransform(_currentScreen, GetViewCenter(), GetScenarioColor(0));
        _currentScreen.sortingOrder = sortingOrder + 1;
        _currentScreen.enabled = true;

        if (_incomingScreen != null)
        {
            _incomingScreen.sortingOrder = sortingOrder;
            _incomingScreen.enabled = false;
        }

        _currentDurationDistance = scrollSpeed * Mathf.Max(0.1f, scenarios[0].durationSeconds);
        OnLoopStarted?.Invoke(_loopIndex);
        OnScenarioStarted?.Invoke(0);
    }

    public void StopPath()
    {
        _isRunning = false;
    }

    void BeginNextTransition()
    {
        int nextIndex = _scenarioIndex + 1;
        bool startingNewLoop = false;

        if (nextIndex >= scenarios.Length)
        {
            if (!loopOnComplete)
            {
                CompletePath();
                return;
            }

            // Same top-to-bottom reveal as any other scenario — just wrap to the first.
            OnPathCompleted?.Invoke();
            _loopIndex++;
            nextIndex = 0;
            startingNewLoop = true;
        }

        _scenarioIndex = nextIndex;
        _segmentTravel = 0f;
        _isTransitioning = true;
        _currentDurationDistance = GetViewHeight();

        ApplyScreenTransform(_incomingScreen, GetViewCenter(), GetScenarioColor(nextIndex));
        _incomingScreen.sortingOrder = sortingOrder;
        _incomingScreen.enabled = true;
        _currentScreen.sortingOrder = sortingOrder + 1;

        if (startingNewLoop)
        {
            OnLoopStarted?.Invoke(_loopIndex);
        }

        OnScenarioStarted?.Invoke(nextIndex);
    }

    void FinishTransition()
    {
        _isTransitioning = false;
        _segmentTravel = 0f;

        var previous = _currentScreen;
        _currentScreen = _incomingScreen;
        _incomingScreen = previous;

        ApplyScreenTransform(_currentScreen, GetViewCenter(), _currentScreen.color);
        _currentScreen.sortingOrder = sortingOrder + 1;
        _incomingScreen.sortingOrder = sortingOrder;
        _incomingScreen.enabled = false;

        float fullDistance = scrollSpeed * Mathf.Max(0.1f, scenarios[_scenarioIndex].durationSeconds);
        float holdDistance = Mathf.Max(0f, fullDistance - GetViewHeight());
        _currentDurationDistance = holdDistance;

        if (_currentDurationDistance <= 0f)
        {
            BeginNextTransition();
        }
    }

    void CompletePath()
    {
        if (_completed)
        {
            return;
        }

        _isRunning = false;
        _completed = true;
        _isTransitioning = false;
        OnPathCompleted?.Invoke();
    }

    void CreateScreens()
    {
        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }

        EnsureWhiteSprite();

        _currentScreen = CreateScreenObject("ScenarioCurrent");
        _incomingScreen = CreateScreenObject("ScenarioIncoming");
        _incomingScreen.enabled = false;
    }

    SpriteRenderer CreateScreenObject(string objectName)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(transform, false);

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = _whiteSprite;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    void ApplyScreenTransform(SpriteRenderer screen, Vector3 worldCenter, Color color)
    {
        if (screen == null || worldCamera == null)
        {
            return;
        }

        float width = GetViewWidth() + widthPadding * 2f;
        float height = GetViewHeight() + widthPadding;

        screen.transform.position = new Vector3(worldCenter.x, worldCenter.y, transform.position.z);
        screen.transform.localScale = new Vector3(width, height, 1f);
        screen.color = color;
    }

    Color GetScenarioColor(int index)
    {
        if (scenarios == null || index < 0 || index >= scenarios.Length)
        {
            return Color.magenta;
        }

        return scenarios[index].color;
    }

    Vector3 GetViewCenter()
    {
        float depth = GetCameraDepth();
        return worldCamera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, depth));
    }

    float GetViewHeight()
    {
        return worldCamera.orthographicSize * 2f;
    }

    float GetViewWidth()
    {
        return GetViewHeight() * worldCamera.aspect;
    }

    float GetCameraDepth()
    {
        return Mathf.Abs(worldCamera.transform.position.z - transform.position.z);
    }

    void EnsureWhiteSprite()
    {
        if (_whiteSprite != null)
        {
            return;
        }

        _whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        _whiteTexture.name = "ScenarioWhiteTexture";
        _whiteTexture.SetPixel(0, 0, Color.white);
        _whiteTexture.Apply(false, true);

        _whiteSprite = Sprite.Create(
            _whiteTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f);
        _whiteSprite.name = "ScenarioWhiteSprite";
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (scenarios == null || scenarios.Length == 0)
        {
            scenarios = CreateDefaultScenarios();
            return;
        }

        for (int i = 0; i < scenarios.Length; i++)
        {
            if (scenarios[i] == null)
            {
                scenarios[i] = new ScenarioDefinition();
            }

            scenarios[i].durationSeconds = Mathf.Max(0.1f, scenarios[i].durationSeconds);
        }

        scrollSpeed = Mathf.Max(0.01f, scrollSpeed);
    }
#endif
}

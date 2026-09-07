using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Procedurally generates scenario stretches at runtime and scrolls them past
/// a fixed camera/player. Planetary and Space alternate; cloud piles appear
/// only on the horizontal seam during transitions.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ScenarioChunkBuilder))]
[DefaultExecutionOrder(100)]
public class ScenarioPathRunner : MonoBehaviour
{
    [Header("Procedural Path")]
    [SerializeField]
    [Min(1)]
    [Tooltip("How many freshly generated scenarios make up one loop.")]
    int scenariosPerLoop = 6;

    [SerializeField]
    [Min(0.1f)]
    float scenarioDurationSeconds = 8f;

    [SerializeField]
    [Tooltip("If > 0, the whole run uses this seed. 0 = different layout every play.")]
    int runSeed;

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
    [Tooltip("Must be above the player (sorting 3) so the ship flies behind the cloud seam.")]
    int cloudSeamSortingOrder = 50;

    [SerializeField]
    Camera worldCamera;

    [Header("Runtime")]
    [SerializeField]
    bool autoStart = true;

    [SerializeField]
    [Tooltip("When all scenarios finish, generate a new loop and raise the loop index.")]
    bool loopOnComplete = true;

    ScenarioChunkBuilder _chunkBuilder;
    Transform _currentChunk;
    Transform _incomingChunk;
    Transform _seamClouds;
    readonly List<ScenarioDefinition> _generated = new();
    System.Random _runRng;
    float _segmentTravel;
    float _currentDurationDistance;
    int _scenarioIndex;
    int _loopIndex;
    bool _isRunning;
    bool _completed;
    bool _isTransitioning;
    float _chunkHalfWidth;
    float _chunkHalfHeight;

    public event Action<int> OnScenarioStarted;
    public event Action OnPathCompleted;
    /// <summary>Fired when a full scenario cycle begins. Arg is the 0-based loop index.</summary>
    public event Action<int> OnLoopStarted;

    public bool IsRunning => _isRunning;
    public bool IsCompleted => _completed;
    public bool LoopOnComplete => loopOnComplete;
    public int CurrentScenarioIndex => _scenarioIndex;
    public int LoopIndex => _loopIndex;
    public int ScenarioCount => Mathf.Max(1, scenariosPerLoop);
    public float Progress01
    {
        get
        {
            int count = ScenarioCount;
            float distancePer = scrollSpeed * Mathf.Max(0.1f, scenarioDurationSeconds);
            float total = distancePer * count;
            if (total <= 0f)
            {
                return 0f;
            }

            float done = distancePer * _scenarioIndex;
            done += Mathf.Min(_segmentTravel, distancePer);
            return Mathf.Clamp01(done / total);
        }
    }

    void Awake()
    {
        Screen.orientation = ScreenOrientation.Portrait;

        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }

        _chunkBuilder = GetComponent<ScenarioChunkBuilder>();
        if (_chunkBuilder == null)
        {
            _chunkBuilder = gameObject.AddComponent<ScenarioChunkBuilder>();
        }

        _chunkBuilder.EnsureArtLoaded();
    }

    void Start()
    {
        CreateChunks();

        if (autoStart)
        {
            StartPath();
        }
    }

    void OnDestroy()
    {
        if (_chunkBuilder != null)
        {
            _chunkBuilder.ReleaseRuntimeAssets();
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

        if (_isTransitioning && _currentChunk != null)
        {
            // Seam clouds ride the top edge of the outgoing chunk.
            _currentChunk.position += Vector3.down * delta;
            if (_seamClouds != null)
            {
                _seamClouds.position += Vector3.down * delta;
            }

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
    /// Starts (or restarts) a freshly generated scenario loop.
    /// </summary>
    public void BeginLoop(int loopIndex, bool resetLoopIndex = false)
    {
        if (_currentChunk == null)
        {
            CreateChunks();
        }

        _loopIndex = resetLoopIndex ? 0 : Mathf.Max(0, loopIndex);
        _scenarioIndex = 0;
        _completed = false;
        _isTransitioning = false;
        _segmentTravel = 0f;
        _isRunning = true;

        InitRunRng();
        GenerateLoopScenarios();
        ClearSeamClouds();

        ApplyChunk(_currentChunk, GetViewCenter(), 0, sortingOrder + 1);
        _currentChunk.gameObject.SetActive(true);

        if (_incomingChunk != null)
        {
            _incomingChunk.gameObject.SetActive(false);
        }

        _currentDurationDistance = scrollSpeed * Mathf.Max(0.1f, scenarioDurationSeconds);
        OnLoopStarted?.Invoke(_loopIndex);
        OnScenarioStarted?.Invoke(0);
    }

    public void StopPath()
    {
        _isRunning = false;
    }

    void BeginNextTransition()
    {
        int outgoingSeed = TryGetScenario(_scenarioIndex, out ScenarioDefinition outgoing)
            ? outgoing.seed
            : Environment.TickCount;

        int nextIndex = _scenarioIndex + 1;
        bool startingNewLoop = false;

        if (nextIndex >= ScenarioCount)
        {
            if (!loopOnComplete)
            {
                CompletePath();
                return;
            }

            OnPathCompleted?.Invoke();
            _loopIndex++;
            nextIndex = 0;
            startingNewLoop = true;
            InitRunRng();
            GenerateLoopScenarios();
        }

        _scenarioIndex = nextIndex;
        _segmentTravel = 0f;
        _isTransitioning = true;
        _currentDurationDistance = GetViewHeight();

        ApplyChunk(_incomingChunk, GetViewCenter(), nextIndex, sortingOrder);
        _incomingChunk.gameObject.SetActive(true);
        SetChunkSorting(_currentChunk, sortingOrder + 1);

        // Cloud pile only on the dividing line (top edge of the outgoing chunk).
        PlaceSeamCloudsOnCurrentTop(outgoingSeed);

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

        var previous = _currentChunk;
        _currentChunk = _incomingChunk;
        _incomingChunk = previous;

        Vector3 center = GetViewCenter();
        _currentChunk.position = new Vector3(center.x, center.y, transform.position.z);
        SetChunkSorting(_currentChunk, sortingOrder + 1);
        _currentChunk.gameObject.SetActive(true);
        _incomingChunk.gameObject.SetActive(false);
        ClearSeamClouds();

        float fullDistance = scrollSpeed * Mathf.Max(0.1f, scenarioDurationSeconds);
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
        ClearSeamClouds();
        OnPathCompleted?.Invoke();
    }

    void InitRunRng()
    {
        int seed = runSeed != 0
            ? unchecked(runSeed + _loopIndex * 9973)
            : unchecked(Environment.TickCount ^ (_loopIndex * 7919) ^ GetInstanceID());
        _runRng = new System.Random(seed);
    }

    void GenerateLoopScenarios()
    {
        if (_runRng == null)
        {
            InitRunRng();
        }

        _generated.Clear();
        int count = Mathf.Max(1, scenariosPerLoop);

        // Randomize which kind starts this loop, then keep alternating.
        ScenarioKind kind = _runRng.Next(0, 2) == 0 ? ScenarioKind.Planetary : ScenarioKind.Space;

        for (int i = 0; i < count; i++)
        {
            _generated.Add(CreateScenario(kind, i));
            kind = kind == ScenarioKind.Planetary ? ScenarioKind.Space : ScenarioKind.Planetary;
        }
    }

    ScenarioDefinition CreateScenario(ScenarioKind kind, int indexInLoop)
    {
        // Bold rolls: solid or multi-stop feel via horizontal gradient.
        bool useGradient = _runRng.NextDouble() < 0.62;
        Color primary = RollBackgroundColor(kind);
        Color secondary = RollBackgroundColor(kind);

        if (useGradient)
        {
            // Force a readable contrast between gradient ends.
            int guard = 0;
            while (ColorsTooClose(primary, secondary) && guard++ < 8)
            {
                secondary = RollBackgroundColor(kind);
            }

            if (ColorsTooClose(primary, secondary))
            {
                Color.RGBToHSV(primary, out float h, out float s, out float v);
                secondary = Color.HSVToRGB(
                    (h + 0.18f + (float)_runRng.NextDouble() * 0.25f) % 1f,
                    Mathf.Clamp01(s * 0.85f),
                    Mathf.Clamp01(kind == ScenarioKind.Space ? v * 0.55f : v * 1.15f));
            }
        }

        return new ScenarioDefinition
        {
            displayName = kind == ScenarioKind.Planetary
                ? $"Planeta {_loopIndex}-{indexInLoop}"
                : $"Espaco {_loopIndex}-{indexInLoop}",
            kind = kind,
            color = primary,
            useGradient = useGradient,
            gradientColor = secondary,
            durationSeconds = scenarioDurationSeconds,
            seed = _runRng.Next(1, int.MaxValue),
        };
    }

    Color RollBackgroundColor(ScenarioKind kind)
    {
        float hue = (float)_runRng.NextDouble();

        if (kind == ScenarioKind.Planetary)
        {
            // Punchy surface colors: any hue, usually vivid, never near-black.
            float sat = LerpRng(0.45f, 1f);
            float val = LerpRng(0.42f, 0.95f);

            // Occasional pastel / neon extremes.
            double style = _runRng.NextDouble();
            if (style < 0.18)
            {
                sat = LerpRng(0.15f, 0.4f);
                val = LerpRng(0.75f, 1f);
            }
            else if (style < 0.36)
            {
                sat = LerpRng(0.85f, 1f);
                val = LerpRng(0.55f, 1f);
            }

            return Color.HSVToRGB(hue, sat, val);
        }

        // Space: dark but chromatic — deep teals, magentas, violets, crimson voids.
        float spaceSat = LerpRng(0.35f, 1f);
        float spaceVal = LerpRng(0.04f, 0.28f);

        double spaceStyle = _runRng.NextDouble();
        if (spaceStyle < 0.2)
        {
            // Near-black with a tint.
            spaceSat = LerpRng(0.2f, 0.7f);
            spaceVal = LerpRng(0.02f, 0.1f);
        }
        else if (spaceStyle < 0.4)
        {
            // Bold nebula glow (still darker than planetary).
            spaceSat = LerpRng(0.7f, 1f);
            spaceVal = LerpRng(0.16f, 0.38f);
        }

        return Color.HSVToRGB(hue, spaceSat, spaceVal);
    }

    float LerpRng(float a, float b)
    {
        return a + (float)_runRng.NextDouble() * (b - a);
    }

    static bool ColorsTooClose(Color a, Color b)
    {
        float dr = a.r - b.r;
        float dg = a.g - b.g;
        float db = a.b - b.b;
        return (dr * dr + dg * dg + db * db) < 0.045f;
    }

    bool TryGetScenario(int index, out ScenarioDefinition scenario)
    {
        if (index >= 0 && index < _generated.Count)
        {
            scenario = _generated[index];
            return scenario != null;
        }

        scenario = null;
        return false;
    }

    void CreateChunks()
    {
        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }

        if (_chunkBuilder == null)
        {
            _chunkBuilder = GetComponent<ScenarioChunkBuilder>();
        }

        _currentChunk = CreateChunkObject("ScenarioCurrent");
        _incomingChunk = CreateChunkObject("ScenarioIncoming");
        _incomingChunk.gameObject.SetActive(false);

        var seamGo = new GameObject("ScenarioSeamClouds");
        seamGo.transform.SetParent(transform, false);
        _seamClouds = seamGo.transform;
        _seamClouds.gameObject.SetActive(false);
    }

    Transform CreateChunkObject(string objectName)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(transform, false);
        // Keeps background + props as one visual unit so decorations never
        // punch through the chunk in front during transitions.
        go.AddComponent<SortingGroup>();
        return go.transform;
    }

    void ApplyChunk(Transform chunk, Vector3 worldCenter, int scenarioIndex, int baseSorting)
    {
        if (chunk == null || worldCamera == null || _chunkBuilder == null)
        {
            return;
        }

        _chunkHalfWidth = GetViewWidth() * 0.5f + widthPadding;
        _chunkHalfHeight = GetViewHeight() * 0.5f + widthPadding * 0.5f;

        chunk.position = new Vector3(worldCenter.x, worldCenter.y, transform.position.z);
        chunk.rotation = Quaternion.identity;
        chunk.localScale = Vector3.one;
        SetChunkSorting(chunk, baseSorting);

        if (!TryGetScenario(scenarioIndex, out ScenarioDefinition scenario))
        {
            return;
        }

        // Local sorting only (0,1,2...). Chunk order is owned by SortingGroup.
        _chunkBuilder.Build(
            chunk,
            scenario,
            _chunkHalfWidth,
            _chunkHalfHeight,
            baseSortingOrder: 0);
    }

    void PlaceSeamCloudsOnCurrentTop(int seed)
    {
        if (_seamClouds == null || _currentChunk == null || _chunkBuilder == null)
        {
            return;
        }

        Vector3 top = _currentChunk.position + Vector3.up * _chunkHalfHeight;
        _seamClouds.position = new Vector3(top.x, top.y, _currentChunk.position.z);
        _seamClouds.rotation = Quaternion.identity;
        _seamClouds.localScale = Vector3.one;
        _seamClouds.gameObject.SetActive(true);

        if (!_seamClouds.TryGetComponent(out SortingGroup seamGroup))
        {
            seamGroup = _seamClouds.gameObject.AddComponent<SortingGroup>();
        }

        // Above player/enemies so the ship passes behind the cloud bank.
        seamGroup.sortingOrder = cloudSeamSortingOrder;

        _chunkBuilder.BuildCloudSeam(
            _seamClouds,
            unchecked(seed * 31 + 17),
            _chunkHalfWidth,
            baseSortingOrder: 0);
    }

    void ClearSeamClouds()
    {
        if (_seamClouds == null)
        {
            return;
        }

        for (int i = _seamClouds.childCount - 1; i >= 0; i--)
        {
            Destroy(_seamClouds.GetChild(i).gameObject);
        }

        _seamClouds.gameObject.SetActive(false);
    }

    static void SetChunkSorting(Transform chunk, int baseSorting)
    {
        if (chunk == null)
        {
            return;
        }

        if (!chunk.TryGetComponent(out SortingGroup group))
        {
            group = chunk.gameObject.AddComponent<SortingGroup>();
        }

        group.sortingOrder = baseSorting;
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

#if UNITY_EDITOR
    void OnValidate()
    {
        scenariosPerLoop = Mathf.Max(1, scenariosPerLoop);
        scenarioDurationSeconds = Mathf.Max(0.1f, scenarioDurationSeconds);
        scrollSpeed = Mathf.Max(0.01f, scrollSpeed);

        if (_chunkBuilder == null)
        {
            _chunkBuilder = GetComponent<ScenarioChunkBuilder>();
        }
    }
#endif
}

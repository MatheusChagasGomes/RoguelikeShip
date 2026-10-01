using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Continuously scrolls procedurally generated scenario chunks past a fixed camera/player.
/// While the player is in a stretch, the next chunk and its cloud seam are already built
/// and stacked above, so the handoff scrolls in instead of spawning suddenly.
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
    [Tooltip("Seconds each scenario takes to scroll fully past the camera.")]
    float scenarioDurationSeconds = 20f;

    [SerializeField]
    [Tooltip("If > 0, the whole run uses this seed. 0 = different layout every play.")]
    int runSeed;

    [Header("Scroll")]
    [SerializeField]
    [Min(0.01f)]
    [Tooltip("World units per second.")]
    float scrollSpeed = 3f;

    [SerializeField]
    [Min(1f)]
    [Tooltip("Minimum scenario length in camera heights (longer stretches feel like flight).")]
    float minChunkHeightScreens = 2.5f;

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
    int _preparedNextIndex;
    bool _isRunning;
    bool _completed;
    bool _isTransitioning;
    bool _nextPrepared;
    bool _preparedStartsNewLoop;
    bool _boundaryEventPending;
    bool _pendingStartsNewLoop;
    float _chunkHalfWidth;
    float _currentHalfHeight;
    float _incomingHalfHeight;
    Transform _boundaryAnchor;

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

        // Current, prepared next, and seam always move together.
        ScrollPreparedPath(delta);

        if (_isTransitioning)
        {
            if (_boundaryEventPending && HasReachedScenarioBoundary())
            {
                RaiseBoundaryEvents();
            }

            if (_segmentTravel >= _currentDurationDistance)
            {
                if (_boundaryEventPending)
                {
                    RaiseBoundaryEvents();
                }

                FinishTransitionAndPrepareNext();
            }
        }
        else if (_segmentTravel >= _currentDurationDistance)
        {
            if (!_nextPrepared)
            {
                CompletePath();
                return;
            }

            BeginSeamCrossing();
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
        _nextPrepared = false;
        _preparedStartsNewLoop = false;
        _boundaryEventPending = false;
        _pendingStartsNewLoop = false;
        _segmentTravel = 0f;
        _isRunning = true;

        InitRunRng(_loopIndex);
        GenerateLoopScenarios(_loopIndex);

        Vector3 startPos = GetBottomAlignedChunkPosition(ComputeChunkHalfHeight(0));
        ApplyChunk(_currentChunk, startPos, 0, sortingOrder + 1, out _currentHalfHeight);
        _currentChunk.gameObject.SetActive(true);

        PrepareNextChunk();

        _currentDurationDistance = GetPreTransitionDistance(_currentHalfHeight);
        OnLoopStarted?.Invoke(_loopIndex);
        OnScenarioStarted?.Invoke(0);
    }

    public void StopPath()
    {
        _isRunning = false;
    }

    /// <summary>
    /// Seam is entering the view. Indices advance now; scenario/loop events wait until the
    /// cloud dividing line reaches the player (or camera center).
    /// </summary>
    void BeginSeamCrossing()
    {
        _segmentTravel = 0f;
        _isTransitioning = true;
        _currentDurationDistance = GetViewHeight();
        _scenarioIndex = _preparedNextIndex;

        _boundaryEventPending = true;
        _pendingStartsNewLoop = _preparedStartsNewLoop;
        _preparedStartsNewLoop = false;
    }

    void RaiseBoundaryEvents()
    {
        if (!_boundaryEventPending)
        {
            return;
        }

        _boundaryEventPending = false;

        if (_pendingStartsNewLoop)
        {
            OnPathCompleted?.Invoke();
            _loopIndex++;
            OnLoopStarted?.Invoke(_loopIndex);
            _pendingStartsNewLoop = false;
        }

        OnScenarioStarted?.Invoke(_scenarioIndex);
    }

    bool HasReachedScenarioBoundary()
    {
        float boundaryY = ResolveBoundaryAnchorY();

        if (_seamClouds != null && _seamClouds.gameObject.activeSelf)
        {
            return _seamClouds.position.y <= boundaryY;
        }

        // Fallback if the seam visual is missing: mid-screen during the cross.
        return _segmentTravel >= GetViewHeight() * 0.5f;
    }

    float ResolveBoundaryAnchorY()
    {
        if (_boundaryAnchor == null)
        {
            PlayerHealth player = FindFirstObjectByType<PlayerHealth>();
            if (player != null)
            {
                _boundaryAnchor = player.transform;
            }
        }

        if (_boundaryAnchor != null)
        {
            return _boundaryAnchor.position.y;
        }

        return GetViewCenter().y;
    }

    /// <summary>
    /// Current chunk has left the view; promote the prepared next and build the one after that.
    /// </summary>
    void FinishTransitionAndPrepareNext()
    {
        _isTransitioning = false;
        _segmentTravel = 0f;

        var previous = _currentChunk;
        _currentChunk = _incomingChunk;
        _incomingChunk = previous;
        _currentHalfHeight = _incomingHalfHeight;

        SetChunkSorting(_currentChunk, sortingOrder + 1);
        _currentChunk.gameObject.SetActive(true);

        // Drop the outgoing visuals before rebuilding that transform as the new lookahead.
        ClearChunkChildren(_incomingChunk);
        _incomingChunk.gameObject.SetActive(false);
        ClearSeamClouds();
        _nextPrepared = false;

        PrepareNextChunk();

        _currentDurationDistance = GetPreTransitionDistance(_currentHalfHeight);

        if (_currentDurationDistance <= 0f)
        {
            if (!_nextPrepared)
            {
                CompletePath();
                return;
            }

            BeginSeamCrossing();
        }
    }

    /// <summary>
    /// Builds the upcoming scenario stacked above the current one, plus the cloud seam on the join.
    /// </summary>
    void PrepareNextChunk()
    {
        if (_currentChunk == null || _incomingChunk == null)
        {
            _nextPrepared = false;
            return;
        }

        int outgoingSeed = TryGetScenario(_scenarioIndex, out ScenarioDefinition outgoing)
            ? outgoing.seed
            : Environment.TickCount;

        int nextIndex = _scenarioIndex + 1;
        bool startsNewLoop = false;

        if (nextIndex >= ScenarioCount)
        {
            if (!loopOnComplete)
            {
                _nextPrepared = false;
                _preparedStartsNewLoop = false;
                ClearSeamClouds();
                _incomingChunk.gameObject.SetActive(false);
                return;
            }

            // Lookahead into the next loop while the player is still finishing this one.
            int nextLoopIndex = _loopIndex + 1;
            InitRunRng(nextLoopIndex);
            GenerateLoopScenarios(nextLoopIndex);
            nextIndex = 0;
            startsNewLoop = true;
        }

        _preparedNextIndex = nextIndex;
        _preparedStartsNewLoop = startsNewLoop;

        float nextHalfHeight = ComputeChunkHalfHeight(nextIndex);
        Vector3 stackedPos = _currentChunk.position
            + Vector3.up * (_currentHalfHeight + nextHalfHeight);
        ApplyChunk(_incomingChunk, stackedPos, nextIndex, sortingOrder, out _incomingHalfHeight);
        _incomingChunk.gameObject.SetActive(true);
        SetChunkSorting(_currentChunk, sortingOrder + 1);

        PlaceSeamCloudsOnCurrentTop(outgoingSeed);
        _nextPrepared = true;
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
        _nextPrepared = false;
        ClearSeamClouds();
        OnPathCompleted?.Invoke();
    }

    void ScrollPreparedPath(float delta)
    {
        Vector3 step = Vector3.down * delta;

        if (_currentChunk != null)
        {
            _currentChunk.position += step;
        }

        if (_nextPrepared && _incomingChunk != null && _incomingChunk.gameObject.activeSelf)
        {
            _incomingChunk.position += step;
        }

        if (_seamClouds != null && _seamClouds.gameObject.activeSelf)
        {
            _seamClouds.position += step;
        }
    }

    void InitRunRng(int loopIndex)
    {
        int seed = runSeed != 0
            ? unchecked(runSeed + loopIndex * 9973)
            : unchecked(Environment.TickCount ^ (loopIndex * 7919) ^ GetInstanceID());
        _runRng = new System.Random(seed);
    }

    void GenerateLoopScenarios(int loopIndex)
    {
        if (_runRng == null)
        {
            InitRunRng(loopIndex);
        }

        var generator = new ScenarioGenerator(_runRng, scenarioDurationSeconds, loopIndex);
        _generated.Clear();
        _generated.AddRange(generator.GenerateLoop(scenariosPerLoop));
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

    void ApplyChunk(
        Transform chunk,
        Vector3 worldPosition,
        int scenarioIndex,
        int baseSorting,
        out float halfHeight)
    {
        halfHeight = 0f;
        if (chunk == null || worldCamera == null || _chunkBuilder == null)
        {
            return;
        }

        _chunkHalfWidth = GetViewWidth() * 0.5f + widthPadding;
        halfHeight = ComputeChunkHalfHeight(scenarioIndex);

        chunk.position = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);
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
            halfHeight,
            baseSortingOrder: 0);
    }

    float ComputeChunkHalfHeight(int scenarioIndex)
    {
        float duration = scenarioDurationSeconds;
        if (TryGetScenario(scenarioIndex, out ScenarioDefinition scenario) && scenario != null)
        {
            duration = Mathf.Max(0.1f, scenario.durationSeconds);
        }

        float travelHeight = scrollSpeed * duration;
        float minHeight = GetViewHeight() * Mathf.Max(1f, minChunkHeightScreens);
        float chunkHeight = Mathf.Max(minHeight, travelHeight);
        return chunkHeight * 0.5f + widthPadding * 0.5f;
    }

    Vector3 GetBottomAlignedChunkPosition(float halfHeight)
    {
        Vector3 center = GetViewCenter();
        float y = center.y - GetViewHeight() * 0.5f + halfHeight;
        return new Vector3(center.x, y, transform.position.z);
    }

    float GetPreTransitionDistance(float halfHeight)
    {
        float chunkHeight = halfHeight * 2f;
        return Mathf.Max(0f, chunkHeight - GetViewHeight());
    }

    void PlaceSeamCloudsOnCurrentTop(int seed)
    {
        if (_seamClouds == null || _currentChunk == null || _chunkBuilder == null)
        {
            return;
        }

        Vector3 top = _currentChunk.position + Vector3.up * _currentHalfHeight;
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

        ClearChunkChildren(_seamClouds);
        _seamClouds.gameObject.SetActive(false);
    }

    static void ClearChunkChildren(Transform root)
    {
        if (root == null)
        {
            return;
        }

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Destroy(root.GetChild(i).gameObject);
        }
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
        minChunkHeightScreens = Mathf.Max(1f, minChunkHeightScreens);

        if (_chunkBuilder == null)
        {
            _chunkBuilder = GetComponent<ScenarioChunkBuilder>();
        }
    }
#endif
}

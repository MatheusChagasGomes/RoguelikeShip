using UnityEngine;

/// <summary>
/// Spawns enemy prefabs just above the camera view at a steady interval.
/// Difficulty scales spawn density and per-enemy combat stats each scenario loop.
/// </summary>
[DisallowMultipleComponent]
public class EnemySpawner : MonoBehaviour
{
    [Header("Spawn")]
    [SerializeField] GameObject enemyPrefab;
    [SerializeField] [Min(0.1f)] float spawnInterval = 2.5f;
    [SerializeField] [Min(0f)] float initialDelay = 1f;
    [SerializeField] [Min(1)] int maxAlive = 6;
    [SerializeField] bool autoStart = true;

    [Header("Placement")]
    [SerializeField] Camera worldCamera;
    [Tooltip("How far above the top of the view enemies appear.")]
    [SerializeField] [Min(0f)] float spawnPaddingY = 1.2f;
    [Tooltip("Inset from the left/right edges of the view.")]
    [SerializeField] [Min(0f)] float sidePadding = 0.6f;

    float _baseSpawnInterval;
    int _baseMaxAlive;
    float _activeSpawnInterval;
    int _activeMaxAlive;
    float _enemyHealthScale = 1f;
    float _enemyFireIntervalScale = 1f;
    float _minEnemyFireInterval = 0.45f;
    float _enemyProjectileSpeedScale = 1f;
    float _enemyEnterSpeedScale = 1f;
    float _nextSpawnTime;
    bool _isRunning;
    int _aliveCount;

    public bool IsRunning => _isRunning;
    public float ActiveSpawnInterval => _activeSpawnInterval;
    public int ActiveMaxAlive => _activeMaxAlive;

    void Awake()
    {
        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }

        _baseSpawnInterval = Mathf.Max(0.1f, spawnInterval);
        _baseMaxAlive = Mathf.Max(1, maxAlive);
        _activeSpawnInterval = _baseSpawnInterval;
        _activeMaxAlive = _baseMaxAlive;
    }

    void Start()
    {
        if (autoStart)
        {
            StartSpawning();
        }
    }

    void Update()
    {
        if (!_isRunning || enemyPrefab == null || worldCamera == null)
        {
            return;
        }

        if (_aliveCount >= _activeMaxAlive || Time.time < _nextSpawnTime)
        {
            return;
        }

        SpawnEnemy();
        _nextSpawnTime = Time.time + _activeSpawnInterval;
    }

    public void StartSpawning()
    {
        _isRunning = true;
        _nextSpawnTime = Time.time + Mathf.Max(0f, initialDelay);
    }

    public void StopSpawning()
    {
        _isRunning = false;
    }

    /// <summary>
    /// Applies loop-based difficulty. Scales are relative to the Inspector base values.
    /// </summary>
    public void ApplyDifficulty(
        float spawnIntervalScale,
        float minSpawnInterval,
        int maxAliveBonus,
        int maxAliveCap,
        float enemyHealthScale,
        float enemyFireIntervalScale,
        float minEnemyFireInterval,
        float enemyProjectileSpeedScale,
        float enemyEnterSpeedScale)
    {
        _activeSpawnInterval = Mathf.Max(minSpawnInterval, _baseSpawnInterval * Mathf.Max(0.01f, spawnIntervalScale));
        _activeMaxAlive = Mathf.Clamp(_baseMaxAlive + Mathf.Max(0, maxAliveBonus), 1, Mathf.Max(1, maxAliveCap));
        _enemyHealthScale = Mathf.Max(0.01f, enemyHealthScale);
        _enemyFireIntervalScale = Mathf.Max(0.01f, enemyFireIntervalScale);
        _minEnemyFireInterval = Mathf.Max(0.05f, minEnemyFireInterval);
        _enemyProjectileSpeedScale = Mathf.Max(0.01f, enemyProjectileSpeedScale);
        _enemyEnterSpeedScale = Mathf.Max(0.01f, enemyEnterSpeedScale);

        spawnInterval = _activeSpawnInterval;
        maxAlive = _activeMaxAlive;
    }

    void SpawnEnemy()
    {
        Vector2 spawnPosition = GetSpawnPosition();
        GameObject enemy = Instantiate(enemyPrefab, spawnPosition, enemyPrefab.transform.rotation);

        if (enemy.TryGetComponent(out EnemyHealth health))
        {
            int scaledMax = Mathf.Max(1, Mathf.RoundToInt(health.MaxHealth * _enemyHealthScale));
            health.SetMaxHealth(scaledMax, refill: true);
            _aliveCount++;
            health.Died += OnEnemyDied;
        }

        if (enemy.TryGetComponent(out EnemyOne enemyOne))
        {
            enemyOne.ApplyDifficultyScaling(
                _enemyFireIntervalScale,
                _minEnemyFireInterval,
                _enemyProjectileSpeedScale,
                _enemyEnterSpeedScale);
        }
    }

    void OnEnemyDied()
    {
        _aliveCount = Mathf.Max(0, _aliveCount - 1);
    }

    Vector2 GetSpawnPosition()
    {
        float verticalExtent = worldCamera.orthographicSize;
        float horizontalExtent = verticalExtent * worldCamera.aspect;
        Vector3 cameraPosition = worldCamera.transform.position;

        float minX = cameraPosition.x - horizontalExtent + sidePadding;
        float maxX = cameraPosition.x + horizontalExtent - sidePadding;
        if (minX > maxX)
        {
            minX = maxX = cameraPosition.x;
        }

        float x = Random.Range(minX, maxX);
        float y = cameraPosition.y + verticalExtent + spawnPaddingY;
        return new Vector2(x, y);
    }
}

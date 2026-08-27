using UnityEngine;

/// <summary>
/// When the scenario path finishes a full cycle, difficulty ramps up for the next loop.
/// </summary>
[DisallowMultipleComponent]
public class GameLoopController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] ScenarioPathRunner pathRunner;
    [SerializeField] EnemySpawner enemySpawner;

    [Header("Spawn Difficulty")]
    [Tooltip("Spawn interval is multiplied by this each loop (lower = denser spawns).")]
    [SerializeField] [Range(0.5f, 1f)] float spawnIntervalScalePerLoop = 0.88f;
    [SerializeField] [Min(0.2f)] float minSpawnInterval = 0.65f;
    [SerializeField] [Min(0)] int maxAliveBonusPerLoop = 1;
    [SerializeField] [Min(1)] int maxAliveCap = 14;

    [Header("Enemy Difficulty")]
    [SerializeField] [Min(1f)] float enemyHealthScalePerLoop = 1.18f;
    [Tooltip("Fire interval is multiplied by this each loop (lower = shoots faster).")]
    [SerializeField] [Range(0.5f, 1f)] float enemyFireIntervalScalePerLoop = 0.9f;
    [SerializeField] [Min(0.2f)] float minEnemyFireInterval = 0.45f;
    [SerializeField] [Min(1f)] float enemyProjectileSpeedScalePerLoop = 1.08f;
    [SerializeField] [Min(1f)] float enemyEnterSpeedScalePerLoop = 1.05f;

    public int CurrentLoop => pathRunner != null ? pathRunner.LoopIndex : 0;

    void Awake()
    {
        if (pathRunner == null)
        {
            pathRunner = FindFirstObjectByType<ScenarioPathRunner>();
        }

        if (enemySpawner == null)
        {
            enemySpawner = FindFirstObjectByType<EnemySpawner>();
        }

        EnsureUpgradeSystem();
    }

    void EnsureUpgradeSystem()
    {
        if (FindFirstObjectByType<PlayerUpgradeController>() == null)
        {
            gameObject.AddComponent<PlayerUpgradeController>();
        }

        if (FindFirstObjectByType<UpgradeOfferController>() == null)
        {
            gameObject.AddComponent<UpgradeOfferController>();
        }
    }

    void OnEnable()
    {
        if (pathRunner != null)
        {
            pathRunner.OnLoopStarted += HandleLoopStarted;
        }
    }

    void OnDisable()
    {
        if (pathRunner != null)
        {
            pathRunner.OnLoopStarted -= HandleLoopStarted;
        }
    }

    void Start()
    {
        ApplyDifficulty(CurrentLoop);
    }

    void HandleLoopStarted(int loopIndex)
    {
        ApplyDifficulty(loopIndex);
    }

    void ApplyDifficulty(int loopIndex)
    {
        loopIndex = Mathf.Max(0, loopIndex);

        if (enemySpawner == null)
        {
            return;
        }

        float spawnIntervalScale = Mathf.Pow(spawnIntervalScalePerLoop, loopIndex);
        float healthScale = Mathf.Pow(enemyHealthScalePerLoop, loopIndex);
        float fireIntervalScale = Mathf.Pow(enemyFireIntervalScalePerLoop, loopIndex);
        float projectileSpeedScale = Mathf.Pow(enemyProjectileSpeedScalePerLoop, loopIndex);
        float enterSpeedScale = Mathf.Pow(enemyEnterSpeedScalePerLoop, loopIndex);
        int maxAliveBonus = maxAliveBonusPerLoop * loopIndex;

        enemySpawner.ApplyDifficulty(
            spawnIntervalScale,
            minSpawnInterval,
            maxAliveBonus,
            maxAliveCap,
            healthScale,
            fireIntervalScale,
            minEnemyFireInterval,
            projectileSpeedScale,
            enterSpeedScale);
    }
}

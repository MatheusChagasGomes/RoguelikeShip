using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Applies consumable effects for the current run and clears loop-scoped buffs at loop end.
/// </summary>
[DisallowMultipleComponent]
public class PlayerConsumableController : MonoBehaviour
{
    const float MetamorphosisIntervalSeconds = 5f;
    const float EmergencyRepairHealthThreshold = 0.3f;
    const int RepairKitHealAmount = 10;
    const int RepairKitCharges = 2;
    const int EmergencyRepairHealAmount = 30;
    const int NotTodayHealAmount = 20;

    [Header("References")]
    [SerializeField] ScenarioPathRunner pathRunner;
    [SerializeField] PlayerHealth playerHealth;
    [SerializeField] PlayerShooting playerShooting;
    [SerializeField] PlayerUpgradeController upgradeController;

    [Header("Pool")]
    [Tooltip("Full consumable pool. Edit names and descriptions here.")]
    public ConsumableDefinition[] consumables = ConsumableDefinition.CreateDefaults();

    readonly List<UpgradeDefinition> _metamorphosisPool = new();
    readonly List<AllyDrone> _legionDrones = new();

    int _loopStartHealth;
    int _repairKitCharges;
    bool _emergencyRepairArmed;
    bool _loopMagazineBonusActive;
    int _loopMagazineBonus;
    bool _loopCooldownBonusActive;
    float _loopCooldownBonusSeconds;
    bool _notTodayActive;
    bool _timeTravelActive;
    bool _duplicatorActive;
    int _duplicatorPreviousCount = 1;
    bool _infiniteMagazineActive;
    float _infiniteMagazinePreviousCooldown = 5f;
    bool _metamorphosisActive;
    float _nextMetamorphosisTime;
    bool _hasMetamorphosisSnapshot;
    PlayerUpgradeController.TemporaryUpgradeSnapshot _metamorphosisSnapshot;

    public IReadOnlyList<ConsumableDefinition> Consumables => consumables;

    void Awake()
    {
        EnsureConsumables();
        ResolveReferences();
        CaptureLoopStartHealth();
    }

    void OnEnable()
    {
        ResolveReferences();

        if (pathRunner != null)
        {
            pathRunner.OnPathCompleted += HandleLoopCompleted;
            pathRunner.OnLoopStarted += HandleLoopStarted;
        }

        if (playerHealth != null)
        {
            playerHealth.Damaged += HandleDamaged;
            playerHealth.HealthChanged += HandleHealthChanged;
        }
    }

    void OnDisable()
    {
        if (pathRunner != null)
        {
            pathRunner.OnPathCompleted -= HandleLoopCompleted;
            pathRunner.OnLoopStarted -= HandleLoopStarted;
        }

        if (playerHealth != null)
        {
            playerHealth.Damaged -= HandleDamaged;
            playerHealth.HealthChanged -= HandleHealthChanged;
        }

        ClearLoopScopedEffects();
    }

    void Update()
    {
        if (!_metamorphosisActive || upgradeController == null)
        {
            return;
        }

        if (Time.time < _nextMetamorphosisTime)
        {
            return;
        }

        RollMetamorphosisUpgrade();
        _nextMetamorphosisTime = Time.time + MetamorphosisIntervalSeconds;
    }

    void EnsureConsumables()
    {
        ConsumableDefinition[] defaults = ConsumableDefinition.CreateDefaults();
        if (consumables == null || consumables.Length == 0)
        {
            consumables = defaults;
            return;
        }

        var kept = new List<ConsumableDefinition>(consumables.Length);
        var existingIds = new HashSet<ConsumableId>();

        for (int i = 0; i < consumables.Length; i++)
        {
            ConsumableDefinition entry = consumables[i];
            if (entry == null)
            {
                continue;
            }

            kept.Add(entry);
            existingIds.Add(entry.id);
        }

        for (int i = 0; i < defaults.Length; i++)
        {
            if (!existingIds.Contains(defaults[i].id))
            {
                kept.Add(defaults[i]);
            }
        }

        consumables = kept.Count > 0 ? kept.ToArray() : defaults;
    }

    void ResolveReferences()
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

        if (upgradeController == null)
        {
            upgradeController = FindFirstObjectByType<PlayerUpgradeController>();
        }
    }

    public bool TryGetDefinition(ConsumableId id, out ConsumableDefinition definition)
    {
        EnsureConsumables();
        for (int i = 0; i < consumables.Length; i++)
        {
            ConsumableDefinition entry = consumables[i];
            if (entry != null && entry.id == id)
            {
                definition = entry;
                return true;
            }
        }

        definition = null;
        return false;
    }

    public void CollectByRarity(ConsumableRarity rarity, List<ConsumableDefinition> buffer)
    {
        buffer.Clear();
        EnsureConsumables();

        for (int i = 0; i < consumables.Length; i++)
        {
            ConsumableDefinition entry = consumables[i];
            if (entry != null && entry.rarity == rarity)
            {
                buffer.Add(entry);
            }
        }
    }

    public bool TryApply(ConsumableId id)
    {
        if (!TryGetDefinition(id, out _))
        {
            return false;
        }

        ResolveReferences();
        ApplyEffect(id);
        return true;
    }

    void ApplyEffect(ConsumableId id)
    {
        switch (id)
        {
            case ConsumableId.GoldenHammer:
                playerHealth?.Heal(20);
                break;
            case ConsumableId.SpareChangeAmmo:
                AddLoopMagazineBonus(2);
                break;
            case ConsumableId.RepairKit:
                _repairKitCharges = RepairKitCharges;
                break;
            case ConsumableId.AuxiliaryCooler:
                AddLoopCooldownBonus(-1f);
                break;
            case ConsumableId.EmergencyRepair:
                _emergencyRepairArmed = true;
                TryTriggerEmergencyRepair();
                break;
            case ConsumableId.ReserveCartridge:
                playerShooting?.GrantInstantNextReload();
                break;
            case ConsumableId.NotToday:
                GrantNotToday();
                break;
            case ConsumableId.CarefulRepair:
                playerHealth?.Heal(50);
                break;
            case ConsumableId.ExtraBullet:
                AddLoopMagazineBonus(4);
                break;
            case ConsumableId.WalkingMetamorphosis:
                BeginMetamorphosis();
                break;
            case ConsumableId.GreaterCooler:
                AddLoopCooldownBonus(-2f);
                break;
            case ConsumableId.Duplicator:
                BeginDuplicator();
                break;
            case ConsumableId.Legion:
                BeginLegion();
                break;
            case ConsumableId.ExperiencedTinker:
                playerHealth?.Heal(200);
                break;
            case ConsumableId.InfiniteMagazine:
                BeginInfiniteMagazine();
                break;
            case ConsumableId.TimeTravel:
                GrantTimeTravel();
                break;
        }
    }

    void AddLoopMagazineBonus(int amount)
    {
        if (playerShooting == null || amount == 0)
        {
            return;
        }

        playerShooting.AddMagazineSize(amount);
        _loopMagazineBonus += amount;
        _loopMagazineBonusActive = true;
    }

    void AddLoopCooldownBonus(float deltaSeconds)
    {
        if (playerShooting == null || Mathf.Approximately(deltaSeconds, 0f))
        {
            return;
        }

        _loopCooldownBonusSeconds += deltaSeconds;
        _loopCooldownBonusActive = true;

        if (_infiniteMagazineActive)
        {
            _infiniteMagazinePreviousCooldown = Mathf.Max(0f, _infiniteMagazinePreviousCooldown + deltaSeconds);
        }
        else
        {
            playerShooting.AddMagazineCooldown(deltaSeconds);
        }
    }

    void GrantNotToday()
    {
        if (playerHealth == null || _notTodayActive)
        {
            return;
        }

        playerHealth.AddTemporaryResurrectionCharges(1, NotTodayHealAmount);
        _notTodayActive = true;
    }

    void GrantTimeTravel()
    {
        if (playerHealth == null || _timeTravelActive)
        {
            return;
        }

        if (_loopStartHealth <= 0)
        {
            CaptureLoopStartHealth();
        }

        playerHealth.AddTemporaryResurrectionCharges(1, () => Mathf.Max(1, _loopStartHealth));
        _timeTravelActive = true;
    }

    void BeginDuplicator()
    {
        if (playerShooting == null || _duplicatorActive)
        {
            return;
        }

        _duplicatorPreviousCount = Mathf.Max(1, playerShooting.ProjectileCount);
        playerShooting.MultiplyProjectileCount(2);
        _duplicatorActive = true;
    }

    void BeginLegion()
    {
        if (upgradeController == null || _legionDrones.Count > 0)
        {
            return;
        }

        List<AllyDrone> created = upgradeController.DuplicateOwnedDrones();
        _legionDrones.AddRange(created);
    }

    void BeginInfiniteMagazine()
    {
        if (playerShooting == null || _infiniteMagazineActive)
        {
            return;
        }

        _infiniteMagazinePreviousCooldown = playerShooting.MagazineCooldown;
        playerShooting.SetMagazineCooldown(0f);
        _infiniteMagazineActive = true;
    }

    void BeginMetamorphosis()
    {
        if (_metamorphosisActive || upgradeController == null)
        {
            return;
        }

        _metamorphosisActive = true;
        _nextMetamorphosisTime = Time.time;
    }

    void RollMetamorphosisUpgrade()
    {
        ClearMetamorphosisBuff();
        upgradeController.CollectTemporaryEligible(_metamorphosisPool);
        if (_metamorphosisPool.Count == 0)
        {
            return;
        }

        UpgradeDefinition pick = _metamorphosisPool[Random.Range(0, _metamorphosisPool.Count)];
        if (upgradeController.TryApplyTemporaryUpgrade(pick, out _metamorphosisSnapshot))
        {
            _hasMetamorphosisSnapshot = true;
        }
    }

    void ClearMetamorphosisBuff()
    {
        if (!_hasMetamorphosisSnapshot || upgradeController == null)
        {
            _hasMetamorphosisSnapshot = false;
            return;
        }

        upgradeController.RestoreTemporaryUpgrade(_metamorphosisSnapshot);
        _hasMetamorphosisSnapshot = false;
    }

    void HandleDamaged(int amount, int current, int max)
    {
        if (_repairKitCharges <= 0 || playerHealth == null || amount <= 0)
        {
            return;
        }

        _repairKitCharges--;
        playerHealth.Heal(RepairKitHealAmount);
    }

    void HandleHealthChanged(int current, int max)
    {
        TryTriggerEmergencyRepair();
    }

    void TryTriggerEmergencyRepair()
    {
        if (!_emergencyRepairArmed || playerHealth == null || !playerHealth.IsAlive)
        {
            return;
        }

        if (playerHealth.MaxHealth <= 0)
        {
            return;
        }

        float ratio = (float)playerHealth.CurrentHealth / playerHealth.MaxHealth;
        if (ratio >= EmergencyRepairHealthThreshold)
        {
            return;
        }

        _emergencyRepairArmed = false;
        playerHealth.Heal(EmergencyRepairHealAmount);
    }

    void HandleLoopStarted(int loopIndex)
    {
        CaptureLoopStartHealth();
    }

    void HandleLoopCompleted()
    {
        ClearLoopScopedEffects();
    }

    void CaptureLoopStartHealth()
    {
        if (playerHealth != null)
        {
            _loopStartHealth = Mathf.Max(1, playerHealth.CurrentHealth);
        }
    }

    void ClearLoopScopedEffects()
    {
        ClearMetamorphosisBuff();
        _metamorphosisActive = false;

        if (_duplicatorActive && playerShooting != null)
        {
            playerShooting.SetProjectileCount(_duplicatorPreviousCount);
        }

        _duplicatorActive = false;

        if (_infiniteMagazineActive && playerShooting != null)
        {
            playerShooting.SetMagazineCooldown(_infiniteMagazinePreviousCooldown);
        }

        _infiniteMagazineActive = false;

        if (_loopCooldownBonusActive && playerShooting != null && !Mathf.Approximately(_loopCooldownBonusSeconds, 0f))
        {
            playerShooting.AddMagazineCooldown(-_loopCooldownBonusSeconds);
        }

        _loopCooldownBonusSeconds = 0f;
        _loopCooldownBonusActive = false;

        if (_loopMagazineBonusActive && playerShooting != null && _loopMagazineBonus != 0)
        {
            playerShooting.AddMagazineSize(-_loopMagazineBonus);
        }

        _loopMagazineBonus = 0;
        _loopMagazineBonusActive = false;

        if (_legionDrones.Count > 0 && upgradeController != null)
        {
            upgradeController.RemoveDrones(_legionDrones);
        }

        _legionDrones.Clear();

        if ((_notTodayActive || _timeTravelActive) && playerHealth != null)
        {
            playerHealth.ClearTemporaryResurrectionCharges();
        }

        _notTodayActive = false;
        _timeTravelActive = false;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        EnsureConsumables();
    }
#endif
}

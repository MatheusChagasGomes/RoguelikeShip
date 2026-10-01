using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns the run's picked upgrades and applies their gameplay effects.
/// Balance every upgrade in the public Upgrades list in the Inspector.
/// </summary>
[DisallowMultipleComponent]
public class PlayerUpgradeController : MonoBehaviour
{
    [Header("References")]
    public PlayerHealth playerHealth;
    public PlayerMovement playerMovement;
    public PlayerShooting playerShooting;

    [Header("Upgrades")]
    [Tooltip("Full upgrade pool. Edit names, descriptions, and balance values here.")]
    public UpgradeDefinition[] upgrades = UpgradeDefinition.CreateDefaults();

    readonly HashSet<UpgradeId> _owned = new();
    readonly List<AllyDrone> _drones = new();
    float _emergencyFuelMaxBonus;
    bool _emergencyFuel;
    int _dronePowerTier;

    public IReadOnlyCollection<UpgradeId> OwnedUpgrades => _owned;
    public int OwnedDroneCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < _drones.Count; i++)
            {
                if (_drones[i] != null)
                {
                    count++;
                }
            }

            return count;
        }
    }

    void Awake()
    {
        EnsureUpgrades();
        ResolveReferences();
    }

    void OnEnable()
    {
        if (playerHealth != null)
        {
            playerHealth.HealthChanged += HandleHealthChanged;
        }
    }

    void OnDisable()
    {
        if (playerHealth != null)
        {
            playerHealth.HealthChanged -= HandleHealthChanged;
        }
    }

    void EnsureUpgrades()
    {
        UpgradeDefinition[] defaults = UpgradeDefinition.CreateDefaults();

        if (upgrades == null || upgrades.Length == 0)
        {
            upgrades = defaults;
            return;
        }

        var kept = new List<UpgradeDefinition>(upgrades.Length);
        var existingIds = new HashSet<UpgradeId>();

        for (int i = 0; i < upgrades.Length; i++)
        {
            UpgradeDefinition entry = upgrades[i];
            if (entry == null || !IsSupported(entry.id))
            {
                continue;
            }

            kept.Add(entry);
            existingIds.Add(entry.id);
        }

        for (int i = 0; i < defaults.Length; i++)
        {
            UpgradeDefinition defaultEntry = defaults[i];
            if (!existingIds.Contains(defaultEntry.id))
            {
                kept.Add(defaultEntry);
            }
        }

        upgrades = kept.Count > 0 ? kept.ToArray() : defaults;
    }

    static bool IsSupported(UpgradeId id)
    {
        switch (id)
        {
            case UpgradeId.TitaniumPlates:
            case UpgradeId.HeavyArmor:
            case UpgradeId.LightArmor:
            case UpgradeId.ForceShield:
            case UpgradeId.ExplosiveShield:
            case UpgradeId.CombatRam:
            case UpgradeId.ReserveCore:
            case UpgradeId.JustOneMoreTime:
            case UpgradeId.ReinforcedThrusters:
            case UpgradeId.EmergencyFuel:
            case UpgradeId.StabilityThruster:
            case UpgradeId.CometTail:
            case UpgradeId.Dragonfly:
            case UpgradeId.Priorities:
            case UpgradeId.DoubleCannon:
            case UpgradeId.ExplosiveAmmo:
            case UpgradeId.Piercing:
            case UpgradeId.Automata:
            case UpgradeId.ReinforcedCannon:
            case UpgradeId.GlassCannon:
            case UpgradeId.Shrapnel:
            case UpgradeId.HomingAmmo:
            case UpgradeId.Reload:
            case UpgradeId.AceInTheHole:
            case UpgradeId.FirstShotPower:
            case UpgradeId.ExtendedMagazine:
            case UpgradeId.SmallAndBrave:
            case UpgradeId.ArtilleryAide:
            case UpgradeId.CombatMedic:
            case UpgradeId.PackLeader:
                return true;
            default:
                return false;
        }
    }

    void ResolveReferences()
    {
        if (playerHealth == null)
        {
            playerHealth = FindFirstObjectByType<PlayerHealth>();
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

    public bool Owns(UpgradeId id) => _owned.Contains(id);

    public bool TryGetDefinition(UpgradeId id, out UpgradeDefinition definition)
    {
        EnsureUpgrades();
        for (int i = 0; i < upgrades.Length; i++)
        {
            UpgradeDefinition entry = upgrades[i];
            if (entry != null && entry.id == id)
            {
                definition = entry;
                return true;
            }
        }

        definition = null;
        return false;
    }

    public bool TryApply(UpgradeId id)
    {
        if (_owned.Contains(id) || !TryGetDefinition(id, out UpgradeDefinition definition))
        {
            return false;
        }

        if (!MeetsPrerequisites(definition))
        {
            return false;
        }

        ResolveReferences();
        ApplyEffect(definition);
        _owned.Add(id);
        return true;
    }

    public void CollectAvailable(List<UpgradeDefinition> buffer)
    {
        buffer.Clear();
        EnsureUpgrades();

        for (int i = 0; i < upgrades.Length; i++)
        {
            UpgradeDefinition entry = upgrades[i];
            if (entry == null || _owned.Contains(entry.id) || !MeetsPrerequisites(entry))
            {
                continue;
            }

            buffer.Add(entry);
        }
    }

    bool MeetsPrerequisites(UpgradeDefinition definition)
    {
        if (definition.prerequisites == null || definition.prerequisites.Length == 0)
        {
            return true;
        }

        for (int i = 0; i < definition.prerequisites.Length; i++)
        {
            if (!_owned.Contains(definition.prerequisites[i]))
            {
                return false;
            }
        }

        return true;
    }

    void ApplyEffect(UpgradeDefinition definition)
    {
        if (definition.grantForceShield)
        {
            float cooldown = definition.forceShieldCooldownSeconds > 0f
                ? definition.forceShieldCooldownSeconds
                : 10f;
            GrantForceShield(cooldown);
        }

        if (definition.shieldExplosionRadius > 0f && definition.shieldExplosionDamage > 0)
        {
            GrantExplosiveShield(definition.shieldExplosionRadius, definition.shieldExplosionDamage);
        }

        if (definition.grantCombatRam)
        {
            GrantCombatRam(definition.combatRamHealthFraction);
        }

        if (definition.grantResurrection)
        {
            playerHealth?.AddResurrectionCharges(
                Mathf.Max(1, definition.resurrectionCharges),
                Mathf.Max(1, definition.resurrectionHealAmount));
        }

        ApplyReversibleCombatEffects(definition, mergeWithExisting: false);

        if (definition.grantCometTail && playerMovement != null)
        {
            GrantCometTail(
                definition.cometTailRadius,
                definition.cometTailDamage,
                definition.cometTailSpacing);
        }

        if (definition.droneCount > 0 && definition.droneRole != AllyDroneRole.None)
        {
            AddDrones(definition.droneCount, definition.droneRole);
        }

        if (definition.grantPackLeader)
        {
            GrantPackLeader();
        }
    }

    /// <summary>
    /// Shared combat-stat application for permanent picks and temporary metamorphosis buffs.
    /// When <paramref name="mergeWithExisting"/> is true, projectile/pierce/explosion use max-merge
    /// so temporary rolls do not wipe stronger permanent values.
    /// </summary>
    void ApplyReversibleCombatEffects(UpgradeDefinition definition, bool mergeWithExisting)
    {
        if (definition.maxHealthDelta != 0)
        {
            playerHealth?.ModifyMaxHealth(definition.maxHealthDelta);
        }

        if (playerMovement != null)
        {
            if (!Mathf.Approximately(definition.moveSpeedMultiplier, 1f)
                && definition.moveSpeedMultiplier > 0f)
            {
                playerMovement.SetMoveSpeedMultiplier(
                    playerMovement.MoveSpeedMultiplier * definition.moveSpeedMultiplier);
            }

            if (definition.instantMovement)
            {
                playerMovement.SetInstantMovement(true);
            }
            else if (definition.movementResponsivenessMultiplier > 1f)
            {
                playerMovement.SetMovementResponsiveness(definition.movementResponsivenessMultiplier);
            }
        }

        if (definition.emergencyFuelMaxBonus > 0f)
        {
            _emergencyFuel = true;
            _emergencyFuelMaxBonus = definition.emergencyFuelMaxBonus;
            playerMovement?.SetEmergencyFuelEnabled(true);
            RefreshEmergencyFuel();
        }

        if (playerShooting == null)
        {
            return;
        }

        if (definition.projectileCount > 1)
        {
            int count = mergeWithExisting
                ? Mathf.Max(playerShooting.ProjectileCount, definition.projectileCount)
                : definition.projectileCount;
            playerShooting.SetProjectileCount(count);
        }

        if (definition.projectileDamageBonus != 0)
        {
            playerShooting.AddProjectileDamage(definition.projectileDamageBonus);
        }

        if (definition.pierceCount > 0)
        {
            int pierce = mergeWithExisting
                ? Mathf.Max(playerShooting.PierceCount, definition.pierceCount)
                : definition.pierceCount;
            playerShooting.SetPierceCount(pierce);
        }

        if (definition.explosionRadius > 0f || definition.explosionDamage > 0)
        {
            float radius = mergeWithExisting
                ? Mathf.Max(playerShooting.ExplosionRadius, definition.explosionRadius)
                : definition.explosionRadius;
            int damage = mergeWithExisting
                ? Mathf.Max(playerShooting.ExplosionDamage, definition.explosionDamage)
                : definition.explosionDamage;
            playerShooting.SetExplosion(radius, damage);
        }

        if (definition.grantShrapnel)
        {
            playerShooting.SetShrapnel(
                definition.shrapnelCount,
                definition.shrapnelSpeed,
                definition.shrapnelDamage);
        }

        if (definition.magazineSizeDelta != 0)
        {
            playerShooting.AddMagazineSize(definition.magazineSizeDelta);
        }

        if (!Mathf.Approximately(definition.magazineCooldownMultiplier, 1f)
            && definition.magazineCooldownMultiplier > 0f)
        {
            playerShooting.MultiplyMagazineCooldown(definition.magazineCooldownMultiplier);
        }

        if (definition.firstShotDamageBonus > 0)
        {
            playerShooting.AddFirstShotDamageBonus(definition.firstShotDamageBonus);
        }

        if (definition.lastShotDamageBonus > 0)
        {
            playerShooting.AddLastShotDamageBonus(definition.lastShotDamageBonus);
        }

        if (definition.grantHoming)
        {
            playerShooting.SetHoming(definition.homingTurnRate, definition.homingRange);
        }
    }

    void GrantForceShield(float cooldownSeconds)
    {
        if (playerHealth == null)
        {
            return;
        }

        if (!playerHealth.TryGetComponent(out PlayerForceShield shield))
        {
            shield = playerHealth.gameObject.AddComponent<PlayerForceShield>();
        }

        shield.Enable(cooldownSeconds);

        if (!playerHealth.TryGetComponent(out ForceShieldVisual visual))
        {
            visual = playerHealth.gameObject.AddComponent<ForceShieldVisual>();
        }

        visual.Bind(shield);
    }

    void GrantExplosiveShield(float radius, int damage)
    {
        if (playerHealth == null)
        {
            return;
        }

        if (!playerHealth.TryGetComponent(out PlayerExplosiveShield explosiveShield))
        {
            explosiveShield = playerHealth.gameObject.AddComponent<PlayerExplosiveShield>();
        }

        explosiveShield.Configure(radius, damage);
    }

    void GrantCombatRam(float healthFraction)
    {
        if (playerHealth == null)
        {
            return;
        }

        if (!playerHealth.TryGetComponent(out PlayerCombatRam combatRam))
        {
            combatRam = playerHealth.gameObject.AddComponent<PlayerCombatRam>();
        }

        combatRam.Configure(playerHealth, healthFraction);
    }

    void GrantCometTail(float radius, int damage, float spacing)
    {
        if (playerMovement == null)
        {
            return;
        }

        if (!playerMovement.TryGetComponent(out PlayerCometTail cometTail))
        {
            cometTail = playerMovement.gameObject.AddComponent<PlayerCometTail>();
        }

        cometTail.Configure(playerMovement.transform, radius, damage, spacing);
    }

    void HandleHealthChanged(int current, int max)
    {
        if (_emergencyFuel)
        {
            RefreshEmergencyFuel();
        }
    }

    void RefreshEmergencyFuel()
    {
        if (playerHealth == null || playerMovement == null)
        {
            return;
        }

        playerMovement.SetEmergencyFuelBonus(playerHealth.MissingHealth01 * _emergencyFuelMaxBonus);
    }

    void AddDrones(int count, AllyDroneRole role)
    {
        if (playerMovement == null || role == AllyDroneRole.None)
        {
            return;
        }

        count = Mathf.Max(1, count);
        int existing = _drones.Count;

        for (int i = 0; i < count; i++)
        {
            var droneObject = new GameObject($"AllyDrone_{role}_{existing + i}");
            droneObject.transform.SetParent(null, false);
            var drone = droneObject.AddComponent<AllyDrone>();
            drone.Initialize(
                playerMovement.transform,
                playerShooting,
                playerHealth,
                0f,
                role);
            drone.SetPowerTier(_dronePowerTier);
            _drones.Add(drone);
        }

        RedistributeOrbitPhases();
    }

    /// <summary>
    /// Spawns a copy of every currently alive automaton. Returns the drones created by this call.
    /// </summary>
    public List<AllyDrone> DuplicateOwnedDrones()
    {
        var created = new List<AllyDrone>();
        if (playerMovement == null)
        {
            return created;
        }

        var snapshot = new List<(AllyDroneRole role, AllyDrone source)>();
        for (int i = 0; i < _drones.Count; i++)
        {
            AllyDrone drone = _drones[i];
            if (drone != null)
            {
                snapshot.Add((drone.Role, drone));
            }
        }

        for (int i = 0; i < snapshot.Count; i++)
        {
            AllyDroneRole role = snapshot[i].role;
            var droneObject = new GameObject($"AllyDrone_{role}_Clone_{_drones.Count}");
            droneObject.transform.SetParent(null, false);
            var drone = droneObject.AddComponent<AllyDrone>();
            drone.Initialize(
                playerMovement.transform,
                playerShooting,
                playerHealth,
                0f,
                role);
            drone.SetPowerTier(_dronePowerTier);
            _drones.Add(drone);
            created.Add(drone);
        }

        RedistributeOrbitPhases();
        return created;
    }

    /// <summary>Removes and destroys drones previously returned by duplication helpers.</summary>
    public void RemoveDrones(IReadOnlyList<AllyDrone> drones)
    {
        if (drones == null || drones.Count == 0)
        {
            return;
        }

        for (int i = 0; i < drones.Count; i++)
        {
            AllyDrone drone = drones[i];
            if (drone == null)
            {
                continue;
            }

            _drones.Remove(drone);
            Destroy(drone.gameObject);
        }

        RedistributeOrbitPhases();
    }

    /// <summary>
    /// Applies a reversible combat snapshot of an upgrade for temporary buffs.
    /// Skips drones, shields, resurrection, comet tail, combat ram, and pack leader.
    /// </summary>
    public bool TryApplyTemporaryUpgrade(UpgradeDefinition definition, out TemporaryUpgradeSnapshot snapshot)
    {
        snapshot = default;
        if (definition == null || !CanApplyTemporarily(definition))
        {
            return false;
        }

        ResolveReferences();
        snapshot = CaptureSnapshot();
        ApplyTemporaryCombatEffects(definition);
        return true;
    }

    public void RestoreTemporaryUpgrade(in TemporaryUpgradeSnapshot snapshot)
    {
        ResolveReferences();
        RestoreHealthExact(snapshot.CurrentHealth, snapshot.MaxHealth);

        if (playerMovement != null)
        {
            playerMovement.SetMoveSpeedMultiplier(snapshot.MoveSpeedMultiplier);
            playerMovement.SetEmergencyFuelEnabled(snapshot.EmergencyFuelEnabled);
            playerMovement.SetEmergencyFuelBonus(snapshot.EmergencyFuelBonus);
            playerMovement.SetMovementResponsiveness(snapshot.MovementResponsiveness);
            playerMovement.SetInstantMovement(snapshot.InstantMovement);
        }

        if (playerShooting != null)
        {
            playerShooting.SetProjectileCount(snapshot.ProjectileCount);
            playerShooting.SetProjectileDamage(snapshot.ProjectileDamage);
            playerShooting.SetPierceCount(snapshot.PierceCount);
            playerShooting.SetExplosion(snapshot.ExplosionRadius, snapshot.ExplosionDamage);
            playerShooting.SetShrapnel(snapshot.ShrapnelCount, snapshot.ShrapnelSpeed, snapshot.ShrapnelDamage);
            int magDelta = snapshot.MagazineSize - playerShooting.MagazineSize;
            if (magDelta != 0)
            {
                playerShooting.AddMagazineSize(magDelta);
            }

            playerShooting.SetMagazineCooldown(snapshot.MagazineCooldown);
            RestoreShotBonuses(snapshot);
        }

        _emergencyFuel = snapshot.ControllerEmergencyFuel;
        _emergencyFuelMaxBonus = snapshot.ControllerEmergencyFuelMaxBonus;
        if (_emergencyFuel)
        {
            RefreshEmergencyFuel();
        }
        else
        {
            playerMovement?.SetEmergencyFuelEnabled(false);
        }
    }

    public void CollectTemporaryEligible(List<UpgradeDefinition> buffer)
    {
        buffer.Clear();
        EnsureUpgrades();

        for (int i = 0; i < upgrades.Length; i++)
        {
            UpgradeDefinition entry = upgrades[i];
            if (entry != null && CanApplyTemporarily(entry))
            {
                buffer.Add(entry);
            }
        }
    }

    void RestoreHealthExact(int current, int max)
    {
        if (playerHealth == null)
        {
            return;
        }

        playerHealth.SetMaxHealth(Mathf.Max(1, max), refill: false);
        int needed = Mathf.Clamp(current, 0, playerHealth.MaxHealth) - playerHealth.CurrentHealth;
        if (needed > 0)
        {
            playerHealth.Heal(needed);
        }
        else if (needed < 0)
        {
            playerHealth.ForceSetCurrentHealth(playerHealth.CurrentHealth + needed);
        }
    }

    void RestoreShotBonuses(in TemporaryUpgradeSnapshot snapshot)
    {
        // Re-sync bonuses by clearing through relative adjust is awkward; expose setters used here.
        playerShooting.SetFirstShotDamageBonus(snapshot.FirstShotDamageBonus);
        playerShooting.SetLastShotDamageBonus(snapshot.LastShotDamageBonus);
        if (snapshot.HomingEnabled)
        {
            playerShooting.SetHoming(snapshot.HomingTurnRate, snapshot.HomingRange);
        }
        else
        {
            playerShooting.ClearHoming();
        }
    }

    static bool CanApplyTemporarily(UpgradeDefinition definition)
    {
        if (definition.droneCount > 0
            || definition.grantForceShield
            || definition.shieldExplosionRadius > 0f
            || definition.grantCombatRam
            || definition.grantResurrection
            || definition.grantCometTail
            || definition.grantPackLeader)
        {
            return false;
        }

        return true;
    }

    TemporaryUpgradeSnapshot CaptureSnapshot()
    {
        var snapshot = new TemporaryUpgradeSnapshot
        {
            ControllerEmergencyFuel = _emergencyFuel,
            ControllerEmergencyFuelMaxBonus = _emergencyFuelMaxBonus,
        };

        if (playerHealth != null)
        {
            snapshot.MaxHealth = playerHealth.MaxHealth;
            snapshot.CurrentHealth = playerHealth.CurrentHealth;
        }

        if (playerMovement != null)
        {
            snapshot.MoveSpeedMultiplier = playerMovement.MoveSpeedMultiplier;
            snapshot.EmergencyFuelEnabled = playerMovement.EmergencyFuelEnabled;
            snapshot.EmergencyFuelBonus = playerMovement.EmergencyFuelBonus;
            snapshot.MovementResponsiveness = playerMovement.MovementResponsiveness;
            snapshot.InstantMovement = playerMovement.InstantMovement;
        }

        if (playerShooting != null)
        {
            snapshot.ProjectileCount = playerShooting.ProjectileCount;
            snapshot.ProjectileDamage = playerShooting.ProjectileDamage;
            snapshot.PierceCount = playerShooting.PierceCount;
            snapshot.ExplosionRadius = playerShooting.ExplosionRadius;
            snapshot.ExplosionDamage = playerShooting.ExplosionDamage;
            snapshot.ShrapnelCount = playerShooting.ShrapnelCount;
            snapshot.ShrapnelSpeed = playerShooting.ShrapnelSpeed;
            snapshot.ShrapnelDamage = playerShooting.ShrapnelDamage;
            snapshot.MagazineSize = playerShooting.MagazineSize;
            snapshot.MagazineCooldown = playerShooting.MagazineCooldown;
            snapshot.FirstShotDamageBonus = playerShooting.FirstShotDamageBonus;
            snapshot.LastShotDamageBonus = playerShooting.LastShotDamageBonus;
            snapshot.HomingEnabled = playerShooting.HomingEnabled;
            snapshot.HomingTurnRate = playerShooting.HomingTurnRate;
            snapshot.HomingRange = playerShooting.HomingRange;
        }

        return snapshot;
    }

    void ApplyTemporaryCombatEffects(UpgradeDefinition definition)
    {
        ApplyReversibleCombatEffects(definition, mergeWithExisting: true);
    }

    public struct TemporaryUpgradeSnapshot
    {
        public int MaxHealth;
        public int CurrentHealth;
        public float MoveSpeedMultiplier;
        public bool EmergencyFuelEnabled;
        public float EmergencyFuelBonus;
        public float MovementResponsiveness;
        public bool InstantMovement;
        public int ProjectileCount;
        public int ProjectileDamage;
        public int PierceCount;
        public float ExplosionRadius;
        public int ExplosionDamage;
        public int ShrapnelCount;
        public float ShrapnelSpeed;
        public int ShrapnelDamage;
        public int MagazineSize;
        public float MagazineCooldown;
        public int FirstShotDamageBonus;
        public int LastShotDamageBonus;
        public bool HomingEnabled;
        public float HomingTurnRate;
        public float HomingRange;
        public bool ControllerEmergencyFuel;
        public float ControllerEmergencyFuelMaxBonus;
    }

    void GrantPackLeader()
    {
        _dronePowerTier = Mathf.Max(_dronePowerTier, 1);
        for (int i = 0; i < _drones.Count; i++)
        {
            if (_drones[i] != null)
            {
                _drones[i].SetPowerTier(_dronePowerTier);
            }
        }
    }

    void RedistributeOrbitPhases()
    {
        int count = 0;
        for (int i = 0; i < _drones.Count; i++)
        {
            if (_drones[i] != null)
            {
                count++;
            }
        }

        if (count <= 0)
        {
            return;
        }

        int index = 0;
        for (int i = 0; i < _drones.Count; i++)
        {
            AllyDrone drone = _drones[i];
            if (drone == null)
            {
                continue;
            }

            drone.SetOrbitPhase((Mathf.PI * 2f / count) * index);
            index++;
        }
    }

    void ClearDrones()
    {
        for (int i = 0; i < _drones.Count; i++)
        {
            if (_drones[i] != null)
            {
                Destroy(_drones[i].gameObject);
            }
        }

        _drones.Clear();
    }

    void OnDestroy()
    {
        ClearDrones();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        EnsureUpgrades();
    }
#endif
}

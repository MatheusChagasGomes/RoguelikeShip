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

    public IReadOnlyCollection<UpgradeId> OwnedUpgrades => _owned;

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
            case UpgradeId.ReinforcedThrusters:
            case UpgradeId.EmergencyFuel:
            case UpgradeId.StabilityThruster:
            case UpgradeId.CometTail:
            case UpgradeId.Dragonfly:
            case UpgradeId.DoubleCannon:
            case UpgradeId.ExplosiveAmmo:
            case UpgradeId.Piercing:
            case UpgradeId.Automata:
            case UpgradeId.ReinforcedCannon:
            case UpgradeId.GlassCannon:
            case UpgradeId.Shrapnel:
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
        if (definition.maxHealthDelta != 0)
        {
            playerHealth?.ModifyMaxHealth(definition.maxHealthDelta);
        }

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

            if (definition.grantCometTail)
            {
                GrantCometTail(
                    definition.cometTailRadius,
                    definition.cometTailDamage,
                    definition.cometTailSpacing);
            }
        }

        if (definition.emergencyFuelMaxBonus > 0f)
        {
            _emergencyFuel = true;
            _emergencyFuelMaxBonus = definition.emergencyFuelMaxBonus;
            playerMovement?.SetEmergencyFuelEnabled(true);
            RefreshEmergencyFuel();
        }

        if (playerShooting != null)
        {
            if (definition.projectileCount > 1)
            {
                playerShooting.SetProjectileCount(definition.projectileCount);
            }

            if (definition.projectileDamageBonus != 0)
            {
                playerShooting.AddProjectileDamage(definition.projectileDamageBonus);
            }

            if (definition.pierceCount > 0)
            {
                playerShooting.SetPierceCount(definition.pierceCount);
            }

            if (definition.explosionRadius > 0f || definition.explosionDamage > 0)
            {
                playerShooting.SetExplosion(definition.explosionRadius, definition.explosionDamage);
            }

            if (definition.grantShrapnel)
            {
                playerShooting.SetShrapnel(
                    definition.shrapnelCount,
                    definition.shrapnelSpeed,
                    definition.shrapnelDamage);
            }
        }

        if (definition.droneCount > 0)
        {
            SpawnAutomata(definition.droneCount);
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

    void SpawnAutomata(int count)
    {
        if (playerMovement == null)
        {
            return;
        }

        ClearDrones();
        count = Mathf.Max(1, count);
        for (int i = 0; i < count; i++)
        {
            var droneObject = new GameObject($"AllyDrone_{i}");
            droneObject.transform.SetParent(null, false);
            var drone = droneObject.AddComponent<AllyDrone>();
            float phase = (Mathf.PI * 2f / count) * i;
            drone.Initialize(playerMovement.transform, playerShooting, phase);
            _drones.Add(drone);
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

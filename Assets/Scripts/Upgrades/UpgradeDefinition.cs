using System;
using UnityEngine;

/// <summary>
/// One upgrade entry. Edit display text and balance values in the Inspector.
/// Only the fields relevant to each upgrade id are used at runtime.
/// </summary>
[Serializable]
public class UpgradeDefinition
{
    [Header("Identity")]
    public UpgradeId id;
    public UpgradeCategory category;
    public string displayName = "Upgrade";
    [TextArea(2, 4)] public string description;
    [Tooltip("All listed upgrades must be owned before this one can appear.")]
    public UpgradeId[] prerequisites;

    [Header("Hull")]
    [Tooltip("Added to max health (negative reduces). Used by titanium / armor upgrades.")]
    public int maxHealthDelta;
    [Tooltip("If true, grants a regenerating force shield.")]
    public bool grantForceShield;
    [Tooltip("Seconds before the force shield reactivates after absorbing a hit.")]
    [Min(0f)] public float forceShieldCooldownSeconds = 10f;
    [Tooltip("Shield break AoE radius (explosive shield).")]
    [Min(0f)] public float shieldExplosionRadius;
    [Tooltip("Shield break AoE damage (explosive shield).")]
    [Min(0)] public int shieldExplosionDamage;
    [Tooltip("Enables ramming enemies for max-health-based damage without taking collision damage.")]
    public bool grantCombatRam;
    [Tooltip("Ram damage = max(1, floor(maxHealth * this fraction)).")]
    [Range(0.01f, 1f)] public float combatRamHealthFraction = 0.1f;

    [Header("Movement")]
    [Tooltip("Multiplies followSpeed (movement speed). 1 = unchanged.")]
    public float moveSpeedMultiplier = 1f;
    [Tooltip("Max followSpeed bonus at 0 HP (emergency fuel).")]
    public float emergencyFuelMaxBonus;
    [Tooltip("Multiplies how quickly the ship catches up to the finger (stability thruster).")]
    [Min(1f)] public float movementResponsivenessMultiplier = 1f;
    [Tooltip("If true, the ship snaps instantly to the target position (dragonfly).")]
    public bool instantMovement;
    [Tooltip("Spawns damaging fire segments along the flight path (comet tail).")]
    public bool grantCometTail;
    [Min(0f)] public float cometTailRadius = 0.55f;
    [Min(0)] public int cometTailDamage = 1;
    [Min(0.05f)] public float cometTailSpacing = 0.22f;

    [Header("Weapons")]
    [Tooltip("Projectile count while firing (double cannon).")]
    [Min(1)] public int projectileCount = 1;
    [Tooltip("Flat damage added to player projectiles.")]
    public int projectileDamageBonus;
    [Tooltip("How many enemies a projectile can pierce through.")]
    [Min(0)] public int pierceCount;
    [Tooltip("Explosion radius on hit (0 = none).")]
    [Min(0f)] public float explosionRadius;
    [Tooltip("Damage applied to enemies inside the explosion radius (not the direct hit).")]
    [Min(0)] public int explosionDamage;
    [Tooltip("Ally drones to spawn (automata).")]
    [Min(0)] public int droneCount;
    [Tooltip("Debris spawned when an explosive projectile detonates (shrapnel).")]
    public bool grantShrapnel;
    [Min(0)] public int shrapnelCount;
    [Min(0f)] public float shrapnelSpeed = 8f;
    [Min(0)] public int shrapnelDamage = 1;

    public string CategoryLabel => category switch
    {
        UpgradeCategory.Hull => "Casco",
        UpgradeCategory.Engine => "Motor",
        UpgradeCategory.Weapons => "Armas",
        _ => category.ToString(),
    };

    public static UpgradeDefinition[] CreateDefaults()
    {
        return new[]
        {
            new UpgradeDefinition
            {
                id = UpgradeId.TitaniumPlates,
                category = UpgradeCategory.Hull,
                displayName = "Placas de titânio",
                description = "Aumenta a vida máxima.",
                maxHealthDelta = 20,
            },
            new UpgradeDefinition
            {
                id = UpgradeId.HeavyArmor,
                category = UpgradeCategory.Hull,
                displayName = "Blindagem pesada",
                description = "Aumenta muito a vida máxima, mas diminui a velocidade.",
                maxHealthDelta = 40,
                moveSpeedMultiplier = 0.8f,
            },
            new UpgradeDefinition
            {
                id = UpgradeId.LightArmor,
                category = UpgradeCategory.Hull,
                displayName = "Blindagem leve",
                description = "Diminui um pouco a vida máxima, mas aumenta a velocidade.",
                maxHealthDelta = -15,
                moveSpeedMultiplier = 1.25f,
            },
            new UpgradeDefinition
            {
                id = UpgradeId.ForceShield,
                category = UpgradeCategory.Hull,
                displayName = "Escudo de força",
                description = "Ignora o primeiro dano recebido. Recarrega após um cooldown.",
                grantForceShield = true,
                forceShieldCooldownSeconds = 10f,
            },
            new UpgradeDefinition
            {
                id = UpgradeId.ExplosiveShield,
                category = UpgradeCategory.Hull,
                displayName = "Escudo explosivo",
                description = "Ao perder o escudo, ele explode causando dano em área.",
                prerequisites = new[] { UpgradeId.ForceShield },
                shieldExplosionRadius = 1.5f,
                shieldExplosionDamage = 2,
            },
            new UpgradeDefinition
            {
                id = UpgradeId.CombatRam,
                category = UpgradeCategory.Hull,
                displayName = "Máquina de combate",
                description = "Causa dano em inimigos ao colidir e colidir não causa dano ao jogador. O dano é baseado na vida máxima.",
                prerequisites = new[] { UpgradeId.TitaniumPlates, UpgradeId.HeavyArmor },
                grantCombatRam = true,
                combatRamHealthFraction = 0.1f,
            },
            new UpgradeDefinition
            {
                id = UpgradeId.ReinforcedThrusters,
                category = UpgradeCategory.Engine,
                displayName = "Propulsores reforçados",
                description = "Aumenta a velocidade de movimento.",
                moveSpeedMultiplier = 1.25f,
            },
            new UpgradeDefinition
            {
                id = UpgradeId.EmergencyFuel,
                category = UpgradeCategory.Engine,
                displayName = "Combustível de emergência",
                description = "Aumenta a velocidade conforme a vida é perdida.",
                emergencyFuelMaxBonus = 0.5f,
            },
            new UpgradeDefinition
            {
                id = UpgradeId.StabilityThruster,
                category = UpgradeCategory.Engine,
                displayName = "Propulsor de estabilidade",
                description = "Reduz o atraso entre o toque e o movimento da nave.",
                movementResponsivenessMultiplier = 4f,
            },
            new UpgradeDefinition
            {
                id = UpgradeId.CometTail,
                category = UpgradeCategory.Engine,
                displayName = "Cauda de cometa",
                description = "Deixa um rastro de fogo por onde passar, causando dano.",
                prerequisites = new[] { UpgradeId.ReinforcedThrusters },
                grantCometTail = true,
                cometTailRadius = 0.55f,
                cometTailDamage = 1,
                cometTailSpacing = 0.22f,
            },
            new UpgradeDefinition
            {
                id = UpgradeId.Dragonfly,
                category = UpgradeCategory.Engine,
                displayName = "Libélula",
                description = "Delay de movimento zerado.",
                prerequisites = new[] { UpgradeId.StabilityThruster },
                instantMovement = true,
            },
            new UpgradeDefinition
            {
                id = UpgradeId.DoubleCannon,
                category = UpgradeCategory.Weapons,
                displayName = "Canhão duplo",
                description = "Atira 2 projéteis ao invés de apenas 1.",
                projectileCount = 2,
            },
            new UpgradeDefinition
            {
                id = UpgradeId.ExplosiveAmmo,
                category = UpgradeCategory.Weapons,
                displayName = "Munição explosiva",
                description = "Projéteis explodem ao contato, causando dano em área.",
                explosionRadius = 1.25f,
                explosionDamage = 1,
            },
            new UpgradeDefinition
            {
                id = UpgradeId.Piercing,
                category = UpgradeCategory.Weapons,
                displayName = "Perfuração",
                description = "Projéteis perfuram 1 inimigo.",
                pierceCount = 1,
            },
            new UpgradeDefinition
            {
                id = UpgradeId.Automata,
                category = UpgradeCategory.Weapons,
                displayName = "Autômatos",
                description = "Drones auxiliares que atiram nos inimigos.",
                droneCount = 2,
            },
            new UpgradeDefinition
            {
                id = UpgradeId.ReinforcedCannon,
                category = UpgradeCategory.Weapons,
                displayName = "Canhão reforçado",
                description = "Aumenta o dano do projétil.",
                projectileDamageBonus = 1,
            },
            new UpgradeDefinition
            {
                id = UpgradeId.GlassCannon,
                category = UpgradeCategory.Weapons,
                displayName = "Canhão de vidro",
                description = "Aumenta muito o dano, mas diminui muito a vida máxima.",
                prerequisites = new[] { UpgradeId.LightArmor, UpgradeId.ReinforcedCannon },
                maxHealthDelta = -25,
                projectileDamageBonus = 3,
            },
            new UpgradeDefinition
            {
                id = UpgradeId.Shrapnel,
                category = UpgradeCategory.Weapons,
                displayName = "Estilhaços",
                description = "Quando o projétil explodir no alvo, uma onda de destroços é arremessada, causando dano em inimigos.",
                prerequisites = new[] { UpgradeId.ExplosiveAmmo },
                grantShrapnel = true,
                shrapnelCount = 6,
                shrapnelSpeed = 9f,
                shrapnelDamage = 1,
            },
        };
    }
}

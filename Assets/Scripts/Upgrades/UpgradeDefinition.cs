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

    [Header("Hull")]
    [Tooltip("Added to max health (negative reduces). Used by titanium / armor upgrades.")]
    public int maxHealthDelta;
    [Tooltip("If true, grants a regenerating force shield.")]
    public bool grantForceShield;
    [Tooltip("Seconds before the force shield reactivates after absorbing a hit.")]
    [Min(0f)] public float forceShieldCooldownSeconds = 10f;

    [Header("Movement")]
    [Tooltip("Multiplies followSpeed (movement speed). 1 = unchanged.")]
    public float moveSpeedMultiplier = 1f;
    [Tooltip("Max followSpeed bonus at 0 HP (emergency fuel).")]
    public float emergencyFuelMaxBonus;

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
        };
    }
}

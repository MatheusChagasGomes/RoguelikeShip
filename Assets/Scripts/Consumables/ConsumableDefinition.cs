using System;
using UnityEngine;

/// <summary>
/// One consumable entry. Edit display text in the Inspector; effects are applied by id.
/// </summary>
[Serializable]
public class ConsumableDefinition
{
    [Header("Identity")]
    public ConsumableId id;
    public ConsumableRarity rarity;
    public string displayName = "Consumível";
    [TextArea(2, 4)] public string description;

    public string RarityLabel => rarity switch
    {
        ConsumableRarity.Common => "Comum",
        ConsumableRarity.Rare => "Raro",
        ConsumableRarity.UltraRare => "Ultra-raro",
        _ => rarity.ToString(),
    };

    public static ConsumableDefinition[] CreateDefaults()
    {
        return new[]
        {
            new ConsumableDefinition
            {
                id = ConsumableId.GoldenHammer,
                rarity = ConsumableRarity.Common,
                displayName = "Martelinho de ouro",
                description = "Cura o jogador em 20 de vida.",
            },
            new ConsumableDefinition
            {
                id = ConsumableId.SpareChangeAmmo,
                rarity = ConsumableRarity.Common,
                displayName = "Troco em bala",
                description = "Até completar o loop, recebe +2 de capacidade do pente.",
            },
            new ConsumableDefinition
            {
                id = ConsumableId.RepairKit,
                rarity = ConsumableRarity.Common,
                displayName = "Kit de reparo",
                description = "Recupera 10 de vida nas próximas 2 vezes que o jogador receber dano.",
            },
            new ConsumableDefinition
            {
                id = ConsumableId.AuxiliaryCooler,
                rarity = ConsumableRarity.Common,
                displayName = "Refrigerador auxiliar",
                description = "Até completar o loop, diminui em 1s o tempo de recarga do pente.",
            },
            new ConsumableDefinition
            {
                id = ConsumableId.EmergencyRepair,
                rarity = ConsumableRarity.Common,
                displayName = "Reparo emergencial",
                description = "Ao ficar com menos de 30% de vida, cura 30 pontos de vida.",
            },
            new ConsumableDefinition
            {
                id = ConsumableId.ReserveCartridge,
                rarity = ConsumableRarity.Common,
                displayName = "Cartucho reserva",
                description = "O próximo pente é recarregado instantaneamente.",
            },
            new ConsumableDefinition
            {
                id = ConsumableId.NotToday,
                rarity = ConsumableRarity.Rare,
                displayName = "Hoje não",
                description = "Até completar o loop, caso o jogador morra, volta com 20 pontos de vida.",
            },
            new ConsumableDefinition
            {
                id = ConsumableId.CarefulRepair,
                rarity = ConsumableRarity.Rare,
                displayName = "Reparo cuidadoso",
                description = "Cura o jogador em 50 pontos de vida.",
            },
            new ConsumableDefinition
            {
                id = ConsumableId.ExtraBullet,
                rarity = ConsumableRarity.Rare,
                displayName = "Bala extra",
                description = "Até completar o loop, recebe +4 de capacidade do pente.",
            },
            new ConsumableDefinition
            {
                id = ConsumableId.WalkingMetamorphosis,
                rarity = ConsumableRarity.Rare,
                displayName = "Metamorfose ambulante",
                description = "Até completar o loop, a cada 5 segundos, recebe um aprimoramento aleatório temporariamente.",
            },
            new ConsumableDefinition
            {
                id = ConsumableId.GreaterCooler,
                rarity = ConsumableRarity.Rare,
                displayName = "Refrigerador maior",
                description = "Até completar o loop, diminui em 2s o tempo de recarga do pente.",
            },
            new ConsumableDefinition
            {
                id = ConsumableId.Duplicator,
                rarity = ConsumableRarity.UltraRare,
                displayName = "Duplicador",
                description = "Até completar o loop, duplica seus tiros.",
            },
            new ConsumableDefinition
            {
                id = ConsumableId.Legion,
                rarity = ConsumableRarity.UltraRare,
                displayName = "Legião",
                description = "Até completar o loop, duplica a quantidade de autômatos.",
            },
            new ConsumableDefinition
            {
                id = ConsumableId.ExperiencedTinker,
                rarity = ConsumableRarity.UltraRare,
                displayName = "Funileiro experiente",
                description = "Cura o jogador em 200 pontos de vida.",
            },
            new ConsumableDefinition
            {
                id = ConsumableId.InfiniteMagazine,
                rarity = ConsumableRarity.UltraRare,
                displayName = "Pente infinito",
                description = "Até completar o loop, zera o cooldown de recarga dos tiros.",
            },
            new ConsumableDefinition
            {
                id = ConsumableId.TimeTravel,
                rarity = ConsumableRarity.UltraRare,
                displayName = "Viagem no tempo",
                description = "Até completar o loop, caso o jogador morra, volta com a mesma quantidade de vida que começou o loop atual.",
            },
        };
    }
}

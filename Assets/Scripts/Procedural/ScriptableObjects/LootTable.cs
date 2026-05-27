// ScriptableObjects/LootTable.cs
using UnityEngine;
using System.Collections.Generic;

public enum LootRarity { Common, Uncommon, Rare, Epic, Legendary }

[System.Serializable]
public class LootEntry
{
    public string ItemName;
    public GameObject Prefab;
    public LootRarity Rarity;
    [Range(0.01f, 100f)] public float Weight;
    public int MinCount = 1;
    public int MaxCount = 1;
    [Tooltip("Solo aparece en dungeons de profundidad >= X")]
    public int MinDepth = 0;
}

[CreateAssetMenu(menuName = "Dungeon/Loot Table")]
public class LootTable : ScriptableObject
{
    public List<LootEntry> Entries;

    // Peso dinámico: a mayor profundidad, los items raros pesan más
    public LootEntry PickWeighted(System.Random rng, int depth)
    {
        var eligible = Entries.FindAll(e => e.MinDepth <= depth);
        if (eligible.Count == 0) return null;

        float total = 0f;
        foreach (var e in eligible)
            total += e.Weight * RarityDepthMultiplier(e.Rarity, depth);

        float roll = (float)rng.NextDouble() * total;
        float cumulative = 0f;

        foreach (var e in eligible)
        {
            cumulative += e.Weight * RarityDepthMultiplier(e.Rarity, depth);
            if (roll <= cumulative) return e;
        }
        return eligible[^1];
    }

    // A mayor profundidad los items raros tienen más chance
    private float RarityDepthMultiplier(LootRarity rarity, int depth)
    {
        float depthFactor = 1f + depth * 0.15f;
        return rarity switch
        {
            LootRarity.Common    => 1f,
            LootRarity.Uncommon  => depthFactor * 0.6f,
            LootRarity.Rare      => depthFactor * 0.3f,
            LootRarity.Epic      => depthFactor * 0.1f,
            LootRarity.Legendary => depthFactor * 0.03f,
            _ => 1f
        };
    }
}


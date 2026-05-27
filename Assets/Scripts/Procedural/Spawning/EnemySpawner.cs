using System.Collections.Generic;
using UnityEngine;
public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private EnemyPool pool;

    public void SpawnForRoom(RoomData room, DungeonDifficultyConfig diff, int depth, System.Random rng)
    {
        if (room.RoomType == RoomType.Start || room.RoomType == RoomType.Treasure)
            return;

        float budgetMod = room.GraphNode != null ? room.GraphNode.BudgetModifier : 1f;
        float budget    = diff.baseThreatBudget * budgetMod * (1f + depth * diff.depthScaling);

        var eligibleEnemies = new List<EnemyData>();
        foreach (var e in pool.Enemies)
            if (e.MinDepth <= depth) eligibleEnemies.Add(e);

        float remaining = budget;
        var spawnList   = new List<EnemyData>();

        while (remaining > 0)
        {
            var candidates = new List<EnemyData>();
            foreach (var e in eligibleEnemies)
                if (e.ThreatCost <= remaining) candidates.Add(e);

            if (candidates.Count == 0) break;

            var chosen = WeightedRandom(candidates, rng);
            spawnList.Add(chosen);
            remaining -= chosen.ThreatCost;
        }

        foreach (var enemy in spawnList)
        {
            var pos = GetRandomSpawnPoint(room);
            if (enemy.Prefab != null)
                Instantiate(enemy.Prefab, pos, Quaternion.identity);
        }
    }

    private EnemyData WeightedRandom(List<EnemyData> pool, System.Random rng)
    {
        float totalWeight = 0f;
        foreach (var e in pool)
            totalWeight += e.Weight;

        float roll = (float)rng.NextDouble() * totalWeight;
        float cumulative = 0f;
        foreach (var e in pool)
        {
            cumulative += e.Weight;
            if (roll <= cumulative) return e;
        }
        return pool[pool.Count - 1];
    }

    private Vector3 GetRandomSpawnPoint(RoomData room)
    {
        // Usar SpawnPoints definidos en el prefab de la room, o calcular random
        // dentro de los bounds de la sala
        return Vector3.zero; // implementar según tu setup
    }
}
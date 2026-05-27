// Spawning/LootGenerator.cs
using UnityEngine;
using System.Collections.Generic;

public class LootGenerator : MonoBehaviour
{
    private Transform lootParent;

    public void GenerateLootForRoom(RoomData room, DungeonDifficultyConfig diff,
                                     int depth, System.Random rng, LootTable table)
    {
        if (table == null) return;

        // Cuántos drops genera esta sala
        int dropCount = GetDropCountForRoom(room.RoomType, diff, rng);
        room.ResolvedLoot = new List<LootEntry>();

        for (int i = 0; i < dropCount; i++)
        {
            var entry = table.PickWeighted(rng, depth);
            if (entry == null) continue;

            int count = rng.Next(entry.MinCount, entry.MaxCount + 1);
            for (int j = 0; j < count; j++)
            {
                room.ResolvedLoot.Add(entry);

                // Instanciar en posición aleatoria dentro de los bounds
                var pos = GetRandomFloorPosition(room);
                if (entry.Prefab != null)
                    Instantiate(entry.Prefab, pos, Quaternion.identity, lootParent);
            }
        }
    }

    private int GetDropCountForRoom(RoomType type, DungeonDifficultyConfig diff,
                                     System.Random rng)
    {
        return type switch
        {
            RoomType.Start    => 0,
            RoomType.Combat   => rng.Next(0, 2),   // 0 o 1
            RoomType.Treasure => rng.Next(2, 5),   // 2-4 items
            RoomType.Elite    => rng.Next(1, 4),   // 1-3 items
            RoomType.Boss     => rng.Next(3, 7),   // 3-6 items
            _                 => 0
        };
    }

    private Vector3 GetRandomFloorPosition(RoomData room)
    {
        // Calcular posición aleatoria dentro de los bounds de la sala
        // con un margen para no poner items en las paredes
        int margin = 1;
        float x = UnityEngine.Random.Range(
            room.Bounds.x + margin,
            room.Bounds.x + room.Bounds.width - margin) * /* cellSize */ 4f;
        float z = UnityEngine.Random.Range(
            room.Bounds.y + margin,
            room.Bounds.y + room.Bounds.height - margin) * /* cellSize */ 4f;
        return new Vector3(x, 0.5f, z);
    }

    public void SetParent(Transform parent) => lootParent = parent;
    public void Clear() { if (lootParent) foreach (Transform t in lootParent) Destroy(t.gameObject); }
}
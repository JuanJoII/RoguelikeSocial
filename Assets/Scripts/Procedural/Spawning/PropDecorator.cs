using UnityEngine;
using System.Collections.Generic;

public class PropDecorator : MonoBehaviour
{
    private Transform propParent;

    public void DecorateRoom(RoomData room, BiomeConfig biome,
                              DungeonGrid grid, System.Random rng)
    {
        if (biome?.PropCollection == null) return;

        var eligibleProps = biome.PropCollection.GetForRoomType(room.RoomType);
        if (eligibleProps.Count == 0) return;

        // Número de props según tamaño de la sala
        int roomArea = room.Bounds.width * room.Bounds.height;
        int maxProps = Mathf.Clamp(roomArea / 4, 1, 8);
        int propCount = rng.Next(1, maxProps + 1);

        for (int i = 0; i < propCount; i++)
        {
            var prop = WeightedPickProp(eligibleProps, rng);
            if (prop == null) continue;

            // Respeta SpawnChance
            if (rng.NextDouble() > prop.SpawnChance) continue;

            Vector3 pos = GetPositionForRule(prop.PlacementRule, room, grid);
            Quaternion rot = Quaternion.Euler(0, rng.Next(0, 4) * 90f, 0);

            if (prop.Prefab != null)
                Instantiate(prop.Prefab, pos, rot, propParent);
        }
    }

    private PropData WeightedPickProp(List<PropData> pool, System.Random rng)
    {
        float total = 0f;
        foreach (var p in pool) total += p.Weight;

        float roll = (float)rng.NextDouble() * total;
        float cum  = 0f;
        foreach (var p in pool)
        {
            cum += p.Weight;
            if (roll <= cum) return p;
        }
        return pool[^1];
    }

    private Vector3 GetPositionForRule(PropPlacementRule rule, RoomData room,
                                        DungeonGrid grid)
    {
        int cs = grid.CellSize;
        var b  = room.Bounds;

        return rule switch
        {
            PropPlacementRule.Center => new Vector3(
                (b.x + b.width  * 0.5f) * cs,
                0f,
                (b.y + b.height * 0.5f) * cs),

            PropPlacementRule.Corner => PickCorner(b, cs),

            PropPlacementRule.AgainstWall => PickWallAdjacentCell(b, cs),

            _ => new Vector3( // Random (dentro de bounds con margen)
                UnityEngine.Random.Range(b.x + 1, b.x + b.width  - 1) * cs,
                0f,
                UnityEngine.Random.Range(b.y + 1, b.y + b.height - 1) * cs)
        };
    }

    private Vector3 PickCorner(RectInt b, int cs)
    {
        var corners = new[]
        {
            new Vector3((b.x + 1)            * cs, 0, (b.y + 1)             * cs),
            new Vector3((b.x + b.width  - 2) * cs, 0, (b.y + 1)             * cs),
            new Vector3((b.x + 1)            * cs, 0, (b.y + b.height - 2)  * cs),
            new Vector3((b.x + b.width  - 2) * cs, 0, (b.y + b.height - 2)  * cs),
        };
        return corners[UnityEngine.Random.Range(0, corners.Length)];
    }

    private Vector3 PickWallAdjacentCell(RectInt b, int cs)
    {
        // Elige un punto a 1 celda del borde (junto a la pared)
        int side = UnityEngine.Random.Range(0, 4);
        float x, z;
        switch (side)
        {
            case 0: x = UnityEngine.Random.Range(b.x + 1, b.x + b.width  - 1); z = b.y + 1; break;
            case 1: x = UnityEngine.Random.Range(b.x + 1, b.x + b.width  - 1); z = b.y + b.height - 2; break;
            case 2: x = b.x + 1;             z = UnityEngine.Random.Range(b.y + 1, b.y + b.height - 1); break;
            default: x = b.x + b.width - 2; z = UnityEngine.Random.Range(b.y + 1, b.y + b.height - 1); break;
        }
        return new Vector3(x * cs, 0f, z * cs);
    }

    public void SetParent(Transform parent) => propParent = parent;
    public void Clear() { if (propParent) foreach (Transform t in propParent) Destroy(t.gameObject); }
}
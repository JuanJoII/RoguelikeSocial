using UnityEngine;
using System.Collections.Generic;

public class PropDecorator : MonoBehaviour
{
    // ── Configuración ─────────────────────────────────────────
    [Header("Cantidad de props por sala")]
    [SerializeField] private int minPropsPerRoom = 2;
    [SerializeField] private int maxPropsPerRoom = 6;

    [Header("Distancia mínima entre props (unidades Unity)")]
    [SerializeField] private float minDistanceBetweenProps = 1.5f;

    [Header("Margen desde la pared (celdas)")]
    [SerializeField] private int wallMarginCells = 1;

    private Transform propParent;

    // Posiciones ya ocupadas por props en esta sala
    private readonly List<Vector3> placedPositions = new();

    // Referencia a muebles colocados (para OnFurniture)
    private readonly List<Vector3> furniturePositions = new();

    // ── API pública ───────────────────────────────────────────

    public void DecorateRoom(RoomData room, PropCollection collection,
                              DungeonGrid grid, System.Random rng)
    {
        if (collection == null || Biome(grid) == null) return;

        EnsureParent();
        placedPositions.Clear();
        furniturePositions.Clear();

        var eligible = collection.GetForRoom(room.RoomType);
        if (eligible.Count == 0) return;

        int count = rng.Next(minPropsPerRoom, maxPropsPerRoom + 1);

        // Primero colocar muebles (para que OnFurniture los encuentre)
        PlaceByRule(eligible, PropPlacementRule.Corner,      room, grid, rng, count);
        PlaceByRule(eligible, PropPlacementRule.AgainstWall, room, grid, rng, count);
        PlaceByRule(eligible, PropPlacementRule.FloorFree,   room, grid, rng, count);
        PlaceByRule(eligible, PropPlacementRule.FloorRare,   room, grid, rng, count);

        // OnFurniture al final — necesita furniturePositions poblado
        PlaceByRule(eligible, PropPlacementRule.OnFurniture, room, grid, rng, count);
    }

    public void Clear()
    {
        if (propParent == null) return;
        foreach (Transform t in propParent)
#if UNITY_EDITOR
            DestroyImmediate(t.gameObject);
#else
            Destroy(t.gameObject);
#endif
    }

    // ── Placement por regla ───────────────────────────────────

    private void PlaceByRule(List<PropData> eligible, PropPlacementRule rule,
                              RoomData room, DungeonGrid grid,
                              System.Random rng, int totalBudget)
    {
        var candidates = eligible.FindAll(p => p.PlacementRule == rule);
        if (candidates.Count == 0) return;

        // Cuántos de esta regla colocar (fracción del budget total)
        int ruleBudget = GetRuleBudget(rule, totalBudget, rng);

        for (int i = 0; i < ruleBudget; i++)
        {
            var prop = WeightedPick(candidates, rng);
            if (prop == null) continue;
            if (rng.NextDouble() > prop.SpawnChance) continue;

            Vector3? pos = FindPositionForRule(rule, room, grid, prop, rng);
            if (pos == null) continue;

            SpawnProp(prop, pos.Value, rng);
        }
    }

    private int GetRuleBudget(PropPlacementRule rule, int total, System.Random rng)
    {
        return rule switch
        {
            PropPlacementRule.Corner      => rng.Next(1, 3),
            PropPlacementRule.AgainstWall => rng.Next(1, Mathf.Max(2, total / 2)),
            PropPlacementRule.FloorFree   => rng.Next(1, Mathf.Max(2, total / 3)),
            PropPlacementRule.FloorRare   => rng.Next(0, 2),
            PropPlacementRule.OnFurniture => rng.Next(1, Mathf.Max(2, total / 3)),
            _ => 1
        };
    }

    // ── Encontrar posición según regla ────────────────────────

    private Vector3? FindPositionForRule(PropPlacementRule rule, RoomData room,
                                          DungeonGrid grid, PropData prop,
                                          System.Random rng)
    {
        return rule switch
        {
            PropPlacementRule.AgainstWall => FindWallPosition(room, grid, rng),
            PropPlacementRule.Corner      => FindCornerPosition(room, grid, rng),
            PropPlacementRule.FloorFree   => FindFloorPosition(room, grid, rng),
            PropPlacementRule.FloorRare   => FindFloorPosition(room, grid, rng),
            PropPlacementRule.OnFurniture => FindFurniturePosition(rng),
            _ => null
        };
    }

    /// Posición contra una pared: 1 celda desde el borde, centrada en X o Z.
    private Vector3? FindWallPosition(RoomData room, DungeonGrid grid, System.Random rng)
    {
        int cs     = grid.CellSize;
        int margin = wallMarginCells;

        // Elegir lado aleatorio: 0=Norte, 1=Sur, 2=Este, 3=Oeste
        int[] sides = { 0, 1, 2, 3 };
        Shuffle(sides, rng);

        foreach (int side in sides)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector3 pos;

                switch (side)
                {
                    case 0: // Norte
                        pos = new Vector3(
                            RandomInRange(room.Bounds.x + margin,
                                          room.Bounds.x + room.Bounds.width - margin, rng) * cs + cs * 0.5f,
                            0f,
                            (room.Bounds.y + room.Bounds.height - margin - 1) * cs + cs * 0.5f);
                        break;
                    case 1: // Sur
                        pos = new Vector3(
                            RandomInRange(room.Bounds.x + margin,
                                          room.Bounds.x + room.Bounds.width - margin, rng) * cs + cs * 0.5f,
                            0f,
                            (room.Bounds.y + margin) * cs + cs * 0.5f);
                        break;
                    case 2: // Este
                        pos = new Vector3(
                            (room.Bounds.x + room.Bounds.width - margin - 1) * cs + cs * 0.5f,
                            0f,
                            RandomInRange(room.Bounds.y + margin,
                                          room.Bounds.y + room.Bounds.height - margin, rng) * cs + cs * 0.5f);
                        break;
                    default: // Oeste
                        pos = new Vector3(
                            (room.Bounds.x + margin) * cs + cs * 0.5f,
                            0f,
                            RandomInRange(room.Bounds.y + margin,
                                          room.Bounds.y + room.Bounds.height - margin, rng) * cs + cs * 0.5f);
                        break;
                }

                if (IsFarEnough(pos))
                    return pos;
            }
        }
        return null;
    }

    /// Posición en una de las 4 esquinas interiores de la sala.
    private Vector3? FindCornerPosition(RoomData room, DungeonGrid grid, System.Random rng)
    {
        int cs     = grid.CellSize;
        int margin = wallMarginCells;

        var corners = new[]
        {
            new Vector3((room.Bounds.x + margin)                        * cs + cs * 0.5f, 0f,
                        (room.Bounds.y + margin)                        * cs + cs * 0.5f),
            new Vector3((room.Bounds.x + room.Bounds.width  - margin - 1) * cs + cs * 0.5f, 0f,
                        (room.Bounds.y + margin)                        * cs + cs * 0.5f),
            new Vector3((room.Bounds.x + margin)                        * cs + cs * 0.5f, 0f,
                        (room.Bounds.y + room.Bounds.height - margin - 1) * cs + cs * 0.5f),
            new Vector3((room.Bounds.x + room.Bounds.width  - margin - 1) * cs + cs * 0.5f, 0f,
                        (room.Bounds.y + room.Bounds.height - margin - 1) * cs + cs * 0.5f),
        };

        Shuffle(corners, rng);
        foreach (var c in corners)
            if (IsFarEnough(c)) return c;

        return null;
    }

    /// Posición libre en el interior de la sala (zona central).
    private Vector3? FindFloorPosition(RoomData room, DungeonGrid grid, System.Random rng)
    {
        int cs     = grid.CellSize;
        int margin = wallMarginCells + 1; // más margen para centro

        int minX = room.Bounds.x + margin;
        int maxX = room.Bounds.x + room.Bounds.width  - margin;
        int minZ = room.Bounds.y + margin;
        int maxZ = room.Bounds.y + room.Bounds.height - margin;

        if (minX >= maxX || minZ >= maxZ) return null;

        for (int attempt = 0; attempt < 15; attempt++)
        {
            var pos = new Vector3(
                RandomInRange(minX, maxX, rng) * cs + cs * 0.5f,
                0f,
                RandomInRange(minZ, maxZ, rng) * cs + cs * 0.5f);

            if (IsFarEnough(pos)) return pos;
        }
        return null;
    }

    /// Posición encima de un mueble ya colocado.
    private Vector3? FindFurniturePosition(System.Random rng)
    {
        if (furniturePositions.Count == 0) return null;

        // Elegir un mueble aleatorio
        int idx = rng.Next(0, furniturePositions.Count);
        var furniturePos = furniturePositions[idx];

        // Offset pequeño encima del mueble
        return furniturePos + Vector3.up * 0.5f
             + new Vector3((float)(rng.NextDouble() - 0.5) * 0.3f, 0f,
                           (float)(rng.NextDouble() - 0.5) * 0.3f);
    }

    // ── Instanciar prop ───────────────────────────────────────

    private void SpawnProp(PropData prop, Vector3 basePos, System.Random rng)
    {
        // Leer altura real desde el renderer del prefab
        float halfHeight = 0f;
        var renderer = prop.Prefab.GetComponentInChildren<Renderer>();
        if (renderer != null)
            halfHeight = renderer.bounds.size.y * 0.5f;

        // Posición final: base en Y=0, subir la mitad de la altura
        var finalPos = new Vector3(basePos.x, halfHeight, basePos.z);

        // Rotación aleatoria en pasos de 90° (queda más ordenado que rotación libre)
        int rotSteps = prop.PlacementRule == PropPlacementRule.AgainstWall
            ? rng.Next(0, 4)   // contra pared: cualquier orientación
            : rng.Next(0, 4);
        var rot = Quaternion.Euler(0f, rotSteps * 90f, 0f);

        var go = Instantiate(prop.Prefab, finalPos, rot, propParent);
        go.name = $"Prop_{prop.PropName}";

        // Registrar posición
        placedPositions.Add(finalPos);

        // Si es mueble, registrar para OnFurniture
        bool isFurniture = prop.PropName.ToLower().Contains("table")
                        || prop.PropName.ToLower().Contains("shelf");
        if (isFurniture)
            furniturePositions.Add(finalPos);
    }

    // ── Helpers ───────────────────────────────────────────────

    private bool IsFarEnough(Vector3 pos)
    {
        foreach (var existing in placedPositions)
            if (Vector3.Distance(pos, existing) < minDistanceBetweenProps)
                return false;
        return true;
    }

    private PropData WeightedPick(List<PropData> pool, System.Random rng)
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
        return pool[pool.Count - 1];
    }

    private int RandomInRange(int min, int max, System.Random rng)
    {
        if (min >= max) return min;
        return rng.Next(min, max);
    }

    private void Shuffle<T>(T[] array, System.Random rng)
    {
        for (int i = array.Length - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            (array[i], array[j]) = (array[j], array[i]);
        }
    }

    private BiomeConfig Biome(DungeonGrid grid) => null; // no se usa, solo para guardia

    private void EnsureParent()
    {
        if (propParent != null) return;
        var go = new GameObject("_Props");
        go.transform.SetParent(transform);
        go.transform.localPosition = Vector3.zero;
        propParent = go.transform;
    }
}
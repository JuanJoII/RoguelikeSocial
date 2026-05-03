using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Valida que el dungeon sea 100% accesible usando BFS.
/// Opera sobre dos niveles:
///   1. Grafo de salas (ConnectedRoomIds) — valida lógica de conexión
///   2. Grilla de celdas de suelo — valida geometría física
/// </summary>
public class AccessibilityValidator : MonoBehaviour
{
    [SerializeField] private DungeonConfig config;

    // ─────────────────────────────────────────────
    // VALIDACIÓN DE GRAFO (RoomData)
    // ─────────────────────────────────────────────

    /// <summary>
    /// BFS sobre el grafo de salas.
    /// Devuelve true si todas las salas son alcanzables desde rooms[0].
    /// </summary>
    public bool ValidateRoomGraph(List<RoomData> rooms)
    {
        if (rooms == null || rooms.Count == 0)
        {
            Debug.LogWarning("[VALIDATOR] Lista de salas vacía");
            return false;
        }
        if (rooms.Count == 1) return true;

        // Construir diccionario Id → RoomData para BFS
        var roomById = new Dictionary<int, RoomData>();
        foreach (var r in rooms) roomById[r.Id] = r;

        var visited = new HashSet<int>();
        var queue   = new Queue<int>();

        queue.Enqueue(rooms[0].Id);
        visited.Add(rooms[0].Id);

        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            if (!roomById.TryGetValue(current, out RoomData room)) continue;

            foreach (int neighborId in room.ConnectedRoomIds)
            {
                if (!visited.Contains(neighborId))
                {
                    visited.Add(neighborId);
                    queue.Enqueue(neighborId);
                }
            }
        }

        bool allReachable = visited.Count == rooms.Count;

        if (allReachable)
        {
            Log($"✓ Grafo: {rooms.Count}/{rooms.Count} salas alcanzables");
        }
        else
        {
            Debug.LogError($"[VALIDATOR] ✗ Grafo: solo {visited.Count}/{rooms.Count} salas alcanzables");

            foreach (var r in rooms)
            {
                if (!visited.Contains(r.Id))
                    Debug.LogError($"[VALIDATOR]   INALCANZABLE: {r.DebugLabel} " +
                                   $"(conexiones: {r.ConnectedRoomIds.Count})");
            }
        }

        return allReachable;
    }

    // ─────────────────────────────────────────────
    // VALIDACIÓN DE GRILLA (DungeonGrid)
    // ─────────────────────────────────────────────

    /// <summary>
    /// BFS sobre la grilla de celdas.
    /// Devuelve true si todas las celdas de suelo están conectadas físicamente.
    /// Esto detecta pasillos rotos o salas flotantes aunque el grafo diga que están conectadas.
    /// </summary>
    public bool ValidateGridConnectivity(DungeonGrid grid, List<RoomData> rooms)
    {
        if (rooms == null || rooms.Count == 0) return false;

        // Encontrar una celda de suelo inicial (centro de la primera sala)
        Vector2Int start = rooms[0].CenterCellInt;

        if (!grid.IsFloor(start))
        {
            // Buscar cualquier celda de suelo cercana
            start = FindAnyFloorCell(grid);
            if (start == new Vector2Int(-1, -1))
            {
                Debug.LogError("[VALIDATOR] No se encontró ninguna celda de suelo");
                return false;
            }
        }

        // BFS en la grilla
        var visited = new HashSet<Vector2Int>();
        var queue   = new Queue<Vector2Int>();

        queue.Enqueue(start);
        visited.Add(start);

        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        while (queue.Count > 0)
        {
            Vector2Int cell = queue.Dequeue();

            foreach (var dir in dirs)
            {
                Vector2Int neighbor = cell + dir;
                if (!visited.Contains(neighbor) && grid.IsFloor(neighbor))
                {
                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }

        // Contar total de celdas de suelo
        int totalFloor = grid.GetAllFloorCells().Count;
        bool connected = visited.Count == totalFloor;

        if (connected)
        {
            Log($"✓ Grilla: {totalFloor} celdas de suelo, todas conectadas");
        }
        else
        {
            Debug.LogError($"[VALIDATOR] ✗ Grilla: {visited.Count}/{totalFloor} celdas conectadas " +
                           $"({totalFloor - visited.Count} celdas aisladas)");
        }

        return connected;
    }

    // ─────────────────────────────────────────────
    // VALIDACIÓN COMPLETA
    // ─────────────────────────────────────────────

    /// <summary>
    /// Ejecuta ambas validaciones y devuelve true solo si AMBAS pasan.
    /// </summary>
    public bool ValidateAll(List<RoomData> rooms, DungeonGrid grid)
    {
        Log("═══════════════════ VALIDACIÓN ═══════════════════");

        bool graphOk = ValidateRoomGraph(rooms);
        bool gridOk  = ValidateGridConnectivity(grid, rooms);

        if (graphOk && gridOk)
            Log("✓✓✓ DUNGEON COMPLETAMENTE ACCESIBLE ✓✓✓");
        else
            Debug.LogError("[VALIDATOR] ✗ DUNGEON TIENE PROBLEMAS DE CONECTIVIDAD");

        Log("══════════════════════════════════════════════════");
        return graphOk && gridOk;
    }

    // ─────────────────────────────────────────────
    // UTILIDADES
    // ─────────────────────────────────────────────

    private Vector2Int FindAnyFloorCell(DungeonGrid grid)
    {
        var floors = grid.GetAllFloorCells();
        return floors.Count > 0 ? floors[0] : new Vector2Int(-1, -1);
    }

    private void Log(string msg)
    {
        if (config != null && config.logStats)
            Debug.Log($"[VALIDATOR] {msg}");
    }
}
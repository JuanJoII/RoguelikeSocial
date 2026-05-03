using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Lee la DungeonGrid final y coloca prefabs en escena.
/// - Suelo: en toda celda Room o Corridor
/// - Pared:  en toda celda Empty que sea adyacente a al menos una celda de suelo
/// 
/// NINGUNA pared se coloca sobre pasillos. La grilla es la única fuente de verdad.
/// </summary>
public class RoomArchitectureGenerator : MonoBehaviour
{
    [SerializeField] private DungeonConfig config;

    // ─────────────────────────────────────────────
    // CONTAINER DE OBJETOS
    // ─────────────────────────────────────────────

    private Transform floorParent;
    private Transform wallParent;

    // ─────────────────────────────────────────────
    // API PÚBLICA
    // ─────────────────────────────────────────────

    /// <summary>
    /// Genera toda la geometría (suelo + paredes) a partir de la grilla.
    /// Llama primero a ClearGeometry() si quieres regenerar.
    /// </summary>
    public void BuildGeometry(DungeonGrid grid)
    {
        if (config.floorPrefab == null || config.wallPrefab == null)
        {
            Debug.LogError("[ARCHITECTURE] floorPrefab o wallPrefab no asignados en DungeonConfig.");
            return;
        }

        // Crear contenedores limpios
        ClearGeometry();
        floorParent = CreateContainer("_Floors");
        wallParent  = CreateContainer("_Walls");

        int floors = 0;
        int walls  = 0;

        // ── Paso 1: Colocar suelos ─────────────────────────────────
        for (int x = 0; x < grid.Width; x++)
        for (int y = 0; y < grid.Height; y++)
        {
            var cell = new Vector2Int(x, y);
            if (grid.IsFloor(cell))
            {
                PlaceFloor(cell, grid);
                floors++;
            }
        }

        // ── Paso 2: Colocar paredes (celda vacía adyacente a suelo) ─
        for (int x = 0; x < grid.Width; x++)
        for (int y = 0; y < grid.Height; y++)
        {
            var cell = new Vector2Int(x, y);
            if (grid.IsEmpty(cell) && IsAdjacentToFloor(cell, grid))
            {
                PlaceWall(cell, grid);
                walls++;
            }
        }

        Log($"Geometría generada: {floors} suelos, {walls} paredes");
    }

    /// <summary>
    /// Destruye toda la geometría previamente generada.
    /// </summary>
    public void ClearGeometry()
    {
        DestroyContainer("_Floors");
        DestroyContainer("_Walls");
    }

    // ─────────────────────────────────────────────
    // COLOCACIÓN
    // ─────────────────────────────────────────────

    private void PlaceFloor(Vector2Int cell, DungeonGrid grid)
    {
        Vector3 worldPos = DungeonGrid.CellToWorld(cell, config.cellSize);
        worldPos.y = 0f;

        // El suelo es plano — rotación identidad
        Instantiate(config.floorPrefab, worldPos, Quaternion.identity, floorParent);
    }

    private void PlaceWall(Vector2Int cell, DungeonGrid grid)
    {
        Vector3 worldPos = DungeonGrid.CellToWorld(cell, config.cellSize);
        worldPos.y = 0f;

        // Calcular rotación basada en qué lado tiene suelo (para paredes con dirección)
        Quaternion rotation = GetWallRotation(cell, grid);

        Instantiate(config.wallPrefab, worldPos, rotation, wallParent);
    }

    // ─────────────────────────────────────────────
    // ROTACIÓN DE PAREDES
    // ─────────────────────────────────────────────

    /// <summary>
    /// Orienta la pared mirando hacia la celda de suelo más cercana.
    /// Si hay múltiples vecinos de suelo (esquina), mantiene identidad.
    /// </summary>
    private Quaternion GetWallRotation(Vector2Int cell, DungeonGrid grid)
    {
        // Vecinos en 4 direcciones
        bool N = grid.IsFloor(cell + Vector2Int.up);
        bool S = grid.IsFloor(cell + Vector2Int.down);
        bool E = grid.IsFloor(cell + Vector2Int.right);
        bool W = grid.IsFloor(cell + Vector2Int.left);

        // Contar vecinos de suelo
        int count = (N ? 1 : 0) + (S ? 1 : 0) + (E ? 1 : 0) + (W ? 1 : 0);

        if (count == 1)
        {
            // Pared simple: apuntar hacia el suelo
            if (N) return Quaternion.Euler(0, 0,   0);   // Frente al Norte
            if (S) return Quaternion.Euler(0, 180, 0);   // Frente al Sur
            if (E) return Quaternion.Euler(0, 90,  0);   // Frente al Este
            if (W) return Quaternion.Euler(0, 270, 0);   // Frente al Oeste
        }

        return Quaternion.identity; // Esquina o múltiple — sin rotación especial
    }

    // ─────────────────────────────────────────────
    // CONSULTAS DE GRILLA
    // ─────────────────────────────────────────────

    private static readonly Vector2Int[] CardinalDirs =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    /// <summary>
    /// Devuelve true si al menos uno de los 4 vecinos cardinales es suelo.
    /// (No diagonal — evita esquinas de paredes flotantes)
    /// </summary>
    private bool IsAdjacentToFloor(Vector2Int cell, DungeonGrid grid)
    {
        foreach (var dir in CardinalDirs)
            if (grid.IsFloor(cell + dir))
                return true;
        return false;
    }

    // ─────────────────────────────────────────────
    // GESTIÓN DE CONTAINERS
    // ─────────────────────────────────────────────

    private Transform CreateContainer(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform);
        return go.transform;
    }

    private void DestroyContainer(string name)
    {
        Transform existing = transform.Find(name);
        if (existing != null)
        {
#if UNITY_EDITOR
            DestroyImmediate(existing.gameObject);
#else
            Destroy(existing.gameObject);
#endif
        }
    }

    // ─────────────────────────────────────────────
    // UTILIDADES
    // ─────────────────────────────────────────────

    private void Log(string msg)
    {
        if (config != null && config.logStats)
            Debug.Log($"[ARCHITECTURE] {msg}");
    }
}
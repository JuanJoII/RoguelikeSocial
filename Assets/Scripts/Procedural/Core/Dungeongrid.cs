using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Tipos de celda posibles en la grilla.
/// </summary>
public enum CellType
{
    Empty,      // Celda vacía, sin nada
    Room,       // Interior de una sala
    Corridor,   // Pasillo
    Wall        // Pared (se calcula en ArchitectureGenerator)
}

/// <summary>
/// Grilla central de ocupación del dungeon.
/// ÚNICA fuente de verdad sobre qué hay en cada celda.
/// Todas las coordenadas son Vector2Int (celdas), NO unidades de Unity.
/// </summary>
public class DungeonGrid
{
    private readonly int width;
    private readonly int height;
    private readonly Dictionary<Vector2Int, CellType> cells;

    public int Width  => width;
    public int Height => height;

    public DungeonGrid(int width, int height)
    {
        this.width  = width;
        this.height = height;
        cells = new Dictionary<Vector2Int, CellType>(width * height / 4);
    }

    // ─────────────────────────────────────────────
    // ESCRITURA
    // ─────────────────────────────────────────────

    /// <summary>
    /// Marca una celda con un tipo. Los pasillos no pueden sobreescribir salas.
    /// </summary>
    public void SetCell(Vector2Int pos, CellType type)
    {
        if (!IsInBounds(pos)) return;

        // Las salas tienen prioridad sobre pasillos
        if (cells.TryGetValue(pos, out CellType existing))
        {
            if (existing == CellType.Room && type == CellType.Corridor)
                return;
        }

        cells[pos] = type;
    }

    /// <summary>
    /// Marca un rectángulo entero de celdas con un tipo dado.
    /// </summary>
    public void SetRect(RectInt rect, CellType type)
    {
        for (int x = rect.x; x < rect.x + rect.width; x++)
        for (int y = rect.y; y < rect.y + rect.height; y++)
            SetCell(new Vector2Int(x, y), type);
    }

    // ─────────────────────────────────────────────
    // LECTURA
    // ─────────────────────────────────────────────

    public CellType GetCell(Vector2Int pos)
    {
        if (!IsInBounds(pos)) return CellType.Empty;
        return cells.TryGetValue(pos, out CellType t) ? t : CellType.Empty;
    }

    public CellType GetCell(int x, int y) => GetCell(new Vector2Int(x, y));

    public bool IsFloor(Vector2Int pos)
    {
        CellType t = GetCell(pos);
        return t == CellType.Room || t == CellType.Corridor;
    }

    public bool IsEmpty(Vector2Int pos)
    {
        return GetCell(pos) == CellType.Empty;
    }

    public bool IsInBounds(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height;
    }

    // ─────────────────────────────────────────────
    // CONSULTAS
    // ─────────────────────────────────────────────

    /// <summary>
    /// Devuelve true si TODAS las celdas del rectángulo están vacías
    /// (considerando el padding alrededor).
    /// </summary>
    public bool IsRectFree(RectInt rect, int padding = 0)
    {
        RectInt expanded = new RectInt(
            rect.x - padding,
            rect.y - padding,
            rect.width  + padding * 2,
            rect.height + padding * 2
        );

        for (int x = expanded.x; x < expanded.x + expanded.width; x++)
        for (int y = expanded.y; y < expanded.y + expanded.height; y++)
        {
            Vector2Int pos = new Vector2Int(x, y);
            if (!IsInBounds(pos)) return false;         // Fuera de límites = no válido
            if (!IsEmpty(pos))    return false;         // Celda ocupada = no válido
        }
        return true;
    }

    /// <summary>
    /// Devuelve todas las celdas de suelo (Room + Corridor).
    /// </summary>
    public List<Vector2Int> GetAllFloorCells()
    {
        var result = new List<Vector2Int>();
        foreach (var kvp in cells)
            if (kvp.Value == CellType.Room || kvp.Value == CellType.Corridor)
                result.Add(kvp.Key);
        return result;
    }

    /// <summary>
    /// Devuelve todas las celdas del diccionario.
    /// </summary>
    public IEnumerable<KeyValuePair<Vector2Int, CellType>> GetAllCells() => cells;

    // ─────────────────────────────────────────────
    // UTILIDADES DE COORDENADAS
    // ─────────────────────────────────────────────

    /// <summary>
    /// Convierte coordenada de celda a posición mundo (centro de la celda).
    /// </summary>
    public static Vector3 CellToWorld(Vector2Int cell, int cellSize)
    {
        return new Vector3(
            cell.x * cellSize + cellSize * 0.5f,
            0f,
            cell.y * cellSize + cellSize * 0.5f
        );
    }

    /// <summary>
    /// Convierte posición mundo a coordenada de celda.
    /// </summary>
    public static Vector2Int WorldToCell(Vector3 world, int cellSize)
    {
        return new Vector2Int(
            Mathf.FloorToInt(world.x / cellSize),
            Mathf.FloorToInt(world.z / cellSize)
        );
    }
}
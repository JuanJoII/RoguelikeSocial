using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Tipos de celda del mapa.
/// </summary>
public enum CellType { Empty, Room, Corridor, Wall }

/// <summary>
/// Grilla central de ocupación del dungeon.
/// ÚNICA fuente de verdad sobre qué hay en cada celda.
///
/// COORDENADAS:
///   - Todo opera en celdas (Vector2Int), NO en unidades Unity
///   - La celda (0,0) empieza exactamente en el origen (0,0,0) de Unity
///   - Para convertir a mundo: usa CellCenter() o CellOrigin()
///
/// CONSTRUCTOR:
///   new DungeonGrid(width, height, cellSize)
///   Ejemplo: new DungeonGrid(80, 80, 4)
/// </summary>
public class DungeonGrid
{
    private readonly int width;
    private readonly int height;
    private readonly int cellSize;

    private readonly Dictionary<Vector2Int, CellType> cells;

    // ─────────────────────────────────────────────
    // PROPIEDADES
    // ─────────────────────────────────────────────

    public int Width    => width;
    public int Height   => height;
    public int CellSize => cellSize;

    // ─────────────────────────────────────────────
    // CONSTRUCTOR
    // ─────────────────────────────────────────────

    /// <param name="width">Ancho de la grilla en celdas</param>
    /// <param name="height">Alto de la grilla en celdas</param>
    /// <param name="cellSize">Tamaño de cada celda en unidades Unity (debe coincidir con DungeonConfig.cellSize)</param>
    public DungeonGrid(int width, int height, int cellSize)
    {
        this.width    = width;
        this.height   = height;
        this.cellSize = cellSize;
        cells = new Dictionary<Vector2Int, CellType>(width * height / 4);
    }

    // ─────────────────────────────────────────────
    // ESCRITURA
    // ─────────────────────────────────────────────

    /// <summary>
    /// Marca una celda con un tipo.
    /// Las salas tienen prioridad sobre pasillos — un Corridor no sobreescribe un Room.
    /// </summary>
    public void SetCell(Vector2Int pos, CellType type)
    {
        if (!IsInBounds(pos)) return;

        // Room tiene prioridad sobre Corridor
        if (cells.TryGetValue(pos, out CellType existing))
            if (existing == CellType.Room && type == CellType.Corridor)
                return;

        cells[pos] = type;
    }

    /// <summary>
    /// Marca un rectángulo entero de celdas con el tipo dado.
    /// </summary>
    public void SetRect(RectInt rect, CellType type)
    {
        for (int x = rect.x; x < rect.x + rect.width;  x++)
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

    /// <summary>Devuelve true si la celda es Room o Corridor.</summary>
    public bool IsFloor(Vector2Int pos)
    {
        var t = GetCell(pos);
        return t == CellType.Room || t == CellType.Corridor;
    }

    public bool IsEmpty(Vector2Int pos)  => GetCell(pos) == CellType.Empty;

    public bool IsInBounds(Vector2Int pos) =>
        pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height;

    /// <summary>
    /// Devuelve true si TODAS las celdas del rectángulo (+ padding) están vacías.
    /// </summary>
    public bool IsRectFree(RectInt rect, int padding = 0)
    {
        var expanded = new RectInt(
            rect.x      - padding,
            rect.y      - padding,
            rect.width  + padding * 2,
            rect.height + padding * 2);

        for (int x = expanded.x; x < expanded.x + expanded.width;  x++)
        for (int y = expanded.y; y < expanded.y + expanded.height; y++)
        {
            var pos = new Vector2Int(x, y);
            if (!IsInBounds(pos) || !IsEmpty(pos)) return false;
        }
        return true;
    }

    /// <summary>Devuelve todas las celdas de suelo (Room + Corridor).</summary>
    public List<Vector2Int> GetAllFloorCells()
    {
        var result = new List<Vector2Int>();
        foreach (var kvp in cells)
            if (kvp.Value == CellType.Room || kvp.Value == CellType.Corridor)
                result.Add(kvp.Key);
        return result;
    }

    /// <summary>Iterador sobre todo el diccionario de celdas.</summary>
    public IEnumerable<KeyValuePair<Vector2Int, CellType>> GetAllCells() => cells;

    // ─────────────────────────────────────────────
    // COORDENADAS MUNDO
    // ─────────────────────────────────────────────

    /// <summary>
    /// Centro de la celda en unidades Unity.
    /// Usa esto si el pivot de tu prefab está en el CENTRO del modelo.
    /// </summary>
    public Vector3 CellCenter(Vector2Int cell) => new Vector3(
        cell.x * cellSize + cellSize * 0.5f,
        0f,
        cell.y * cellSize + cellSize * 0.5f);

    /// <summary>
    /// Esquina inferior-izquierda de la celda en unidades Unity.
    /// Usa esto si el pivot de tu prefab está en la ESQUINA del modelo.
    /// </summary>
    public Vector3 CellOrigin(Vector2Int cell) => new Vector3(
        cell.x * cellSize,
        0f,
        cell.y * cellSize);

    /// <summary>
    /// Centro en mundo de un rectángulo de celdas (por ejemplo, el centro de una sala completa).
    /// </summary>
    public Vector3 RectCenter(RectInt rect) => new Vector3(
        rect.x * cellSize + rect.width  * cellSize * 0.5f,
        0f,
        rect.y * cellSize + rect.height * cellSize * 0.5f);

    /// <summary>
    /// Convierte una posición mundo a coordenada de celda.
    /// </summary>
    public Vector2Int WorldToCell(Vector3 world) => new Vector2Int(
        Mathf.FloorToInt(world.x / cellSize),
        Mathf.FloorToInt(world.z / cellSize));
}
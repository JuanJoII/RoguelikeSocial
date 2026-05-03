using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Tipos de sala disponibles.
/// </summary>
public enum RoomType { Start, Normal, Boss, Treasure }

/// <summary>
/// Datos de una sala individual.
/// TODAS las coordenadas son en celdas (Vector2Int), no en unidades de Unity.
/// Para convertir a mundo: DungeonGrid.CellToWorld(pos, cellSize)
/// </summary>
public class RoomData
{
    // ─────────────────────────────────────────────
    // GEOMETRÍA (en celdas)
    // ─────────────────────────────────────────────

    /// <summary>Rectángulo que ocupa la sala en la grilla (en celdas).</summary>
    public RectInt Bounds;

    /// <summary>Centro de la sala en celdas (puede ser fraccionario).</summary>
    public Vector2 CenterCell => new Vector2(
        Bounds.x + Bounds.width  * 0.5f,
        Bounds.y + Bounds.height * 0.5f
    );

    /// <summary>Centro de la sala en celdas redondeado a entero.</summary>
    public Vector2Int CenterCellInt => new Vector2Int(
        Mathf.RoundToInt(CenterCell.x),
        Mathf.RoundToInt(CenterCell.y)
    );

    // ─────────────────────────────────────────────
    // METADATOS
    // ─────────────────────────────────────────────

    public int Id;
    public RoomType RoomType = RoomType.Normal;

    /// <summary>IDs de salas conectadas a ésta (relación bidireccional).</summary>
    public List<int> ConnectedRoomIds = new List<int>();

    // ─────────────────────────────────────────────
    // DEBUG
    // ─────────────────────────────────────────────

    public string DebugLabel => $"Room_{Id} [{Bounds.width}x{Bounds.height}] @({Bounds.x},{Bounds.y})";

    // ─────────────────────────────────────────────
    // UTILIDADES
    // ─────────────────────────────────────────────

    /// <summary>
    /// Punto más cercano del borde de la sala a una posición dada (en celdas).
    /// Útil para calcular el punto de entrada de un pasillo.
    /// </summary>
    public Vector2Int ClosestBorderCell(Vector2Int from)
    {
        int cx = Mathf.Clamp(from.x, Bounds.x, Bounds.x + Bounds.width  - 1);
        int cy = Mathf.Clamp(from.y, Bounds.y, Bounds.y + Bounds.height - 1);
        return new Vector2Int(cx, cy);
    }

    public override string ToString() => DebugLabel;
}
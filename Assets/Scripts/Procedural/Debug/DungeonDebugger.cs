using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Visualiza el dungeon en el Editor de Unity via Gizmos.
/// Attach en el mismo GameObject que DungeonGenerator.
/// </summary>
public class DungeonDebugger : MonoBehaviour
{
    [SerializeField] private DungeonConfig config;

    [Header("Colores")]
    [SerializeField] private Color roomColor      = new Color(0.2f, 0.6f, 1f,  0.4f);
    [SerializeField] private Color corridorColor  = new Color(1f,   0.8f, 0.2f, 0.3f);
    [SerializeField] private Color wallColor      = new Color(0.8f, 0.2f, 0.2f, 0.2f);
    [SerializeField] private Color startColor     = new Color(0.2f, 1f,   0.2f, 0.6f);
    [SerializeField] private Color bossColor      = new Color(1f,   0.2f, 0.2f, 0.6f);
    [SerializeField] private Color connectionColor = Color.white;

    // Datos guardados tras la generación
    private DungeonGrid      lastGrid;
    private List<RoomData>   lastRooms;

    public void SetData(DungeonGrid grid, List<RoomData> rooms)
    {
        lastGrid  = grid;
        lastRooms = rooms;
    }

    private void OnDrawGizmos()
    {
        if (!config.drawGizmos) return;
        if (lastGrid == null)   return;

        int cs = config.cellSize;

        // ── Dibujar celdas de la grilla ──────────────────────────
        foreach (var kvp in lastGrid.GetAllCells())
        {
            Vector2Int cell = kvp.Key;
            CellType   type = kvp.Value;

            Vector3 center = DungeonGrid.CellToWorld(cell, cs);
            Vector3 size   = new Vector3(cs * 0.9f, 0.05f, cs * 0.9f);

            switch (type)
            {
                case CellType.Room:
                    Gizmos.color = roomColor;
                    Gizmos.DrawCube(center, size);
                    break;

                case CellType.Corridor:
                    Gizmos.color = corridorColor;
                    Gizmos.DrawCube(center, size);
                    break;
            }
        }

        // ── Dibujar salas con etiquetas ───────────────────────────
        if (lastRooms == null) return;

        foreach (var room in lastRooms)
        {
            // Borde de la sala
            Color borderColor = room.RoomType switch
            {
                RoomType.Start   => startColor,
                RoomType.Boss    => bossColor,
                _                => roomColor
            };
            borderColor.a = 1f;

            Vector3 worldCenter = DungeonGrid.CellToWorld(room.CenterCellInt, cs);
            Vector3 roomSize    = new Vector3(room.Bounds.width * cs, 0.1f, room.Bounds.height * cs);

            Gizmos.color = borderColor;
            Gizmos.DrawWireCube(worldCenter, roomSize);

            // Etiqueta
#if UNITY_EDITOR
            UnityEditor.Handles.color = borderColor;
            UnityEditor.Handles.Label(worldCenter + Vector3.up * 2f, room.DebugLabel);
#endif
        }

        // ── Dibujar conexiones entre salas ────────────────────────
        var drawn = new HashSet<(int, int)>();

        foreach (var room in lastRooms)
        {
            Vector3 fromWorld = DungeonGrid.CellToWorld(room.CenterCellInt, cs);

            foreach (int neighborId in room.ConnectedRoomIds)
            {
                int a = Mathf.Min(room.Id, neighborId);
                int b = Mathf.Max(room.Id, neighborId);
                if (drawn.Contains((a, b))) continue;
                drawn.Add((a, b));

                RoomData neighbor = lastRooms.Find(r => r.Id == neighborId);
                if (neighbor == null) continue;

                Vector3 toWorld = DungeonGrid.CellToWorld(neighbor.CenterCellInt, cs);

                Gizmos.color = connectionColor;
                Gizmos.DrawLine(fromWorld + Vector3.up * 0.5f, toWorld + Vector3.up * 0.5f);
            }
        }
    }
}
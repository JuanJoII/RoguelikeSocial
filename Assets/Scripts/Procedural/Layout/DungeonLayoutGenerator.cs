using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Coloca salas en la DungeonGrid usando intentos aleatorios.
/// Los parámetros de cantidad/tamaño vienen del DungeonDifficultyConfig activo.
/// GARANTÍA: cero solapamiento verificado contra la grilla antes de confirmar.
/// </summary>
public class DungeonLayoutGenerator : MonoBehaviour
{
    [SerializeField] private DungeonConfig config;

    // ─────────────────────────────────────────────
    // API PÚBLICA
    // ─────────────────────────────────────────────

    /// <summary>
    /// Genera y coloca salas en la grilla según el perfil de dificultad.
    /// </summary>
    public List<RoomData> GenerateRooms(DungeonGrid grid, DungeonDifficultyConfig difficulty)
    {
        var rooms = new List<RoomData>();
        int targetCount = Random.Range(difficulty.minRooms, difficulty.maxRooms + 1);

        Log($"Dificultad: {difficulty.displayName} | " +
            $"Objetivo: {targetCount} salas | " +
            $"Padding: {difficulty.roomPadding}c");

        for (int i = 0; i < targetCount; i++)
        {
            RoomData room = TryPlaceRoom(grid, i, rooms.Count, difficulty);

            if (room != null)
            {
                rooms.Add(room);
                Log($"  ✓ {room.DebugLabel}");
            }
            else
            {
                Log($"  ✗ Sala {i}: sin espacio tras {difficulty.maxPlacementAttempts} intentos");
            }
        }

        AssignRoomTypes(rooms);

        Log($"Layout completo: {rooms.Count}/{targetCount} salas colocadas");
        return rooms;
    }

    // ─────────────────────────────────────────────
    // COLOCACIÓN
    // ─────────────────────────────────────────────

    private RoomData TryPlaceRoom(DungeonGrid grid, int id, int placedSoFar,
                                   DungeonDifficultyConfig difficulty)
    {
        for (int attempt = 0; attempt < difficulty.maxPlacementAttempts; attempt++)
        {
            int w = Random.Range(difficulty.minRoomWidth,  difficulty.maxRoomWidth  + 1);
            int h = Random.Range(difficulty.minRoomHeight, difficulty.maxRoomHeight + 1);

            int margin = difficulty.roomPadding + 1;
            int x = Random.Range(margin, grid.Width  - w - margin);
            int y = Random.Range(margin, grid.Height - h - margin);

            RectInt bounds = new RectInt(x, y, w, h);

            if (grid.IsRectFree(bounds, difficulty.roomPadding))
            {
                grid.SetRect(bounds, CellType.Room);

                return new RoomData
                {
                    Id       = id,
                    Bounds   = bounds,
                    RoomType = (placedSoFar == 0) ? RoomType.Start : RoomType.Normal
                };
            }
        }

        return null;
    }

    // ─────────────────────────────────────────────
    // TIPOS DE SALA
    // ─────────────────────────────────────────────

    private void AssignRoomTypes(List<RoomData> rooms)
    {
        if (rooms.Count < 2) return;

        // Boss = sala más alejada del inicio
        RoomData startRoom = rooms[0];
        RoomData boss      = null;
        float    maxDist   = -1f;

        for (int i = 1; i < rooms.Count; i++)
        {
            float dist = Vector2.Distance(rooms[i].CenterCell, startRoom.CenterCell);
            if (dist > maxDist) { maxDist = dist; boss = rooms[i]; }
        }

        if (boss != null)
        {
            boss.RoomType = RoomType.Boss;
            Log($"Boss: {boss.DebugLabel}");
        }

        // Tesoro = primera sala Normal que no sea Boss
        if (rooms.Count >= 4)
        {
            foreach (var r in rooms)
            {
                if (r.RoomType == RoomType.Normal)
                {
                    r.RoomType = RoomType.Treasure;
                    Log($"Tesoro: {r.DebugLabel}");
                    break;
                }
            }
        }
    }

    // ─────────────────────────────────────────────
    // UTILIDADES
    // ─────────────────────────────────────────────

    private void Log(string msg)
    {
        if (config != null && config.logStats)
            Debug.Log($"[LAYOUT] {msg}");
    }
}
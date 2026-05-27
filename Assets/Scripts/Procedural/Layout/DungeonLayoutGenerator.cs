using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Coloca salas en la DungeonGrid.
///
/// LÓGICA DE BOSS ROOM:
///   La sala Boss se genera SIEMPRE al final, con tamaño garantizado
///   mayor que las salas normales (config.bossRoomMinSize/MaxSize).
///   Se coloca en la esquina más alejada del punto de inicio.
///
///   currentRooms.Last() es siempre la sala Boss — esto es la fuente
///   de verdad que usa RoomArchitectureGenerator para las puertas.
/// </summary>
public class DungeonLayoutGenerator : MonoBehaviour
{
    [SerializeField] private DungeonConfig config;

    // ─────────────────────────────────────────────
    // API PÚBLICA
    // ─────────────────────────────────────────────

    public List<RoomData> GenerateRooms(DungeonGrid grid, DungeonDifficultyConfig diff)
    {
        var rooms  = new List<RoomData>();
        int target = Random.Range(diff.minRooms, diff.maxRooms + 1);

        Log($"Objetivo: {target} salas normales + 1 Boss | " +
            $"Padding: {diff.roomPadding}c | Dificultad: {diff.displayName}");

        // ── Paso 1: Colocar sala de inicio ──────────────────────────
        var startRoom = PlaceStartRoom(grid, diff);
        if (startRoom != null)
        {
            rooms.Add(startRoom);
            Log($"  ✓ Start: {startRoom.DebugLabel}");
        }

        // ── Paso 2: Colocar salas normales ──────────────────────────
        for (int i = 1; i < target; i++)
        {
            var room = TryPlaceNormalRoom(grid, i, diff);
            if (room != null)
            {
                rooms.Add(room);
                Log($"  ✓ {room.DebugLabel}");
            }
            else
            {
                Log($"  ✗ Sala {i}: sin espacio tras {diff.maxPlacementAttempts} intentos");
            }
        }

        // ── Paso 3: Colocar sala Tesoro ──────────────────────────────
        if (rooms.Count >= 3)
        {
            var treasure = TryPlaceTreasureRoom(grid, rooms.Count, diff);
            if (treasure != null)
            {
                rooms.Add(treasure);
                Log($"  ✓ Tesoro: {treasure.DebugLabel}");
            }
        }

        // ── Paso 4: Colocar sala Boss (SIEMPRE al final) ─────────────
        var bossRoom = TryPlaceBossRoom(grid, rooms.Count, rooms, diff);
        if (bossRoom != null)
        {
            rooms.Add(bossRoom);
            Log($"  ✓ BOSS: {bossRoom.DebugLabel}");
        }
        else
        {
            // Si no encontró espacio con el tamaño grande, forzar con tamaño normal
            bossRoom = TryPlaceNormalRoom(grid, rooms.Count, diff);
            if (bossRoom != null)
            {
                bossRoom.RoomType = RoomType.Boss;
                rooms.Add(bossRoom);
                Log($"  ⚠ BOSS (tamaño reducido): {bossRoom.DebugLabel}");
            }
        }

        Log($"Layout completo: {rooms.Count} salas " +
            $"(última = {(rooms.Count > 0 ? rooms[rooms.Count-1].RoomType.ToString() : "ninguna")})");
        return rooms;
    }

    // ─────────────────────────────────────────────
    // SALA DE INICIO
    // ─────────────────────────────────────────────

    /// <summary>
    /// La sala de inicio se coloca cerca del centro de la grilla
    /// para maximizar el espacio disponible alrededor.
    /// </summary>
    private RoomData PlaceStartRoom(DungeonGrid grid, DungeonDifficultyConfig diff)
    {
        int w = Random.Range(diff.minRoomWidth, diff.maxRoomWidth + 1);
        int h = Random.Range(diff.minRoomHeight, diff.maxRoomHeight + 1);

        // Centro de la grilla con pequeña variación
        int cx = grid.Width  / 2 + Random.Range(-5, 5);
        int cy = grid.Height / 2 + Random.Range(-5, 5);
        int x  = Mathf.Clamp(cx - w / 2, 1, grid.Width  - w - 1);
        int y  = Mathf.Clamp(cy - h / 2, 1, grid.Height - h - 1);

        var bounds = new RectInt(x, y, w, h);
        if (!grid.IsRectFree(bounds, diff.roomPadding)) return null;

        grid.SetRect(bounds, CellType.Room);
        return new RoomData { Id = 0, Bounds = bounds, RoomType = RoomType.Start };
    }

    // ─────────────────────────────────────────────
    // SALAS NORMALES
    // ─────────────────────────────────────────────

    private RoomData TryPlaceNormalRoom(DungeonGrid grid, int id,
                                         DungeonDifficultyConfig diff)
    {
        for (int attempt = 0; attempt < diff.maxPlacementAttempts; attempt++)
        {
            int w      = Random.Range(diff.minRoomWidth,  diff.maxRoomWidth  + 1);
            int h      = Random.Range(diff.minRoomHeight, diff.maxRoomHeight + 1);
            int margin = diff.roomPadding + 1;
            int x      = Random.Range(margin, grid.Width  - w - margin);
            int y      = Random.Range(margin, grid.Height - h - margin);

            var bounds = new RectInt(x, y, w, h);
            if (!grid.IsRectFree(bounds, diff.roomPadding)) continue;

            grid.SetRect(bounds, CellType.Room);
            
            return new RoomData { Id = id, Bounds = bounds, RoomType = RoomType.Combat };
        }
        return null;
    }

    // ─────────────────────────────────────────────
    // SALA TESORO
    // ─────────────────────────────────────────────

    private RoomData TryPlaceTreasureRoom(DungeonGrid grid, int id,
                                           DungeonDifficultyConfig diff)
    {
        // Tesoro: sala mediana, misma lógica que normal
        for (int attempt = 0; attempt < diff.maxPlacementAttempts; attempt++)
        {
            int w      = Random.Range(diff.minRoomWidth, diff.maxRoomWidth);
            int h      = Random.Range(diff.minRoomHeight, diff.maxRoomHeight);
            int margin = diff.roomPadding + 1;
            int x      = Random.Range(margin, grid.Width  - w - margin);
            int y      = Random.Range(margin, grid.Height - h - margin);

            var bounds = new RectInt(x, y, w, h);
            if (!grid.IsRectFree(bounds, diff.roomPadding)) continue;

            grid.SetRect(bounds, CellType.Room);
            return new RoomData { Id = id, Bounds = bounds, RoomType = RoomType.Treasure };
        }
        return null;
    }

    // ─────────────────────────────────────────────
    // SALA DEL JEFE
    // ─────────────────────────────────────────────

    /// <summary>
    /// La sala Boss se coloca en la esquina más alejada de la sala de inicio.
    /// Tiene tamaño garantizado mayor que las salas normales.
    /// Si no hay espacio en la esquina preferida, prueba las otras esquinas.
    /// </summary>
    private RoomData TryPlaceBossRoom(DungeonGrid grid, int id,
                                       List<RoomData> existingRooms,
                                       DungeonDifficultyConfig diff)
    {
        int minW = config.bossRoomMinCells;
        int maxW = config.bossRoomMaxCells;

        // Determinar la esquina más lejana al Start
        Vector2Int startCenter = existingRooms.Count > 0
            ? existingRooms[0].CenterCellInt
            : new Vector2Int(grid.Width / 2, grid.Height / 2);

        // Las 4 esquinas de la grilla (con margen)
        int margin = diff.roomPadding + 2;
        Vector2Int[] corners =
        {
            new Vector2Int(margin, margin),
            new Vector2Int(grid.Width - maxW - margin, margin),
            new Vector2Int(margin, grid.Height - maxW - margin),
            new Vector2Int(grid.Width - maxW - margin, grid.Height - maxW - margin)
        };

        // Ordenar esquinas por distancia al Start (más lejana primero)
        System.Array.Sort(corners, (a, b) =>
        {
            float da = Vector2Int.Distance(a, startCenter);
            float db = Vector2Int.Distance(b, startCenter);
            return db.CompareTo(da); // descendente
        });

        // Intentar en cada esquina
        foreach (var corner in corners)
        {
            for (int attempt = 0; attempt < 30; attempt++)
            {
                int w = Random.Range(minW, maxW + 1);
                int h = Random.Range(minW, maxW + 1);

                // Posición cerca de la esquina preferida con algo de variación
                int x = Mathf.Clamp(corner.x + Random.Range(-3, 3), margin, grid.Width  - w - margin);
                int y = Mathf.Clamp(corner.y + Random.Range(-3, 3), margin, grid.Height - h - margin);

                var bounds = new RectInt(x, y, w, h);
                if (!grid.IsRectFree(bounds, diff.roomPadding)) continue;

                grid.SetRect(bounds, CellType.Room);
                return new RoomData
                {
                    Id       = id,
                    Bounds   = bounds,
                    RoomType = RoomType.Boss
                };
            }
        }

        return null;
    }

    // ─────────────────────────────────────────────
    // UTILIDADES
    // ─────────────────────────────────────────────

    private void Log(string msg)
    {
        if (config != null && config.logStats)
            Debug.Log($"[LAYOUT] {msg}");
    }
    public List<RoomData> GenerateRoomsFromGraph(DungeonGrid grid,  DungeonGraph graph, DungeonDifficultyConfig diff, System.Random rng)
    {
        return GenerateRooms(grid, diff);
    }
}
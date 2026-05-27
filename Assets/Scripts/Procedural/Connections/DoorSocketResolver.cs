using System.Collections.Generic;
using UnityEngine;

public class DoorSocketResolver : MonoBehaviour
{
    public void ResolveAllSockets(List<RoomData> rooms, DungeonGrid grid)
    {
        foreach (var room in rooms)
            ResolveSocketsForRoom(room, grid);
    }

    public void ResolveSocketsForRoom(RoomData room, DungeonGrid grid)
    {
        room.Sockets.Clear();
        int cs = grid.CellSize;

        // ── Norte: borde superior (Z máximo) ─────────────────────
        for (int x = room.Bounds.x; x < room.Bounds.x + room.Bounds.width; x++)
        {
            var outside = new Vector2Int(x, room.Bounds.y + room.Bounds.height);
            if (grid.GetCell(outside) == CellType.Corridor)
            {
                room.Sockets.Add(new DoorSocket
                {
                    Direction     = DoorDirection.North,
                    WorldPosition = new Vector3(
                        x * cs + cs * 0.5f, 0,
                        (room.Bounds.y + room.Bounds.height) * cs),
                    IsConnected = true
                });
                break;
            }
        }

        // ── Sur: borde inferior (Z mínimo) ────────────────────────
        for (int x = room.Bounds.x; x < room.Bounds.x + room.Bounds.width; x++)
        {
            var outside = new Vector2Int(x, room.Bounds.y - 1);
            if (grid.GetCell(outside) == CellType.Corridor)
            {
                room.Sockets.Add(new DoorSocket
                {
                    Direction     = DoorDirection.South,
                    WorldPosition = new Vector3(
                        x * cs + cs * 0.5f, 0,
                        room.Bounds.y * cs),
                    IsConnected = true
                });
                break;
            }
        }

        // ── Este: borde derecho (X máximo) ────────────────────────
        for (int y = room.Bounds.y; y < room.Bounds.y + room.Bounds.height; y++)
        {
            var outside = new Vector2Int(room.Bounds.x + room.Bounds.width, y);
            if (grid.GetCell(outside) == CellType.Corridor)
            {
                room.Sockets.Add(new DoorSocket
                {
                    Direction     = DoorDirection.East,
                    WorldPosition = new Vector3(
                        (room.Bounds.x + room.Bounds.width) * cs, 0,
                        y * cs + cs * 0.5f),
                    IsConnected = true
                });
                break;
            }
        }

        // ── Oeste: borde izquierdo (X mínimo) ─────────────────────
        for (int y = room.Bounds.y; y < room.Bounds.y + room.Bounds.height; y++)
        {
            var outside = new Vector2Int(room.Bounds.x - 1, y);
            if (grid.GetCell(outside) == CellType.Corridor)
            {
                room.Sockets.Add(new DoorSocket
                {
                    Direction     = DoorDirection.West,
                    WorldPosition = new Vector3(
                        room.Bounds.x * cs, 0,
                        y * cs + cs * 0.5f),
                    IsConnected = true
                });
                break;
            }
        }
    }
}
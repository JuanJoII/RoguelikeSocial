// Validation/AccessibilityValidator.cs
using UnityEngine;
using System.Collections.Generic;

public class AccessibilityValidator : MonoBehaviour
{
    [SerializeField] private DungeonConfig config;

    public struct ValidationResult
    {
        public bool IsValid;
        public bool CanReachBoss;
        public int  ReachableRooms;
        public int  TotalRooms;
        public List<int> UnreachableRoomIds;
        public string Summary => IsValid
            ? $"✓ Válido — {ReachableRooms}/{TotalRooms} salas accesibles, Boss alcanzable"
            : $"✗ Inválido — {ReachableRooms}/{TotalRooms} accesibles, Boss={CanReachBoss}, " +
              $"aisladas={string.Join(',', UnreachableRoomIds)}";
    }

    public bool ValidateAll(List<RoomData> rooms, DungeonGrid grid)
    {
        var result = Validate(rooms, grid);
        if (config.logStats) Debug.Log($"[VALIDATOR] {result.Summary}");
        return result.IsValid;
    }

    public ValidationResult Validate(List<RoomData> rooms, DungeonGrid grid)
    {
        var result = new ValidationResult
        {
            TotalRooms         = rooms.Count,
            UnreachableRoomIds = new List<int>()
        };

        if (rooms.Count == 0) return result;

        // BFS desde el centro de la primera sala (Start)
        var startCell    = rooms[0].CenterCellInt;
        var reachable    = BFS(startCell, grid);
        var bossRoom     = rooms[^1];
        var bossCenter   = bossRoom.CenterCellInt;

        result.CanReachBoss = reachable.Contains(bossCenter);

        // Verificar que al menos 1 celda de cada sala sea alcanzable
        foreach (var room in rooms)
        {
            bool roomReachable = false;
            for (int x = room.Bounds.x; x < room.Bounds.x + room.Bounds.width && !roomReachable; x++)
            for (int y = room.Bounds.y; y < room.Bounds.y + room.Bounds.height && !roomReachable; y++)
                if (reachable.Contains(new Vector2Int(x, y)))
                    roomReachable = true;

            if (roomReachable) result.ReachableRooms++;
            else               result.UnreachableRoomIds.Add(room.Id);
        }

        result.IsValid = result.CanReachBoss
                      && result.UnreachableRoomIds.Count == 0;
        return result;
    }

    private HashSet<Vector2Int> BFS(Vector2Int start, DungeonGrid grid)
    {
        var visited = new HashSet<Vector2Int>();
        var queue   = new Queue<Vector2Int>();

        if (!grid.IsFloor(start)) return visited;

        queue.Enqueue(start);
        visited.Add(start);

        var dirs = new[] {
            Vector2Int.up, Vector2Int.down,
            Vector2Int.left, Vector2Int.right
        };

        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            foreach (var d in dirs)
            {
                var neighbor = cell + d;
                if (!visited.Contains(neighbor) && grid.IsFloor(neighbor))
                {
                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }
        return visited;
    }
}
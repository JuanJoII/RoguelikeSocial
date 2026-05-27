using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class DungeonDebugger : MonoBehaviour
{
    [SerializeField] private bool showRoomLabels  = true;
    [SerializeField] private bool showThreatBudget = true;
    [SerializeField] private bool showLootInfo    = true;
    [SerializeField] private bool showGraph       = true;

    private DungeonGrid     grid;
    private List<RoomData>  rooms;
    private DungeonGraph    graph;

    public void SetData(DungeonGrid g, List<RoomData> r, DungeonGraph gr = null)
    {
        grid = g; rooms = r; graph = gr;
    }

    private void OnGUI()
    {
        if (rooms == null || rooms.Count == 0) return;

        // Panel lateral de stats detallados
        GUILayout.BeginArea(new Rect(Screen.width - 340, 15, 325, Screen.height - 30));
        var style = new GUIStyle(GUI.skin.box) { fontSize = 10, alignment = TextAnchor.UpperLeft };

        GUILayout.BeginVertical(style);
        GUILayout.Label("── DUNGEON DEBUG ──", new GUIStyle { fontSize = 11,
            fontStyle = FontStyle.Bold, normal = { textColor = Color.cyan } });

        foreach (var room in rooms)
        {
            var color = RoomTypeColor(room.RoomType);
            var labelStyle = new GUIStyle { fontSize = 10,
                normal = { textColor = color } };

            string line = $"[{room.Id}] {room.RoomType,-9} {room.Bounds.width}×{room.Bounds.height}";

            if (showThreatBudget && room.ResolvedEnemies != null && room.ResolvedEnemies.Count > 0)
            {
                int totalCost = room.ResolvedEnemies.Sum(e => e.ThreatCost);
                line += $" | ☠ {totalCost}pts ({room.ResolvedEnemies.Count} enemies)";
            }

            if (showLootInfo && room.ResolvedLoot != null && room.ResolvedLoot.Count > 0)
                line += $" | ♦ {room.ResolvedLoot.Count} loot";

            GUILayout.Label(line, labelStyle);
        }

        GUILayout.Space(8);
        GUILayout.Label($"Grid: {grid?.Width}×{grid?.Height} | Cell: {grid?.CellSize}u",
                         new GUIStyle { fontSize = 10, normal = { textColor = Color.gray } });

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    private void OnDrawGizmos()
    {
        if (rooms == null || grid == null) return;

        foreach (var room in rooms)
        {
            // Caja de la sala en colores por tipo
            Gizmos.color = RoomTypeColorGizmo(room.RoomType);
            var center = grid.RectCenter(room.Bounds);
            var size   = new Vector3(
                room.Bounds.width  * grid.CellSize,
                0.2f,
                room.Bounds.height * grid.CellSize);
            Gizmos.DrawWireCube(center, size);

            // Label en Scene view (solo Editor)
#if UNITY_EDITOR
            if (showRoomLabels)
                UnityEditor.Handles.Label(center + Vector3.up * 2f,
                    $"{room.RoomType}\n{room.Bounds.width}×{room.Bounds.height}");
#endif

            // Sockets de puertas
            if (room.Sockets != null)
            {
                Gizmos.color = Color.yellow;
                foreach (var socket in room.Sockets)
                    Gizmos.DrawSphere(socket.WorldPosition, 0.4f);
            }
        }

        // Graph connections
        if (showGraph && graph != null)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
            foreach (var node in graph.Nodes)
            {
                if (node.PlacedRoom == null) continue;
                var from = grid.RectCenter(node.PlacedRoom.Bounds) + Vector3.up * 3f;

                foreach (var connId in node.ConnectedNodeIds)
                {
                    var connNode = graph.Nodes.Find(n => n.Id == connId);
                    if (connNode?.PlacedRoom == null) continue;
                    var to = grid.RectCenter(connNode.PlacedRoom.Bounds) + Vector3.up * 3f;
                    Gizmos.DrawLine(from, to);
                }
            }
        }
    }

    private Color RoomTypeColorGizmo(RoomType type) => type switch
    {
        RoomType.Start    => Color.green,
        RoomType.Combat   => Color.red,
        RoomType.Treasure => Color.yellow,
        RoomType.Elite    => new Color(1f, 0.5f, 0f),
        RoomType.Boss     => Color.magenta,
        _                 => Color.white
    };

    private Color RoomTypeColor(RoomType type) => type switch
    {
        RoomType.Start    => Color.green,
        RoomType.Combat   => new Color(1f, 0.4f, 0.4f),
        RoomType.Treasure => Color.yellow,
        RoomType.Elite    => new Color(1f, 0.6f, 0.1f),
        RoomType.Boss     => Color.magenta,
        _                 => Color.white
    };
}
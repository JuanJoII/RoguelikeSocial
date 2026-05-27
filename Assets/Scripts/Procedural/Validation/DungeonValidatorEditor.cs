#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(DungeonGenerator))]
public class DungeonValidatorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        GUILayout.Space(10);

        var gen = (DungeonGenerator)target;

        if (GUILayout.Button("▶ Generate in Editor", GUILayout.Height(36)))
            gen.Generate();

        if (GUILayout.Button("♻ Regenerate (new seed)", GUILayout.Height(30)))
        {
            gen.Config.seed = (int)System.DateTime.Now.Ticks;
            gen.Generate();
        }

        if (GUILayout.Button("🗑 Clear", GUILayout.Height(28)))
            gen.Clear();

        GUILayout.Space(6);

        // Estadísticas rápidas
        var rooms = gen.GetRooms();
        if (rooms != null && rooms.Count > 0)
        {
            EditorGUILayout.HelpBox(
                $"Rooms: {rooms.Count} | " +
                $"Boss: {rooms[^1].RoomType} | " +
                $"Grid: {gen.GetGrid()?.Width}×{gen.GetGrid()?.Height}",
                MessageType.Info);
        }
    }
}
#endif
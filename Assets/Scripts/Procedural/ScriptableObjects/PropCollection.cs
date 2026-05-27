using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Dungeon/Prop Collection")]
public class PropCollection : ScriptableObject
{
    public List<PropData> Props = new();

    public List<PropData> GetForRoom(RoomType type)
    {
        var result = new List<PropData>();
        foreach (var p in Props)
        {
            if (p.Prefab == null) continue;
            if (p.AllowedRooms.Count == 0 || p.AllowedRooms.Contains(type))
                result.Add(p);
        }
        return result;
    }
}
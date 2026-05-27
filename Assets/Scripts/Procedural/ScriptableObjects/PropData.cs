using UnityEngine;
using System.Collections.Generic;

public enum PropPlacementRule { AgainstWall, Center, Corner, Random }

[System.Serializable]
public class PropData
{
    public string PropName;
    public GameObject Prefab;
    [Range(0.01f, 100f)] public float Weight;
    public PropPlacementRule PlacementRule;
    public List<RoomType> AllowedRoomTypes; // vacío = todos
    [Range(0f, 1f)] public float SpawnChance = 0.7f;
}

[CreateAssetMenu(menuName = "Dungeon/Prop Collection")]
public class PropCollection : ScriptableObject
{
    public List<PropData> Props;

    public List<PropData> GetForRoomType(RoomType type)
    {
        return Props.FindAll(p =>
            p.AllowedRoomTypes.Count == 0 || p.AllowedRoomTypes.Contains(type));
    }
}
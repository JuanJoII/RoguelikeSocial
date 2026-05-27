using System.Collections.Generic;
using UnityEngine;

public enum RoomType { Start, Combat, Treasure, Elite, Puzzle, Boss }

[System.Serializable]
public class DungeonNode
{
    public int Id;
    public RoomType RoomType;
    public float BudgetModifier; // 1.0 = normal, 1.5 = elite, 0 = Boss gestiona el suyo
    public List<int> ConnectedNodeIds = new();
    public RoomData PlacedRoom; // se asigna en fase 3

    public bool IsMandatory => RoomType == RoomType.Start || RoomType == RoomType.Boss;
}


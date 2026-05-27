using UnityEngine;
using System.Collections.Generic;

public enum PropPlacementRule
{
    AgainstWall,    // shelf, torsher, Cell
    OnFurniture,    // velas, libros, botella — encima de table/shelf
    Corner,         // barrels, box wood
    FloorFree,      // chair, table, bags
    FloorRare,      // chest, coins
}

[System.Serializable]
public class PropData
{
    public string      PropName;
    public GameObject  Prefab;
    public PropPlacementRule PlacementRule;
    [Range(0.01f, 100f)] public float Weight = 50f;
    [Range(0f, 1f)]      public float SpawnChance = 0.7f;

    // Qué tipos de sala permiten este prop
    // Lista vacía = todos los tipos
    public List<RoomType> AllowedRooms = new();

    // Solo aparece si hay un mueble (table/shelf) cercano
    // Solo relevante para OnFurniture
    public bool RequiresFurnitureNearby = false;
}
using UnityEngine;
using System.Collections.Generic;

public class RoomData
{
    public Vector2Int gridPosition;              // Posición en grid (ej: 0,0)
    public Vector2Int size;                     // Tamaño (ej: 20,20)
    public RoomType roomType;                   // Normal, Boss, Treasure, etc
    public BiomeType biomeType;                 // Stone, Ice, Fire, Forest
    public float decorationDensityModifier = 1f; // x1 = normal, x0.5 = menos, x2 = más
    
    // Contenido generado
    public List<Vector3> wallPositions = new List<Vector3>();
    public List<DecorationInstance> decorations = new List<DecorationInstance>();
    public List<Vector3> enemySpawnPoints = new List<Vector3>();
}

public enum RoomType { Normal, Boss, Treasure, Shop }
public enum BiomeType { Stone, Ice, Fire, Forest }

public class DecorationInstance
{
    public string prefabName;
    public Vector3 position;
    public Quaternion rotation;
    public float scale = 1f;
}
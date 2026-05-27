using UnityEngine;

[CreateAssetMenu(fileName = "DungeonConfig", menuName = "Dungeon/Config")]
public class DungeonConfig : ScriptableObject
{
    [Header("Módulo Base")]
    public int cellSize = 4;

    [Header("Tamaño de la Grilla (en celdas)")]
    public int gridWidth  = 40;
    public int gridHeight = 40;

    [Header("Biome Activo")]
    public BiomeConfig currentBiome;

    [Header("Boss Room")]
    public int bossRoomMinCells = 5;
    public int bossRoomMaxCells = 8;

    [Header("Dungeon Depth")]
    public int dungeonDepth = 0;

    [Header("Seed")]
    public int  seed          = 0;
    public bool useRandomSeed = true;

    [Header("Debug")]
    public bool drawGizmos = true;
    public bool logStats   = true;
}
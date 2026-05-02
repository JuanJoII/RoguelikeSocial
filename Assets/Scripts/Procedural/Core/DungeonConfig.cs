using UnityEngine;

[CreateAssetMenu(fileName = "DungeonConfig", menuName = "Dungeon/Config")]
public class DungeonConfig : ScriptableObject
{
    [Header("Layout")]
    public int minRoomsPerDungeon = 5;
    public int maxRoomsPerDungeon = 15;
    public Vector2Int roomSize = new Vector2Int(20, 20);
    
    [Header("Architecture")]
    public float moduleSize = 2f; // Tamaño de cada módulo de pared
    public int wallHeight = 3;    // Altura de paredes en unidades
    
    [Header("Decoration")]
    [Range(0.05f, 0.3f)]
    public float decorationDensity = 0.15f; // 15% de la sala llena
    
    [Header("Difficulty")]
    [Range(1, 5)]
    public int difficultyLevel = 1;
    
    [Header("Seed & Reproducibility")]
    public int dungeonSeed = 12345;
    public bool useRandomSeed = false;
    
    [Header("Debug")]
    public bool drawDebugGizmos = true;
    public bool logGenerationStats = true;
}
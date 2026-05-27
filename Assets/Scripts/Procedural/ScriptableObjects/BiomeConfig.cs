using UnityEngine;

[CreateAssetMenu(menuName = "Dungeon/Biome Config")]
public class BiomeConfig : ScriptableObject
{
    [Header("Identidad")]
    public string BiomeName;
    public Color  BiomeColor = Color.white;

    [Header("Prefabs — Suelos")]
    public GameObject floorRoom;
    public GameObject floorCorridor;

    [Header("Prefabs — Paredes")]
    public GameObject wallStraight;

    [Header("Prefabs — Puertas")]
    public GameObject doorNormal;
    public GameObject doorBoss;

    [Header("Contenido")]
    public EnemyPool      EnemyPool;
    public LootTable      LootTable;
    public PropCollection PropCollection;

    [Header("Audio")]
    public AudioClip AmbientLoop;

    public GameObject GetFloorPrefab(CellType type) =>
        type == CellType.Corridor
            ? (floorCorridor != null ? floorCorridor : floorRoom)
            : (floorRoom     != null ? floorRoom     : floorCorridor);

    public GameObject GetDoorPrefab(bool isBoss) =>
        isBoss && doorBoss != null ? doorBoss
            : (doorNormal != null ? doorNormal : wallStraight);
}
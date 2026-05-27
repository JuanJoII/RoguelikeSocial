using UnityEngine;

[CreateAssetMenu(menuName = "Rooms/RoomConfig")]
public class RoomConfigSO : ScriptableObject
{
    [Header("Enemigos")]
    public int totalEnemiesRequired;

    [Header("Oleadas terrestres")]
    public WaveConfigSO[] groundWaveConfigs;
    public int groundWaveCount = 2;

    [Header("Oleadas aéreas")]
    public WaveConfigSO[] airWaveConfigs;
    public int airWaveCount = 1;

    [Header("Timing")]
    public float timeBetweenWaves = 8f;

    [Header("Sala de Boss")]
    public bool isBossRoom;
    public int bossRoomEnemyCap = 15;

    [Tooltip("Prefab del boss que aparece en esta sala. " +
             "Solo se usa si isBossRoom es true.")]
    public GameObject bossPrefab;

    [Tooltip("Posición relativa al centro de la sala donde spawneará el boss. " +
             "Ajusta en el Inspector según el tamaño de la sala.")]
    public Vector3 bossSpawnOffset = new Vector3(0f, 0f, 5f);
}
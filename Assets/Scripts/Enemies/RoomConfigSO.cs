using UnityEngine;

/// <summary>
/// Configuración completa de una sala.
/// Crea varios assets por nivel de dificultad y métalos en el
/// array possibleConfigs del RoomContext — el sistema elige uno al azar.
/// </summary>
[CreateAssetMenu(menuName = "Rooms/RoomConfig")]
public class RoomConfigSO : ScriptableObject
{
    [Header("Enemigos")]
    [Tooltip("Total de enemigos que deben morir para completar la sala.")]
    public int totalEnemiesRequired;

    [Header("Oleadas terrestres")]
    public WaveConfigSO[] groundWaveConfigs;
    public int groundWaveCount = 2;

    [Header("Oleadas aéreas")]
    public WaveConfigSO[] airWaveConfigs;
    public int airWaveCount = 1;

    [Header("Timing")]
    [Tooltip("Segundos entre el inicio de una oleada y la siguiente.")]
    public float timeBetweenWaves = 8f;

    [Header("Sala de Boss")]
    public bool isBossRoom;

    [Tooltip("Máximo de enemigos vivos simultáneamente en sala de boss. " +
             "Cuando bajan de este número, se lanza nueva oleada.")]
    public int bossRoomEnemyCap = 15;
}
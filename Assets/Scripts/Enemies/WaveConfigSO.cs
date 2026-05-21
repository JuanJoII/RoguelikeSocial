using UnityEngine;

/// <summary>
/// Configuración de una oleada individual.
/// Crea assets via: Enemies > WaveConfig
/// </summary>
[CreateAssetMenu(menuName = "Enemies/WaveConfig")]
public class WaveConfigSO : ScriptableObject
{
    public EnemyDataSO enemyData;

    [Header("Tamaño")]
    [Tooltip("El tamaño real se sortea entre estos valores al spawnear.")]
    public int minEnemies = 5;
    public int maxEnemies = 10;

    [Header("Spawn")]
    [Tooltip("Segundos entre cada enemigo individual al spawnear la oleada.")]
    public float spawnInterval = 0.15f;

    [Tooltip("Tiempo entre que aparece el portal y sale el primer enemigo. " +
             "Dale al jugador tiempo de reaccionar.")]
    public float portalDelay = 1.2f;
}
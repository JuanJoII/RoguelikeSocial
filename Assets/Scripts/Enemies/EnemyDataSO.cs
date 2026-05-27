using UnityEngine;

/// <summary>
/// Define el comportamiento y stats de un tipo de enemigo.
/// Crea assets via: Enemies > EnemyData
///
/// PESOS DE STEERING:
/// Todos los comportamientos de movimiento son vectores que se suman.
/// El peso de cada uno determina cuánto influye en el movimiento final.
///
/// REFERENCIA DE PESOS (punto de partida):
/// chaseWeight: 1.0    — perseguir al jugador es la prioridad
/// separationWeight: 1.5 — separarse de vecinos evita el apilamiento
/// cohesionWeight: 0.5  — mantenerse cerca del grupo, más suave
/// wallAvoidWeight: 2.0 — evitar paredes tiene alta prioridad
/// </summary>
[CreateAssetMenu(menuName = "Enemies/EnemyData")]
public class EnemyDataSO : ScriptableObject
{
    [Header("Stats")]
    public int maxHealth = 3;
    public int contactDamage = 1;
    public EnemyType enemyType;

    [Header("Movimiento")]
    public float moveSpeed = 4f;

    [Header("Pesos de Steering")]
    [Tooltip("Fuerza de persecución al jugador.")]
    public float chaseWeight = 1f;

    [Tooltip("Fuerza de separación de vecinos cercanos. " +
             "Evita el apilamiento visual y el bucle de daño.")]
    public float separationWeight = 1.5f;

    [Tooltip("Radio dentro del cual un vecino activa la fuerza de separación.")]
    public float separationRadius = 1.2f;

    [Tooltip("Fuerza de cohesión hacia el centro del grupo. " +
             "Mantiene la sensación de oleada unida.")]
    public float cohesionWeight = 0.5f;

    [Tooltip("Fuerza de evasión de paredes detectadas con raycast.")]
    public float wallAvoidWeight = 2f;

    [Tooltip("Distancia a la que el raycast detecta paredes.")]
    public float wallDetectionDistance = 1.5f;

    [Header("Salto sobre otros enemigos")]
    [Tooltip("Probabilidad de saltar sobre un enemigo bloqueante (0-1).")]
    [Range(0f, 1f)]
    public float jumpProbability = 0.3f;

    [Tooltip("Cooldown mínimo entre saltos del mismo enemigo.")]
    public float jumpCooldown = 2f;

    [Header("Feedback")]
    public SoundData hurtSound;
    public SoundData deathSound;
    public VFXData deathVFX;
}

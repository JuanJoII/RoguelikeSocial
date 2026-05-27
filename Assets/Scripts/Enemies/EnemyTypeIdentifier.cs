using UnityEngine;

/// <summary>
/// Identifica el tipo de enemigo para el sistema de detección y proyectiles.
/// Va en el GameObject que tiene el Collider del enemigo.
///
/// Separado del EnemyAI para que ProjectileBase y WeaponSystem
/// puedan preguntar el tipo sin depender de la clase EnemyAI completa.
/// </summary>
public class EnemyTypeIdentifier : MonoBehaviour
{
    [SerializeField] private EnemyType enemyType;
    public EnemyType EnemyType => enemyType;
}
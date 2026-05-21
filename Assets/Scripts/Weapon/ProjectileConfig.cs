using UnityEngine;

/// <summary>
/// Configuración de un tipo de proyectil.
/// Crea assets via: Weapons > ProjectileConfig
///
/// Tendrás dos assets: GroundProjectileConfig y AirProjectileConfig.
/// El WeaponSystem los referencia en el Inspector.
/// </summary>
[CreateAssetMenu(menuName = "Weapons/ProjectileConfig")]
public class ProjectileConfig : ScriptableObject
{
    [Header("Stats")]
    public int damage = 1;
    public float speed = 20f;

    [Header("Alcance")]
    [Tooltip("Distancia máxima antes de que el proyectil se devuelva al pool.")]
    public float maxRange = 15f;

    [Header("Feedback de impacto")]
    public VFXData hitVFX;
    public VFXData wrongTypeVFX; // VFX cuando la bala no es del tipo correcto
    public SoundData hitSound;
}
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Trigger en el menú 3D que carga una escena según su tipo.
/// Coloca este script en cada portal/puerta del menú.
/// 
/// SETUP:
/// 1. Agrega un Collider con Is Trigger activado
/// 2. Asigna el PortalType en el Inspector
/// 3. El jugador debe tener el tag "Player"
/// </summary>
[RequireComponent(typeof(Collider))]
public class MenuPortalTrigger : MonoBehaviour
{
    public enum PortalType { Grey, Red, Green }

    [SerializeField] private PortalType portalType;

    // Nombres de escena configurables desde el Inspector
    // por si cambian sin tener que tocar el código
    [Header("Nombres de escenas")]
    [SerializeField] private string greySceneName  = "Level_01";
    [SerializeField] private string redSceneName   = "Level_02";
    [SerializeField] private string greenSceneName = "Level_00";

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        string sceneToLoad = portalType switch
        {
            PortalType.Grey  => greySceneName,
            PortalType.Red   => redSceneName,
            PortalType.Green => greenSceneName,
            _                => greySceneName
        };

        SceneManager.LoadScene(sceneToLoad);
    }
}
using UnityEngine;
public class LevelInitializer : MonoBehaviour
{
    [SerializeField] private DungeonGenerator dungeonGenerator;
    [SerializeField] private DungeonGameplayIntegrator integrator;

    private void Start()
    {
        dungeonGenerator.Config.dungeonDepth = RunManager.Instance.CurrentLevel;
        dungeonGenerator.Generate();
        integrator.Integrate(); // Generate() ya terminó — es síncrono
    }
}
using UnityEngine;

[CreateAssetMenu(menuName = "Dungeon/Enemy Data")]
public class EnemyData : ScriptableObject
{
    public string EnemyName;
    public GameObject Prefab;
    public int ThreatCost;
    [Range(0.01f, 100f)] public float Weight;
    public int MinDepth = 0;
}
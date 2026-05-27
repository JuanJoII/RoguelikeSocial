using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Dungeon/Enemy Pool")]
public class EnemyPool : ScriptableObject
{
    public List<EnemyData> Enemies;
}
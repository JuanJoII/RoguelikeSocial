using System.Collections.Generic;
using UnityEngine;
public class DungeonGraphGenerator : MonoBehaviour
{
    public DungeonGraph Generate(DungeonDifficultyConfig diff, System.Random rng)
    {
        var graph = new DungeonGraph();
        int id = 0;

        // Nodo Start (siempre)
        var start = new DungeonNode { Id = id++, RoomType = RoomType.Start };
        graph.Nodes.Add(start);

        var prev = start;

        // Combat rooms (cantidad según dificultad)
        int combatCount = rng.Next(diff.minCombatRooms, diff.maxCombatRooms + 1);
        for (int i = 0; i < combatCount; i++)
        {
            var node = new DungeonNode
            {
                Id = id++,
                RoomType = RoomType.Combat,
                BudgetModifier = 1f + (i * 0.1f) // escalar con profundidad
            };
            graph.Nodes.Add(node);
            graph.Connect(prev, node);
            prev = node;
        }

        // Treasure room (probabilidad según config)
        if (rng.NextDouble() < diff.treasureRoomChance)
        {
            var treasure = new DungeonNode { Id = id++, RoomType = RoomType.Treasure };
            graph.Nodes.Add(treasure);
            // Se conecta al último combat, no en la ruta principal
            graph.Connect(graph.Nodes[^2], treasure);
        }

        // Elite room (después de varios combats)
        if (combatCount >= 3 && rng.NextDouble() < diff.eliteRoomChance)
        {
            var elite = new DungeonNode
            {
                Id = id++,
                RoomType = RoomType.Elite,
                BudgetModifier = 2f
            };
            graph.Nodes.Add(elite);
            graph.Connect(prev, elite);
            prev = elite;
        }

        // Boss (siempre, al final)
        var boss = new DungeonNode { Id = id++, RoomType = RoomType.Boss };
        graph.Nodes.Add(boss);
        graph.Connect(prev, boss);

        return graph;
    }
}
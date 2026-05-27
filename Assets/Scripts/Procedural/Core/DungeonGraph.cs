using System.Collections.Generic;
using UnityEngine;
public class DungeonGraph
{
    public List<DungeonNode> Nodes = new();
    public DungeonNode StartNode => Nodes.Find(n => n.RoomType == RoomType.Start);
    public DungeonNode BossNode  => Nodes.Find(n => n.RoomType == RoomType.Boss);

    public void Connect(DungeonNode a, DungeonNode b)
    {
        if (!a.ConnectedNodeIds.Contains(b.Id)) a.ConnectedNodeIds.Add(b.Id);
        if (!b.ConnectedNodeIds.Contains(a.Id)) b.ConnectedNodeIds.Add(a.Id);
    }
}
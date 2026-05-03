using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Conecta todas las salas garantizando accesibilidad 100%.
/// 
/// MEJORAS vs versión anterior:
///   - Los pasillos conectan borde-a-borde (no centro-a-centro),
///     lo que los hace significativamente más cortos.
///   - Se usa el punto del borde más cercano al vecino para minimizar distancia.
///   - El corridor width y extra connections vienen del DifficultyConfig.
/// 
/// Algoritmo:
///   1. Kruskal MST (distancia borde-a-borde, no centro-a-centro)
///   2. Conexiones extra según difficulty.extraConnectionRatio
///   3. Pasillos Manhattan desde el borde más cercano de cada sala
/// </summary>
public class SmartConnectionGenerator : MonoBehaviour
{
    [SerializeField] private DungeonConfig config;

    // ─────────────────────────────────────────────
    // API PÚBLICA
    // ─────────────────────────────────────────────

    public void ConnectRooms(List<RoomData> rooms, DungeonGrid grid,
                              DungeonDifficultyConfig difficulty)
    {
        if (rooms == null || rooms.Count < 2)
        {
            Log("Menos de 2 salas — nada que conectar");
            return;
        }

        Log($"Conectando {rooms.Count} salas | " +
            $"PassilloW: {difficulty.corridorWidth}c | " +
            $"Extra: {difficulty.extraConnectionRatio * 100:F0}%");

        // ── Paso 1: todas las aristas (distancia borde a borde) ──────
        List<Edge> allEdges = BuildEdgesEdgeToEdge(rooms);

        // ── Paso 2: MST ──────────────────────────────────────────────
        List<Edge> mstEdges = Kruskal(allEdges, rooms.Count);
        Log($"  MST: {mstEdges.Count} aristas");

        // ── Paso 3: conexiones extra ─────────────────────────────────
        int extraCount = Mathf.RoundToInt(mstEdges.Count * difficulty.extraConnectionRatio);
        List<Edge> extras = GetExtraEdges(allEdges, mstEdges, extraCount);
        Log($"  Extra: {extras.Count} aristas");

        // ── Paso 4: registrar conexiones bidireccionales ─────────────
        List<Edge> finalEdges = new List<Edge>(mstEdges);
        finalEdges.AddRange(extras);

        foreach (var edge in finalEdges)
        {
            RoomData a = rooms[edge.FromId];
            RoomData b = rooms[edge.ToId];
            if (!a.ConnectedRoomIds.Contains(b.Id)) a.ConnectedRoomIds.Add(b.Id);
            if (!b.ConnectedRoomIds.Contains(a.Id)) b.ConnectedRoomIds.Add(a.Id);
        }

        // ── Paso 5: trazar pasillos (borde → borde, Manhattan) ───────
        foreach (var edge in finalEdges)
        {
            RoomData a = rooms[edge.FromId];
            RoomData b = rooms[edge.ToId];

            // Puntos de entrada: borde más cercano de cada sala al centro del otro
            Vector2Int entryA = ClosestBorderPoint(a, b.CenterCellInt);
            Vector2Int entryB = ClosestBorderPoint(b, a.CenterCellInt);

            CarveCorridorManhattan(entryA, entryB, difficulty.corridorWidth, grid);
        }

        Log($"Conexiones completas: {finalEdges.Count} pasillos");
    }

    // ─────────────────────────────────────────────
    // CONSTRUCCIÓN DE GRAFO (borde a borde)
    // ─────────────────────────────────────────────

    /// <summary>
    /// Calcula las aristas usando distancia BORDE-A-BORDE, no centro-a-centro.
    /// Esto hace que el MST prefiera salas realmente próximas y los pasillos
    /// resultantes sean los más cortos posibles.
    /// </summary>
    private List<Edge> BuildEdgesEdgeToEdge(List<RoomData> rooms)
    {
        var edges = new List<Edge>();

        for (int i = 0; i < rooms.Count; i++)
        for (int j = i + 1; j < rooms.Count; j++)
        {
            // Punto de entrada de i mirando hacia j
            Vector2Int pA = ClosestBorderPoint(rooms[i], rooms[j].CenterCellInt);
            // Punto de entrada de j mirando hacia i
            Vector2Int pB = ClosestBorderPoint(rooms[j], rooms[i].CenterCellInt);

            // Distancia Manhattan entre los dos puntos de borde
            float dist = Mathf.Abs(pA.x - pB.x) + Mathf.Abs(pA.y - pB.y);
            edges.Add(new Edge(rooms[i].Id, rooms[j].Id, dist));
        }

        edges.Sort((a, b) => a.Cost.CompareTo(b.Cost));
        return edges;
    }

    /// <summary>
    /// Devuelve el punto del borde de una sala más cercano a un objetivo.
    /// El resultado está DENTRO de los límites de la sala (no fuera).
    /// </summary>
    private Vector2Int ClosestBorderPoint(RoomData room, Vector2Int target)
    {
        // Clampear el target al interior del rectángulo de la sala
        int cx = Mathf.Clamp(target.x, room.Bounds.x, room.Bounds.x + room.Bounds.width  - 1);
        int cy = Mathf.Clamp(target.y, room.Bounds.y, room.Bounds.y + room.Bounds.height - 1);
        return new Vector2Int(cx, cy);
    }

    // ─────────────────────────────────────────────
    // KRUSKAL MST
    // ─────────────────────────────────────────────

    private List<Edge> Kruskal(List<Edge> sortedEdges, int nodeCount)
    {
        int[] parent = new int[nodeCount];
        int[] rank   = new int[nodeCount];
        for (int i = 0; i < nodeCount; i++) parent[i] = i;

        var mst = new List<Edge>();

        foreach (var edge in sortedEdges)
        {
            int rootA = Find(parent, edge.FromId);
            int rootB = Find(parent, edge.ToId);

            if (rootA != rootB)
            {
                mst.Add(edge);
                Union(parent, rank, rootA, rootB);
                if (mst.Count == nodeCount - 1) break;
            }
        }

        return mst;
    }

    private int Find(int[] parent, int i)
    {
        if (parent[i] != i) parent[i] = Find(parent, parent[i]);
        return parent[i];
    }

    private void Union(int[] parent, int[] rank, int a, int b)
    {
        if      (rank[a] < rank[b]) parent[a] = b;
        else if (rank[a] > rank[b]) parent[b] = a;
        else { parent[b] = a; rank[a]++; }
    }

    // ─────────────────────────────────────────────
    // CONEXIONES EXTRA
    // ─────────────────────────────────────────────

    private List<Edge> GetExtraEdges(List<Edge> all, List<Edge> mst, int count)
    {
        var mstSet = new HashSet<(int, int)>();
        foreach (var e in mst)
            mstSet.Add((Mathf.Min(e.FromId, e.ToId), Mathf.Max(e.FromId, e.ToId)));

        var extras = new List<Edge>();
        foreach (var e in all)
        {
            if (extras.Count >= count) break;
            int a = Mathf.Min(e.FromId, e.ToId);
            int b = Mathf.Max(e.FromId, e.ToId);
            if (!mstSet.Contains((a, b))) extras.Add(e);
        }
        return extras;
    }

    // ─────────────────────────────────────────────
    // PASILLO MANHATTAN (borde a borde)
    // ─────────────────────────────────────────────

    /// <summary>
    /// Traza un pasillo en L desde 'from' hasta 'to'.
    /// Elige automáticamente si ir primero en X o en Y según cuál
    /// dimensión es mayor (minimiza el doblez del pasillo).
    /// </summary>
    private void CarveCorridorManhattan(Vector2Int from, Vector2Int to,
                                         int width, DungeonGrid grid)
    {
        int dx = Mathf.Abs(to.x - from.x);
        int dy = Mathf.Abs(to.y - from.y);

        Vector2Int corner;

        // Si una dimensión es mucho mayor, no hay "L" real — es casi recto.
        // Elegir el doblez que produce el pasillo más compacto.
        if (dx >= dy)
        {
            // Moverse primero en X, luego en Y
            corner = new Vector2Int(to.x, from.y);
        }
        else
        {
            // Moverse primero en Y, luego en X
            corner = new Vector2Int(from.x, to.y);
        }

        CarveLine(from, corner, width, grid);
        CarveLine(corner, to,   width, grid);
    }

    private void CarveLine(Vector2Int from, Vector2Int to, int width, DungeonGrid grid)
    {
        if (from == to) return;

        int halfW = width / 2;
        int dx    = (to.x > from.x) ? 1 : (to.x < from.x) ? -1 : 0;
        int dy    = (to.y > from.y) ? 1 : (to.y < from.y) ? -1 : 0;

        bool horizontal = dy == 0;
        Vector2Int current = from;

        while (current != to)
        {
            MarkCorridor(current, horizontal, halfW, grid);
            current += new Vector2Int(dx, dy);
        }
        MarkCorridor(to, horizontal, halfW, grid);
    }

    private void MarkCorridor(Vector2Int pos, bool horizontal, int halfW, DungeonGrid grid)
    {
        if (horizontal)
        {
            for (int offset = -halfW; offset <= halfW; offset++)
                grid.SetCell(new Vector2Int(pos.x, pos.y + offset), CellType.Corridor);
        }
        else
        {
            for (int offset = -halfW; offset <= halfW; offset++)
                grid.SetCell(new Vector2Int(pos.x + offset, pos.y), CellType.Corridor);
        }
    }

    // ─────────────────────────────────────────────
    // TIPOS INTERNOS
    // ─────────────────────────────────────────────

    private struct Edge
    {
        public int   FromId;
        public int   ToId;
        public float Cost;
        public Edge(int f, int t, float c) { FromId = f; ToId = t; Cost = c; }
    }

    // ─────────────────────────────────────────────
    // UTILIDADES
    // ─────────────────────────────────────────────

    private void Log(string msg)
    {
        if (config != null && config.logStats)
            Debug.Log($"[CONNECTION] {msg}");
    }
}
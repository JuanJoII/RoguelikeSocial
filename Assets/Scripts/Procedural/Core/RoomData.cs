using UnityEngine;
using System.Collections.Generic;

public class RoomData
{
    // ── Identidad ───────────────────────────────────
    public int      Id;
    public RoomType RoomType;
    public RectInt  Bounds;
    public List<int> ConnectedRoomIds = new();

    // ── Grafo ───────────────────────────────────────
    public DungeonNode GraphNode; // nodo lógico que originó esta sala

    // ── Sockets de puertas ──────────────────────────
    public List<DoorSocket> Sockets = new();

    // ── Contenido resuelto (poblado en fase 6) ──────
    public List<EnemyData>  ResolvedEnemies = new();
    public List<LootEntry>  ResolvedLoot    = new();
    public List<PropData>   ResolvedProps   = new();

    // ── Instancias en escena ────────────────────────
    public List<GameObject> SpawnedEnemies = new();
    public List<GameObject> SpawnedLoot    = new();
    public List<GameObject> SpawnedProps   = new();

    // ── Estado runtime ──────────────────────────────
    public bool IsCleared  = false; // todos los enemigos derrotados
    public bool IsVisited  = false;
    public bool IsRevealed = false; // visible en minimap

    // ── Helpers ─────────────────────────────────────
    public Vector2Int CenterCellInt => new(
        Bounds.x + Bounds.width  / 2,
        Bounds.y + Bounds.height / 2);

    public string DebugLabel =>
        $"[{Id}:{RoomType}] {Bounds.width}×{Bounds.height} @({Bounds.x},{Bounds.y})";

    // Activa/desactiva enemigos cuando el jugador entra/sale
    public void SetEnemiesActive(bool active)
    {
        foreach (var go in SpawnedEnemies)
            if (go != null) go.SetActive(active);
    }

    // Llamado cuando el jugador derrota a todos los enemigos
    public void OnCleared()
    {
        IsCleared = true;
        // Abrir puertas, spawnar loot especial, etc.
        Debug.Log($"[ROOM] {DebugLabel} cleared!");
    }
}
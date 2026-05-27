using UnityEngine;
using System.Collections.Generic;

/// Fuente de verdad para la generación visual del dungeon.
/// 
/// SISTEMA DE SNAP:
///   - Suelos: 1 prefab escalado por sala (pivot centro, sin SnapPoints)
///   - Paredes y puertas: posicionadas via SnapPoints
///   - El DungeonGrid sigue siendo la fuente lógica (qué va dónde)
///   - Este sistema convierte esa lógica en geometría usando snap
///
/// SETUP DE PREFABS:
///   Pared: añadir RoomModule + 1 SnapPoint hijo en cada extremo lateral
///          El forward del SnapPoint apunta HACIA AFUERA del prefab
///   Puerta: igual que pared
public class RoomArchitectureGenerator : MonoBehaviour
{
    [SerializeField] private DungeonConfig config;
    private BiomeConfig Biome => config.currentBiome;

    [Header("Dimensiones de referencia del prefab de pared")]
    [Tooltip("Ancho real del prefab de pared en unidades Unity (eje X)")]
    [SerializeField] private float wallPrefabWidth = 4f;
    [Tooltip("Alto real del prefab de pared en unidades Unity (eje Y)")]
    [SerializeField] private float wallHeight = 3f;

    // Contenedores de jerarquía
    private Transform floorParent;
    private Transform wallParent;
    private Transform doorParent;

    // Datos de sala boss para puertas especiales
    private RoomData bossRoom;

    // Direcciones en coordenadas de celda
    private static readonly Vector2Int DirN = new( 0,  1);
    private static readonly Vector2Int DirE = new( 1,  0);
    private static readonly Vector2Int DirS = new( 0, -1);
    private static readonly Vector2Int DirW = new(-1,  0);

    // Gizmos de diagnóstico
    private struct GizmoItem
    {
        public Vector3 Pos;
        public Color   Col;
        public Vector3 Size;
    }
    private readonly List<GizmoItem> gizmos = new();

    private void OnDrawGizmos()
    {
        foreach (var g in gizmos)
        {
            Gizmos.color = g.Col;
            Gizmos.DrawWireCube(g.Pos, g.Size);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // API PÚBLICA
    // ─────────────────────────────────────────────────────────────

    public void BuildGeometry(DungeonGrid grid, List<RoomData> rooms)
    {
        if (config == null)
        {
            Debug.LogError("[ARCH] DungeonConfig no asignado"); return;
        }
        if (Biome == null)
        {
            Debug.LogError("[ARCH] BiomeConfig no asignado en DungeonConfig"); return;
        }

        ClearGeometry();
        gizmos.Clear();

        floorParent = MakeContainer("_Floors");
        wallParent  = MakeContainer("_Walls");
        doorParent  = MakeContainer("_Doors");

        bossRoom = FindBossRoom(rooms);

        int floors = 0, walls = 0, doors = 0, corridorFloors = 0;

        // ── Paso 1: Suelo de salas ────────────────────────────────
        if (rooms != null)
            foreach (var room in rooms)
            {
                PlaceRoomFloor(room, grid);
                floors++;
            }

        // ── Paso 2: Paredes de salas via Snap ────────────────────
        if (rooms != null)
            foreach (var room in rooms)
                PlaceRoomWalls(room, grid, ref walls, ref doors);

        // ── Paso 3: Suelo de pasillos ────────────────────────────
        for (int x = 0; x < grid.Width;  x++)
        for (int y = 0; y < grid.Height; y++)
        {
            var cell = new Vector2Int(x, y);
            if (grid.GetCell(cell) == CellType.Corridor)
            {
                PlaceCorridorFloor(cell, grid);
                corridorFloors++;
            }
        }

        // ── Paso 4: Paredes de pasillos via Snap ─────────────────
        for (int x = 0; x < grid.Width;  x++)
        for (int y = 0; y < grid.Height; y++)
        {
            var cell = new Vector2Int(x, y);
            if (grid.GetCell(cell) != CellType.Empty) continue;

            bool flN = grid.IsFloor(cell + DirN);
            bool flE = grid.IsFloor(cell + DirE);
            bool flS = grid.IsFloor(cell + DirS);
            bool flW = grid.IsFloor(cell + DirW);
            int  cnt = (flN?1:0)+(flE?1:0)+(flS?1:0)+(flW?1:0);

            if (cnt == 0) continue;

            // Solo colocamos pared en los lados que dan a Corridor.
            // Los lados que dan a Room ya tienen su pared generada en PlaceRoomWalls,
            // pero la celda vacía de esquina aún puede tener lados hacia Corridor
            // que necesitan cerrarse.
            bool wallN = flN && grid.GetCell(cell + DirN) == CellType.Corridor;
            bool wallE = flE && grid.GetCell(cell + DirE) == CellType.Corridor;
            bool wallS = flS && grid.GetCell(cell + DirS) == CellType.Corridor;
            bool wallW = flW && grid.GetCell(cell + DirW) == CellType.Corridor;

            if (!wallN && !wallE && !wallS && !wallW) continue;

            PlaceCorridorWall(cell, wallN, wallE, wallS, wallW, grid);
            walls++;
        }

        Log($"Geometría → {floors} salas | {corridorFloors} pasillos | " +
            $"{walls} paredes | {doors} puertas");
    }

    public void ClearGeometry()
    {
        DestroyContainer("_Floors");
        DestroyContainer("_Walls");
        DestroyContainer("_Doors");
        gizmos.Clear();
        bossRoom = null;
    }

    // ─────────────────────────────────────────────────────────────
    // SUELO DE SALA — 1 prefab escalado, sin SnapPoints
    // ─────────────────────────────────────────────────────────────

    private void PlaceRoomFloor(RoomData room, DungeonGrid grid)
    {
        var prefab = Biome.GetFloorPrefab(CellType.Room);
        if (prefab == null) return;

        // Centro exacto de la sala en mundo
        Vector3 center = grid.RectCenter(room.Bounds);
        center.y = 0f;

        // Tamaño real del prefab desde su renderer
        var renderer = prefab.GetComponentInChildren<Renderer>();
        float prefabW = renderer != null ? renderer.bounds.size.x : wallPrefabWidth;
        float prefabD = renderer != null ? renderer.bounds.size.z : wallPrefabWidth;
        if (prefabW < 0.01f) prefabW = wallPrefabWidth;
        if (prefabD < 0.01f) prefabD = wallPrefabWidth;

        float targetW = room.Bounds.width  * grid.CellSize;
        float targetD = room.Bounds.height * grid.CellSize;

        var go = Instantiate(prefab, center, Quaternion.identity, floorParent);
        go.transform.localScale = new Vector3(
            targetW / prefabW,
            1f,
            targetD / prefabD);
        go.name = $"Floor_{room.DebugLabel}";

        gizmos.Add(new GizmoItem {
            Pos  = center + Vector3.up * 0.05f,
            Col  = new Color(0.2f, 1f, 0.2f, 0.5f),
            Size = new Vector3(targetW, 0.1f, targetD)
        });
    }

    // ─────────────────────────────────────────────────────────────
    // PAREDES DE SALA — via SnapPoints
    // ─────────────────────────────────────────────────────────────

    private void PlaceRoomWalls(RoomData room, DungeonGrid grid,
                                 ref int walls, ref int doors)
    {
        int   cs    = grid.CellSize;
        float wallY = wallHeight * 0.5f;
        bool  isBoss = bossRoom != null && room.Id == bossRoom.Id;

        float worldMinX = room.Bounds.x * cs;
        float worldMaxX = (room.Bounds.x + room.Bounds.width)  * cs;
        float worldMinZ = room.Bounds.y * cs;
        float worldMaxZ = (room.Bounds.y + room.Bounds.height) * cs;

        // Norte — Z máximo, cara mira hacia interior (Sur = 180°)
        for (int x = room.Bounds.x; x < room.Bounds.x + room.Bounds.width; x++)
        {
            var outside = new Vector2Int(x, room.Bounds.y + room.Bounds.height);
            bool corridor = grid.GetCell(outside) == CellType.Corridor;
            var pos = new Vector3(x * cs + cs * 0.5f, wallY, worldMaxZ);
            PlaceWallSnap(pos, Quaternion.Euler(0f, 180f, 0f),
                          corridor && isBoss, corridor, ref walls, ref doors);
        }

        // Sur — Z mínimo, cara mira hacia interior (Norte = 0°)
        for (int x = room.Bounds.x; x < room.Bounds.x + room.Bounds.width; x++)
        {
            var outside = new Vector2Int(x, room.Bounds.y - 1);
            bool corridor = grid.GetCell(outside) == CellType.Corridor;
            var pos = new Vector3(x * cs + cs * 0.5f, wallY, worldMinZ);
            PlaceWallSnap(pos, Quaternion.Euler(0f, 0f, 0f),
                          corridor && isBoss, corridor, ref walls, ref doors);
        }

        // Este — X máximo, cara mira hacia interior (Oeste = 270°)
        for (int y = room.Bounds.y; y < room.Bounds.y + room.Bounds.height; y++)
        {
            var outside = new Vector2Int(room.Bounds.x + room.Bounds.width, y);
            bool corridor = grid.GetCell(outside) == CellType.Corridor;
            var pos = new Vector3(worldMaxX, wallY, y * cs + cs * 0.5f);
            PlaceWallSnap(pos, Quaternion.Euler(0f, 270f, 0f),
                          corridor && isBoss, corridor, ref walls, ref doors);
        }

        // Oeste — X mínimo, cara mira hacia interior (Este = 90°)
        for (int y = room.Bounds.y; y < room.Bounds.y + room.Bounds.height; y++)
        {
            var outside = new Vector2Int(room.Bounds.x - 1, y);
            bool corridor = grid.GetCell(outside) == CellType.Corridor;
            var pos = new Vector3(worldMinX, wallY, y * cs + cs * 0.5f);
            PlaceWallSnap(pos, Quaternion.Euler(0f, 90f, 0f),
                          corridor && isBoss, corridor, ref walls, ref doors);
        }
    }

    /// Instancia una pared o puerta en la posición/rotación dada.
    /// Si el prefab tiene RoomModule, usa su SnapPoint para ajuste fino.
    /// Si no tiene RoomModule, coloca directo (compatibilidad con prefabs simples).
    private void PlaceWallSnap(Vector3 targetPos, Quaternion targetRot,
                                bool forceDoor, bool isCorridor,
                                ref int walls, ref int doors)
    {
        if (isCorridor && !forceDoor) return; // apertura de pasillo — no poner pared

        GameObject prefab = forceDoor
            ? Biome.GetDoorPrefab(true)
            : Biome.wallStraight;

        if (prefab == null) return;

        var go = Instantiate(prefab, targetPos, targetRot,
                             forceDoor ? doorParent : wallParent);
        go.transform.localScale = Vector3.one;

        // Si el prefab tiene RoomModule, usar el SnapPoint para ajuste fino
        var module = go.GetComponent<RoomModule>();
        if (module != null)
        {
            var snap = module.GetFreeSnap();
            if (snap != null)
            {
                Vector3 snapLocalOffset = go.transform.InverseTransformPoint(snap.transform.position);
                go.transform.position = targetPos - go.transform.TransformVector(snapLocalOffset);
                snap.IsOccupied = true;
            }
        }

        // Gizmo
        Color gizmoCol = forceDoor
            ? new Color(1f, 0.2f, 0.2f, 0.8f)
            : new Color(0.8f, 0.8f, 0.8f, 0.5f);
        gizmos.Add(new GizmoItem {
            Pos  = go.transform.position,
            Col  = gizmoCol,
            Size = new Vector3(wallPrefabWidth, wallHeight, 0.3f)
        });

        if (forceDoor) doors++; else walls++;
    }

    // ─────────────────────────────────────────────────────────────
    // SUELO Y PAREDES DE PASILLO
    // ─────────────────────────────────────────────────────────────

    private void PlaceCorridorFloor(Vector2Int cell, DungeonGrid grid)
    {
        var prefab = Biome.GetFloorPrefab(CellType.Corridor);
        if (prefab == null) return;

        Vector3 pos = grid.CellCenter(cell);
        pos.y = 0f;

        bool hasN = grid.IsFloor(cell + DirN);
        bool hasS = grid.IsFloor(cell + DirS);
        bool hasE = grid.IsFloor(cell + DirE);
        bool hasW = grid.IsFloor(cell + DirW);

        // Rotar si el pasillo va en Z (Norte-Sur)
        bool goesNS = (hasN || hasS) && !(hasE || hasW);
        Quaternion rot = goesNS
            ? Quaternion.Euler(0f, 90f, 0f)
            : Quaternion.identity;

        float cs = grid.CellSize;

        var renderer = prefab.GetComponentInChildren<Renderer>();
        float prefabW = renderer != null ? renderer.bounds.size.x : wallPrefabWidth;
        float prefabD = renderer != null ? renderer.bounds.size.z : wallPrefabWidth;
        if (prefabW < 0.01f) prefabW = wallPrefabWidth;
        if (prefabD < 0.01f) prefabD = wallPrefabWidth;

        var go = Instantiate(prefab, pos, rot, floorParent);
        go.transform.localScale = new Vector3(cs / prefabW, 1f, cs / prefabD);

        gizmos.Add(new GizmoItem {
            Pos  = pos + Vector3.up * 0.02f,
            Col  = new Color(1f, 0.85f, 0.1f, 0.3f),
            Size = new Vector3(cs * 0.9f, 0.05f, cs * 0.9f)
        });
    }

    private void PlaceCorridorWall(Vector2Int cell, bool flN, bool flE, bool flS, bool flW,
                                    DungeonGrid grid)
    {
        if (Biome.wallStraight == null) return;

        int cs = grid.CellSize;

        // Una pared por cada dirección con vecino de suelo.
        // Así las esquinas e intersecciones de pasillo quedan tapadas
        // en todos sus lados sin dejar huecos.
        if (flN) PlaceOneSideWall(cell, cs,
            new Vector3(cell.x * cs + cs * 0.5f, wallHeight * 0.5f, (cell.y + 1) * cs),
            Quaternion.Euler(0f, 180f, 0f));

        if (flS) PlaceOneSideWall(cell, cs,
            new Vector3(cell.x * cs + cs * 0.5f, wallHeight * 0.5f, cell.y * cs),
            Quaternion.Euler(0f, 0f, 0f));

        if (flE) PlaceOneSideWall(cell, cs,
            new Vector3((cell.x + 1) * cs, wallHeight * 0.5f, cell.y * cs + cs * 0.5f),
            Quaternion.Euler(0f, 270f, 0f));

        if (flW) PlaceOneSideWall(cell, cs,
            new Vector3(cell.x * cs, wallHeight * 0.5f, cell.y * cs + cs * 0.5f),
            Quaternion.Euler(0f, 90f, 0f));
    }

    /// Instancia exactamente una pared de pasillo en el borde indicado.
    private void PlaceOneSideWall(Vector2Int cell, int cs, Vector3 pos, Quaternion rot)
    {
        var go = Instantiate(Biome.wallStraight, pos, rot, wallParent);
        go.transform.localScale = Vector3.one;

        // Ajuste fino con SnapPoint si el prefab lo tiene
        var module = go.GetComponent<RoomModule>();
        if (module != null)
        {
            var snap = module.GetFreeSnap();
            if (snap != null)
            {
                Vector3 snapLocalOffset = go.transform.InverseTransformPoint(snap.transform.position);
                go.transform.position = pos - go.transform.TransformVector(snapLocalOffset);
                snap.IsOccupied = true;
            }
        }

        gizmos.Add(new GizmoItem {
            Pos  = pos,
            Col  = new Color(0.7f, 0.7f, 0.7f, 0.4f),
            Size = new Vector3(cs, wallHeight, 0.3f)
        });
    }

    // ─────────────────────────────────────────────────────────────
    // BOSS ROOM
    // ─────────────────────────────────────────────────────────────

    private RoomData FindBossRoom(List<RoomData> rooms)
    {
        if (rooms == null || rooms.Count == 0) return null;
        var last = rooms[^1];
        return last.RoomType == RoomType.Boss ? last : null;
    }

    // ─────────────────────────────────────────────────────────────
    // CONTENEDORES
    // ─────────────────────────────────────────────────────────────

    private Transform MakeContainer(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform);
        go.transform.localPosition = Vector3.zero;
        return go.transform;
    }

    private void DestroyContainer(string name)
    {
        var t = transform.Find(name);
        if (t == null) return;
#if UNITY_EDITOR
        DestroyImmediate(t.gameObject);
#else
        Destroy(t.gameObject);
#endif
    }

    private void Log(string msg)
    {
        if (config != null && config.logStats)
            Debug.Log($"[ARCHITECTURE] {msg}");
    }
}
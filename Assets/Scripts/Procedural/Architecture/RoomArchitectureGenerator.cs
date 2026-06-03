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
/// CORRECCIÓN DE PIVOT (v2):
///   PlaceRoomFloor ya NO asume que el pivot del prefab está centrado.
///   El flujo es:
///     1. Instanciar en (0,0,0)
///     2. Escalar usando el tamaño real del Renderer (no del prefab en disco)
///     3. Leer renderer.bounds.center DESPUÉS de escalar
///     4. Desplazar el objeto para que ese centro coincida con grid.RectCenter()
///   Resultado: el suelo visual siempre queda alineado al Grid aunque el pivot
///   esté en una esquina, en el borde, o en cualquier lugar arbitrario.
public class RoomArchitectureGenerator : MonoBehaviour
{
    [SerializeField] private DungeonConfig config;
    private BiomeConfig Biome => config.currentBiome;

    [Header("Dimensiones de referencia del prefab de pared")]
    [Tooltip("Ancho real del prefab de pared en unidades Unity (eje X)")]
    [SerializeField] private float wallPrefabWidth = 4f;
    [Tooltip("Alto real del prefab de pared en unidades Unity (eje Y)")]
    [SerializeField] private float wallHeight = 3f;

    [Header("Diagnóstico de Floors")]
    [Tooltip("Activa los logs y DrawLines de comparación Grid Center vs Floor Visual Center")]
    [SerializeField] private bool diagFloors = true;
    [Tooltip("Duración en segundos de las líneas de diagnóstico en SceneView")]
    [SerializeField] private float diagDuration = 30f;

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

        // ── Paso 2: Diagnóstico post-floor ───────────────────────
        // Se ejecuta DESPUÉS de colocar todos los floors para poder
        // comparar posición grid vs posición visual real.
        if (diagFloors && rooms != null)
            foreach (var room in rooms)
                DiagnoseFloor(room, grid);

        // ── Paso 3: Paredes de salas via Snap ────────────────────
        if (rooms != null)
            foreach (var room in rooms)
                PlaceRoomWalls(room, grid, ref walls, ref doors);

        // ── Paso 4: Suelo de pasillos ────────────────────────────
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

        // ── Paso 5: Paredes de pasillos via Snap ─────────────────
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
    // SUELO DE SALA — pivot-independent (v2)
    // ─────────────────────────────────────────────────────────────

    private void PlaceRoomFloor(RoomData room, DungeonGrid grid)
    {
        var prefab = Biome.GetFloorPrefab(CellType.Room);
        if (prefab == null) return;

        // ── PASO 1: Instanciar en el origen ──────────────────────
        // Instanciar en (0,0,0) para que renderer.bounds esté en
        // espacio mundo sin transformaciones heredadas del padre.
        // Reparentamos después de posicionar (igual que PropDecorator).
        var go = Instantiate(prefab, Vector3.zero, Quaternion.identity);
        go.name = $"Floor_{room.DebugLabel}";

        // ── PASO 2: Medir el prefab real instanciado ─────────────
        // NO usar prefab.GetComponentInChildren<Renderer>().bounds
        // porque esos bounds son en espacio LOCAL del asset, sin
        // la escala/rotación del prefab aplicada en mundo.
        var renderer = go.GetComponentInChildren<Renderer>();

        float prefabW, prefabD;
        if (renderer != null)
        {
            // bounds en espacio mundo con escala actual (1,1,1)
            prefabW = renderer.bounds.size.x;
            prefabD = renderer.bounds.size.z;
        }
        else
        {
            prefabW = wallPrefabWidth;
            prefabD = wallPrefabWidth;
        }

        // Evitar división por cero
        if (prefabW < 0.001f) prefabW = wallPrefabWidth;
        if (prefabD < 0.001f) prefabD = wallPrefabWidth;

        // ── PASO 3: Escalar para cubrir el rect de la sala ───────
        float targetW = room.Bounds.width  * grid.CellSize;
        float targetD = room.Bounds.height * grid.CellSize;

        go.transform.localScale = new Vector3(
            targetW / prefabW,
            1f,
            targetD / prefabD);

        // ── PASO 4: Leer el centro geométrico REAL tras escalar ──
        // Después de aplicar la escala, renderer.bounds.center da
        // la posición en mundo del centro geométrico del mesh,
        // que puede diferir de go.transform.position si el pivot
        // no está en el centro del modelo.
        Vector3 targetCenter = grid.RectCenter(room.Bounds);
        targetCenter.y = 0f;

        if (renderer != null)
        {
            // Offset entre pivot y centro geométrico real (en escala actual)
            Vector3 rendererCenter = renderer.bounds.center;
            rendererCenter.y = 0f;

            // pivot está en (0,0,0), así que el offset es simplemente
            // la posición del centro del renderer en espacio mundo
            Vector3 pivotToRendererCenter = rendererCenter - go.transform.position;
            pivotToRendererCenter.y = 0f;

            // Colocar el pivot de modo que el renderer quede en targetCenter
            go.transform.position = targetCenter - pivotToRendererCenter;
        }
        else
        {
            // Sin renderer: confiar en el pivot (comportamiento anterior)
            go.transform.position = targetCenter;
        }

        // ── PASO 5: Reparentar conservando posición mundo ────────
        go.transform.SetParent(floorParent, worldPositionStays: true);

        // ── Gizmo ────────────────────────────────────────────────
        gizmos.Add(new GizmoItem {
            Pos  = targetCenter + Vector3.up * 0.05f,
            Col  = new Color(0.2f, 1f, 0.2f, 0.5f),
            Size = new Vector3(targetW, 0.1f, targetD)
        });

        room.FloorObject = go;
    }

    // ─────────────────────────────────────────────────────────────
    // DIAGNÓSTICO DE FLOOR
    // Llamado automáticamente si diagFloors == true.
    // También disponible como ContextMenu para llamar manualmente.
    // ─────────────────────────────────────────────────────────────

    private void DiagnoseFloor(RoomData room, DungeonGrid grid)
    {
        if (room.FloorObject == null)
        {
            Debug.LogWarning($"[ARCH DIAG] Room_{room.Id} — FloorObject es null");
            return;
        }

        // Centro según Grid (fuente de verdad)
        Vector3 gridCenter = grid.RectCenter(room.Bounds);
        gridCenter.y = 0f;

        // Centro visual real del mesh (renderer bounds, no pivot)
        var renderer = room.FloorObject.GetComponentInChildren<Renderer>();
        Vector3 visualCenter = renderer != null
            ? new Vector3(renderer.bounds.center.x, 0f, renderer.bounds.center.z)
            : new Vector3(room.FloorObject.transform.position.x, 0f,
                          room.FloorObject.transform.position.z);

        // Offset XZ entre los dos sistemas
        float offsetXZ = Vector2.Distance(
            new Vector2(gridCenter.x,  gridCenter.z),
            new Vector2(visualCenter.x, visualCenter.z));

        // Componentes individuales
        float offsetX = visualCenter.x - gridCenter.x;
        float offsetZ = visualCenter.z - gridCenter.z;

        // Colores:
        //   verde  → alineado (offset < 0.05u)
        //   naranja → desfase moderado (0.05–1u)
        //   rojo   → desfase significativo (> 1u)
        Color lineColor = offsetXZ < 0.05f ? Color.green
                        : offsetXZ < 1f    ? new Color(1f, 0.5f, 0f)
                        :                    Color.red;

        // Línea en SceneView entre centro de grid y centro visual del floor
        Debug.DrawLine(
            gridCenter   + Vector3.up * 0.3f,
            visualCenter + Vector3.up * 0.3f,
            lineColor,
            diagDuration);

        // Cruz verde en el centro de grid (fuente de verdad)
        Debug.DrawLine(gridCenter + Vector3.left    * 0.4f + Vector3.up * 0.3f,
                       gridCenter + Vector3.right   * 0.4f + Vector3.up * 0.3f,
                       Color.green, diagDuration);
        Debug.DrawLine(gridCenter + Vector3.back    * 0.4f + Vector3.up * 0.3f,
                       gridCenter + Vector3.forward * 0.4f + Vector3.up * 0.3f,
                       Color.green, diagDuration);

        // Cruz cyan en el centro visual del floor (donde está realmente)
        Debug.DrawLine(visualCenter + Vector3.left    * 0.4f + Vector3.up * 0.3f,
                       visualCenter + Vector3.right   * 0.4f + Vector3.up * 0.3f,
                       Color.cyan, diagDuration);
        Debug.DrawLine(visualCenter + Vector3.back    * 0.4f + Vector3.up * 0.3f,
                       visualCenter + Vector3.forward * 0.4f + Vector3.up * 0.3f,
                       Color.cyan, diagDuration);

        string status = offsetXZ < 0.05f ? "✅ ALINEADO"
                      : offsetXZ < 1f    ? "⚠️  DESFASE MODERADO"
                      :                    "❌ DESFASE SIGNIFICATIVO";

        Debug.Log($"[ARCH DIAG] Room_{room.Id} [{room.RoomType}] {status}\n" +
                  $"  Grid Center    (verde): ({gridCenter.x:F3}, {gridCenter.z:F3})\n" +
                  $"  Floor Visual   (cyan):  ({visualCenter.x:F3}, {visualCenter.z:F3})\n" +
                  $"  Offset X: {offsetX:F4}u  |  Offset Z: {offsetZ:F4}u  |  Offset XZ: {offsetXZ:F4}u\n" +
                  $"  Bounds: {room.Bounds.width}×{room.Bounds.height} celdas @ " +
                  $"({room.Bounds.x},{room.Bounds.y}) | CellSize={grid.CellSize}");
    }

    /// <summary>
    /// Ejecuta el diagnóstico manualmente desde el Inspector.
    /// Útil para verificar el estado sin regenerar el dungeon.
    /// </summary>
    [ContextMenu("Diagnosticar Floors")]
    public void DiagnoseAllFloors()
    {
        var gen = GetComponent<DungeonGenerator>();
        if (gen == null)
        {
            Debug.LogWarning("[ARCH] No hay DungeonGenerator en este GameObject"); return;
        }

        var rooms = gen.GetRooms();
        var grid  = gen.GetGrid();
        if (rooms == null || grid == null)
        {
            Debug.LogWarning("[ARCH] No hay dungeon generado"); return;
        }

        Debug.Log($"[ARCH DIAG] ══ Diagnóstico de {rooms.Count} floors ══");
        foreach (var room in rooms)
            DiagnoseFloor(room, grid);
    }

    // ─────────────────────────────────────────────────────────────
    // PAREDES DE SALA — via SnapPoints (sin cambios)
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

        // Norte
        for (int x = room.Bounds.x; x < room.Bounds.x + room.Bounds.width; x++)
        {
            var outside = new Vector2Int(x, room.Bounds.y + room.Bounds.height);
            bool corridor = grid.GetCell(outside) == CellType.Corridor;
            var pos = new Vector3(x * cs + cs * 0.5f, wallY, worldMaxZ);
            PlaceWallSnap(pos, Quaternion.Euler(0f, 180f, 0f),
                          corridor && isBoss, corridor, ref walls, ref doors);
        }

        // Sur
        for (int x = room.Bounds.x; x < room.Bounds.x + room.Bounds.width; x++)
        {
            var outside = new Vector2Int(x, room.Bounds.y - 1);
            bool corridor = grid.GetCell(outside) == CellType.Corridor;
            var pos = new Vector3(x * cs + cs * 0.5f, wallY, worldMinZ);
            PlaceWallSnap(pos, Quaternion.Euler(0f, 0f, 0f),
                          corridor && isBoss, corridor, ref walls, ref doors);
        }

        // Este
        for (int y = room.Bounds.y; y < room.Bounds.y + room.Bounds.height; y++)
        {
            var outside = new Vector2Int(room.Bounds.x + room.Bounds.width, y);
            bool corridor = grid.GetCell(outside) == CellType.Corridor;
            var pos = new Vector3(worldMaxX, wallY, y * cs + cs * 0.5f);
            PlaceWallSnap(pos, Quaternion.Euler(0f, 270f, 0f),
                          corridor && isBoss, corridor, ref walls, ref doors);
        }

        // Oeste
        for (int y = room.Bounds.y; y < room.Bounds.y + room.Bounds.height; y++)
        {
            var outside = new Vector2Int(room.Bounds.x - 1, y);
            bool corridor = grid.GetCell(outside) == CellType.Corridor;
            var pos = new Vector3(worldMinX, wallY, y * cs + cs * 0.5f);
            PlaceWallSnap(pos, Quaternion.Euler(0f, 90f, 0f),
                          corridor && isBoss, corridor, ref walls, ref doors);
        }
    }

    private void PlaceWallSnap(Vector3 targetPos, Quaternion targetRot,
                                bool forceDoor, bool isCorridor,
                                ref int walls, ref int doors)
    {
        if (isCorridor && !forceDoor) return;

        GameObject prefab = forceDoor
            ? Biome.GetDoorPrefab(true)
            : Biome.wallStraight;

        if (prefab == null) return;

        var go = Instantiate(prefab, targetPos, targetRot,
                             forceDoor ? doorParent : wallParent);
        go.transform.localScale = Vector3.one;

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
    // SUELO Y PAREDES DE PASILLO (sin cambios)
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

        bool goesNS = (hasN || hasS) && !(hasE || hasW);
        Quaternion rot = goesNS ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;

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

    private void PlaceOneSideWall(Vector2Int cell, int cs, Vector3 pos, Quaternion rot)
    {
        var go = Instantiate(Biome.wallStraight, pos, rot, wallParent);
        go.transform.localScale = Vector3.one;

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
    // BOSS ROOM / CONTENEDORES / LOG (sin cambios)
    // ─────────────────────────────────────────────────────────────

    private RoomData FindBossRoom(List<RoomData> rooms)
    {
        if (rooms == null || rooms.Count == 0) return null;
        var last = rooms[^1];
        return last.RoomType == RoomType.Boss ? last : null;
    }

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
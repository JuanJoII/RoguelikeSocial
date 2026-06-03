using UnityEngine;
using System.Collections.Generic;

public class PropDecorator : MonoBehaviour
{
    [Header("Cantidad de props por sala")]
    [SerializeField] private int minPropsPerRoom = 2;
    [SerializeField] private int maxPropsPerRoom = 6;

    [Header("Separación mínima entre props (unidades Unity)")]
    [SerializeField] private float minSeparation = 0f;

    [Header("Debug Visual")]
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private Color gizmoColorTarget  = new Color(0f,   1f,   0f,   0.6f);
    [SerializeField] private Color gizmoColorPlaced  = new Color(0f,   0.5f, 1f,   0.5f);
    [SerializeField] private Color gizmoColorBlocked = new Color(1f,   0f,   0f,   0.4f);
    [SerializeField] private Color gizmoColorGrid    = new Color(1f,   1f,   0f,   0.2f); // grid space
    [SerializeField] private Color gizmoColorDiag    = new Color(1f,   0.5f, 0f,   0.8f); // diagnóstico

    // ── Contenedor raíz — un hijo por sala ──────────────────────
    private Transform propsRoot;

    // ── Estado POR SALA (se reconstruye en cada DecorateRoom) ────
    // occupied es GLOBAL a toda la sesión de generación — NO se limpia entre salas
    // para evitar que props de salas adyacentes se solapen en bordes.
    private readonly List<(Vector3 pos, Vector3 size)> occupied     = new();
    private readonly List<Vector3>                      furniturePos = new();

    // ── Gizmos ───────────────────────────────────────────────────
    private struct GizmoEntry { public Vector3 pos; public Vector3 size; public Color col; }
    private readonly List<GizmoEntry> gizmoEntries = new();

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;
        foreach (var g in gizmoEntries)
        {
            Gizmos.color = g.col;
            Gizmos.DrawWireCube(g.pos + Vector3.up * g.size.y * 0.5f, g.size);
        }
    }

    // ════════════════════════════════════════════════════════════
    // API PÚBLICA
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Decora una sala. Puede llamarse en loop para varias salas;
    /// la lista <c>occupied</c> persiste entre llamadas para evitar
    /// solapamiento entre props de salas distintas.
    /// Llama a <see cref="Clear"/> antes de regenerar el dungeon completo.
    /// </summary>
    public void DecorateRoom(RoomData room, PropCollection collection,
                              DungeonGrid grid, System.Random rng)
    {
        if (collection == null || grid == null || room == null) return;

        EnsureRoot();

        // Cada sala tiene su propio contenedor hijo
        var roomParent = new GameObject($"_Props_Room{room.Id}");
        roomParent.transform.SetParent(propsRoot);
        roomParent.transform.localPosition = Vector3.zero;

        // furniturePos se reinicia por sala (los muebles son locales a cada habitación)
        furniturePos.Clear();
        // gizmoEntries NO se limpia aquí para acumular diagnóstico de todas las salas

        var eligible = collection.GetForRoom(room.RoomType);
        if (eligible.Count == 0) return;

        // BUG 1 FIX: guardar el CellSize real del grid, no hardcodearlo
        int cellSize = grid.CellSize;

        var grids = BuildRoomGrids(room, grid);
        if (grids.Inner.Count == 0 && grids.Wall.Count == 0) return;

        int count = rng.Next(minPropsPerRoom, maxPropsPerRoom + 1);

        TryPlaceGroup(eligible, PropPlacementRule.Corner,
                      grids.Corner, rng, GetBudget(PropPlacementRule.Corner,      count, rng),
                      cellSize, roomParent.transform);
        TryPlaceGroup(eligible, PropPlacementRule.AgainstWall,
                      grids.Wall,   rng, GetBudget(PropPlacementRule.AgainstWall, count, rng),
                      cellSize, roomParent.transform);
        TryPlaceGroup(eligible, PropPlacementRule.FloorFree,
                      grids.Inner,  rng, GetBudget(PropPlacementRule.FloorFree,   count, rng),
                      cellSize, roomParent.transform);
        TryPlaceGroup(eligible, PropPlacementRule.FloorRare,
                      grids.Inner,  rng, GetBudget(PropPlacementRule.FloorRare,   count, rng),
                      cellSize, roomParent.transform);
        TryPlaceOnFurniture(eligible, rng,
                            GetBudget(PropPlacementRule.OnFurniture, count, rng),
                            roomParent.transform);
    }

    /// <summary>
    /// Limpia todos los props y resetea el estado global de ocupación.
    /// Llamar antes de regenerar el dungeon.
    /// </summary>
    public void Clear()
    {
        if (propsRoot != null)
        {
            var children = new List<Transform>();
            foreach (Transform t in propsRoot) children.Add(t);
            foreach (var t in children)
            {
#if UNITY_EDITOR
                DestroyImmediate(t.gameObject);
#else
                Destroy(t.gameObject);
#endif
            }
        }

        // BUG 2 FIX: limpiar occupied SOLO aquí (en Clear total), no en DecorateRoom
        occupied.Clear();
        furniturePos.Clear();
        gizmoEntries.Clear();
    }

    // ════════════════════════════════════════════════════════════
    // GRIDS PRECALCULADOS
    // ════════════════════════════════════════════════════════════

    private struct RoomGrids
    {
        public List<Vector3> Inner;
        public List<Vector3> Wall;
        public List<Vector3> Corner;
    }

    private RoomGrids BuildRoomGrids(RoomData room, DungeonGrid grid)
    {
        var grids = new RoomGrids
        {
            Inner  = new List<Vector3>(),
            Wall   = new List<Vector3>(),
            Corner = new List<Vector3>()
        };

        int cs   = grid.CellSize;
        int xMin = room.Bounds.x;
        int xMax = room.Bounds.x + room.Bounds.width  - 1;
        int zMin = room.Bounds.y;
        int zMax = room.Bounds.y + room.Bounds.height - 1;

        for (int x = xMin; x <= xMax; x++)
        for (int z = zMin; z <= zMax; z++)
        {
            if (grid.GetCell(new Vector2Int(x, z)) != CellType.Room) continue;

            // Misma fórmula que RoomArchitectureGenerator y DungeonGrid.CellCenter()
            Vector3 worldPos = new Vector3(
                x * cs + cs * 0.5f,
                0f,
                z * cs + cs * 0.5f);

            bool isXEdge  = x == xMin || x == xMax;
            bool isZEdge  = z == zMin || z == zMax;
            bool isCorner = isXEdge && isZEdge;
            bool isEdge   = isXEdge || isZEdge;

            if      (isCorner) grids.Corner.Add(worldPos);
            else if (isEdge)   grids.Wall.Add(worldPos);
            else               grids.Inner.Add(worldPos);

            // Gizmo: visualizar el grid space de esta sala (amarillo translúcido)
            if (showDebugGizmos)
                gizmoEntries.Add(new GizmoEntry
                {
                    pos  = worldPos,
                    size = new Vector3(cs * 0.85f, 0.05f, cs * 0.85f),
                    col  = gizmoColorGrid
                });
        }

        Debug.Log($"[PROPGRID] Room_{room.Id} [{room.RoomType}] " +
                  $"inner={grids.Inner.Count} wall={grids.Wall.Count} corner={grids.Corner.Count} " +
                  $"cellSize={cs}");
        return grids;
    }

    // ════════════════════════════════════════════════════════════
    // COLOCACIÓN POR GRUPO
    // ════════════════════════════════════════════════════════════

    private void TryPlaceGroup(List<PropData> eligible, PropPlacementRule rule,
                                List<Vector3> candidates, System.Random rng, int budget,
                                int cellSize, Transform parent)
    {
        var props = eligible.FindAll(p => p.PlacementRule == rule);
        if (props.Count == 0 || candidates.Count == 0) return;

        var shuffled = new List<Vector3>(candidates);
        Shuffle(shuffled, rng);

        int placed  = 0;
        int cellIdx = 0;

        while (placed < budget && cellIdx < shuffled.Count)
        {
            var prop = WeightedPick(props, rng);
            if (prop == null) break;

            Vector3 rawPos = shuffled[cellIdx++];
            Vector3 candidatePos = AddCellJitter(rawPos, cellSize, rng, rule);

            if (showDebugGizmos)
                gizmoEntries.Add(new GizmoEntry
                {
                    pos  = candidatePos,
                    size = new Vector3(cellSize * 0.9f, 0.1f, cellSize * 0.9f),
                    col  = gizmoColorTarget
                });

            if (rng.NextDouble() > prop.SpawnChance)
            {
                // Rechazado por probabilidad — gizmo rojo pequeño
                if (showDebugGizmos)
                    gizmoEntries[gizmoEntries.Count - 1] = new GizmoEntry
                    {
                        pos  = candidatePos,
                        size = new Vector3(0.3f, 0.3f, 0.3f),
                        col  = gizmoColorBlocked
                    };
                continue;
            }

            // BUG 3 FIX: medir el tamaño con la rotación que se va a usar,
            // no con Quaternion.identity
            Quaternion intendedRot = Quaternion.Euler(0f, rng.Next(0, 4) * 90f, 0f);
            Vector3 propSize = GetPrefabSizeSafe(prop.Prefab, intendedRot);

            if (!IsAreaFree(candidatePos, propSize))
            {
                if (showDebugGizmos)
                    gizmoEntries[gizmoEntries.Count - 1] = new GizmoEntry
                    {
                        pos  = candidatePos,
                        size = propSize,
                        col  = gizmoColorBlocked
                    };
                continue;
            }

            SpawnProp(prop, candidatePos, propSize, intendedRot, parent, rule, rng);
            placed++;
        }
    }

    private void TryPlaceOnFurniture(List<PropData> eligible,
                                  System.Random rng, int budget,
                                  Transform parent)
    {
        if (furniturePos.Count == 0) return;
        var props = eligible.FindAll(p => p.PlacementRule == PropPlacementRule.OnFurniture);
        if (props.Count == 0) return;

        for (int i = 0; i < budget; i++)
        {
            var prop = WeightedPick(props, rng);
            if (prop == null || rng.NextDouble() > prop.SpawnChance) continue;

            Quaternion rot     = Quaternion.Euler(0f, rng.Next(0, 360), 0f);
            Vector3 basePos    = furniturePos[rng.Next(0, furniturePos.Count)]
                                + Vector3.up * 0.6f;
            Vector3 propSize   = GetPrefabSizeSafe(prop.Prefab, rot);

            SpawnProp(prop, basePos, propSize, rot, parent,
                    PropPlacementRule.OnFurniture, rng);
        }
    }

    // ════════════════════════════════════════════════════════════
    // INSTANCIACIÓN
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// SpawnProp ya no genera su propia rotación — la recibe de afuera
    /// para que el tamaño medido y la rotación aplicada sean siempre consistentes.
    /// </summary>
    private void SpawnProp(PropData prop, Vector3 targetWorldPos,
                            Vector3 propSize, Quaternion rot, Transform parent, PropPlacementRule rule, System.Random rng)
    {

        // PASO 1: Instanciar en (0,0,0) con la rotación final para leer bounds correctos
        var go = Instantiate(prop.Prefab, Vector3.zero, rot);
        go.name = $"Prop_{prop.PropName}";

        // PASO 2: Calcular posición corregida (base del mesh sobre Y=0)
        Vector3 correctedPos = GetCorrectedBasePosition(targetWorldPos, go, floorY: 0f);

        // PASO 3: Mover en espacio mundo
        go.transform.position = correctedPos;

        // PASO 4: Reparentar conservando posición mundo
        go.transform.SetParent(parent, worldPositionStays: true);

        // Registrar ocupación
        occupied.Add((correctedPos, propSize));

        if (showDebugGizmos)
            gizmoEntries.Add(new GizmoEntry
            {
                pos  = correctedPos,
                size = propSize,
                col  = gizmoColorPlaced
            });

        Debug.Log($"[SPAWN] {prop.PropName} | target={targetWorldPos} | " +
                  $"corrected={correctedPos} | size={propSize} | rot={rot.eulerAngles}");

        if (prop.PropName.ToLower().Contains("table") ||
            prop.PropName.ToLower().Contains("shelf"))
            furniturePos.Add(correctedPos);

        Debug.Log(
        $"[PROP WORLD CHECK] {prop.PropName}\n" +
        $"  go.transform.position (mundo): {go.transform.position}\n" +
        $"  go.transform.parent.position:  {go.transform.parent?.position}\n" +
        $"  targetWorldPos original:       {targetWorldPos}"
    );
    }

    // ════════════════════════════════════════════════════════════
    // CORRECCIÓN DE POSICIÓN BASE
    // ════════════════════════════════════════════════════════════

    private Vector3 GetCorrectedBasePosition(Vector3 worldPos, GameObject instance,
                                              float floorY = 0f)
    {
        var renderer = instance.GetComponentInChildren<Renderer>();
        if (renderer == null)
            return new Vector3(worldPos.x, floorY, worldPos.z);

        float pivotToBase = instance.transform.position.y - renderer.bounds.min.y;
        float correctedY  = floorY + pivotToBase;
        return new Vector3(worldPos.x, correctedY, worldPos.z);
    }

    // ════════════════════════════════════════════════════════════
    // TAMAÑO DE PREFAB — con rotación aplicada
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// BUG 3 FIX: Instancia el prefab con la rotación real que se va a usar,
    /// no con Quaternion.identity. Así el OABB medido coincide con el objeto real.
    /// </summary>
    private Vector3 GetPrefabSizeSafe(GameObject prefab, Quaternion rotation)
    {
        if (prefab == null) return Vector3.one;

        var temp      = Instantiate(prefab, Vector3.zero, rotation);
        var renderers = temp.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
        {
            DestroyImmediate(temp);
            return Vector3.one;
        }

        Bounds combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            combined.Encapsulate(renderers[i].bounds);

        Vector3 size = combined.size;
        DestroyImmediate(temp);
        return size;
    }

    // ════════════════════════════════════════════════════════════
    // VALIDACIÓN DE ESPACIO
    // ════════════════════════════════════════════════════════════

    private bool IsAreaFree(Vector3 position, Vector3 propSize)
    {
        float halfX = propSize.x * 0.5f + 0.1f;
        float halfZ = propSize.z * 0.5f + 0.1f;

        foreach (var (pos, size) in occupied)
        {
            float minDistX = halfX + size.x * 0.5f + 0.1f;
            float minDistZ = halfZ + size.z * 0.5f + 0.1f;

            if (Mathf.Abs(position.x - pos.x) < minDistX &&
                Mathf.Abs(position.z - pos.z) < minDistZ)
                return false;
        }
        return true;
    }

    // ════════════════════════════════════════════════════════════
    // DIAGNÓSTICO — llamar desde el Inspector o desde DungeonGenerator
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Dibuja en la SceneView el rectángulo de room.Bounds en espacio mundo
    /// para verificar que coincide con la geometría generada por RoomArchitectureGenerator.
    /// Si el borde magenta no coincide con las paredes visuales → hay desfase de origen.
    /// </summary>
    public void DebugDrawRoomBounds(RoomData room, DungeonGrid grid, float duration = 30f)
    {
        int cs = grid.CellSize;

        // Esquinas en espacio mundo (misma fórmula que RoomArchitectureGenerator)
        Vector3 bl = new Vector3(room.Bounds.x * cs,                        0.3f, room.Bounds.y * cs);
        Vector3 br = new Vector3((room.Bounds.x + room.Bounds.width) * cs,  0.3f, room.Bounds.y * cs);
        Vector3 tl = new Vector3(room.Bounds.x * cs,                        0.3f, (room.Bounds.y + room.Bounds.height) * cs);
        Vector3 tr = new Vector3((room.Bounds.x + room.Bounds.width) * cs,  0.3f, (room.Bounds.y + room.Bounds.height) * cs);

        Debug.DrawLine(bl, br, Color.magenta, duration);
        Debug.DrawLine(br, tr, Color.magenta, duration);
        Debug.DrawLine(tr, tl, Color.magenta, duration);
        Debug.DrawLine(tl, bl, Color.magenta, duration);

        // Centro calculado
        Vector3 gridCenter = new Vector3(
            room.Bounds.x * cs + room.Bounds.width  * cs * 0.5f,
            0.3f,
            room.Bounds.y * cs + room.Bounds.height * cs * 0.5f);

        Debug.DrawLine(gridCenter + Vector3.left    * 0.6f, gridCenter + Vector3.right   * 0.6f, Color.yellow, duration);
        Debug.DrawLine(gridCenter + Vector3.back    * 0.6f, gridCenter + Vector3.forward * 0.6f, Color.yellow, duration);

        // Si la sala tiene FloorObject, comparar con su posición visual real
        if (room.FloorObject != null)
        {
            Vector3 visualCenter = room.FloorObject.transform.position;
            visualCenter.y = 0.3f;

            Debug.DrawLine(visualCenter + Vector3.left    * 0.6f, visualCenter + Vector3.right   * 0.6f, Color.cyan, duration);
            Debug.DrawLine(visualCenter + Vector3.back    * 0.6f, visualCenter + Vector3.forward * 0.6f, Color.cyan, duration);

            // Línea naranja conectando ambos centros — debe ser longitud 0 si no hay desfase
            Debug.DrawLine(gridCenter, visualCenter, gizmoColorDiag, duration);

            float offset = Vector3.Distance(
                new Vector3(gridCenter.x, 0, gridCenter.z),
                new Vector3(visualCenter.x, 0, visualCenter.z));

            Debug.Log($"[BOUNDS DEBUG] Room_{room.Id}\n" +
                      $"  Grid center  (amarillo): {gridCenter}\n" +
                      $"  Visual center  (cyan):   {visualCenter}\n" +
                      $"  Offset XZ: {offset:F4} unidades  ← debe ser ~0\n" +
                      $"  Bounds celdas: x={room.Bounds.x} y={room.Bounds.y} " +
                      $"w={room.Bounds.width} h={room.Bounds.height} | CellSize={cs}");
        }
        else
        {
            Debug.Log($"[BOUNDS DEBUG] Room_{room.Id} — FloorObject es null, " +
                      $"no se puede comparar centro visual. " +
                      $"Grid center: {gridCenter}");
        }
    }

    /// <summary>
    /// Llama DebugDrawRoomBounds para todas las salas. Útil como botón de Context Menu.
    /// </summary>
    [ContextMenu("Debug Draw All Room Bounds")]
    public void DebugDrawAllRooms()
    {
        var gen = GetComponent<DungeonGenerator>();
        if (gen == null) { Debug.LogWarning("[PROPDECORATOR] No hay DungeonGenerator en este GameObject"); return; }

        var rooms = gen.GetRooms();
        var grid  = gen.GetGrid();
        if (rooms == null || grid == null) { Debug.LogWarning("[PROPDECORATOR] Sin dungeon generado"); return; }

        foreach (var room in rooms)
            DebugDrawRoomBounds(room, grid, 20f);

        Debug.Log($"[PROPDECORATOR] Dibujados bounds para {rooms.Count} salas (duración 20s)");
    }

    // ════════════════════════════════════════════════════════════
    // HELPERS
    // ════════════════════════════════════════════════════════════

    private int GetBudget(PropPlacementRule rule, int total, System.Random rng) => rule switch
    {
        PropPlacementRule.Corner      => rng.Next(2, 5),
        PropPlacementRule.AgainstWall => rng.Next(2, Mathf.Max(4, total / 2)),
        PropPlacementRule.FloorFree   => rng.Next(2, Mathf.Max(4, total)),
        PropPlacementRule.FloorRare   => rng.Next(1, 3),
        PropPlacementRule.OnFurniture => rng.Next(1, Mathf.Max(3, total / 2)),
        _ => 2
    };

    private PropData WeightedPick(List<PropData> pool, System.Random rng)
    {
        float total = 0f;
        foreach (var p in pool) total += p.Weight;
        if (total <= 0f) return pool.Count > 0 ? pool[0] : null;

        float roll = (float)rng.NextDouble() * total;
        float cum  = 0f;
        foreach (var p in pool)
        {
            cum += p.Weight;
            if (roll <= cum) return p;
        }
        return pool[pool.Count - 1];
    }

    private void Shuffle(List<Vector3> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private void EnsureRoot()
    {
        if (propsRoot != null) return;
        var go = new GameObject("_Props");
        go.transform.SetParent(transform);
        go.transform.localPosition = Vector3.zero;
        propsRoot = go.transform;
    }

    private Vector3 AddCellJitter(Vector3 cellCenter, int cellSize, 
                               System.Random rng, PropPlacementRule rule)
    {
        float range = rule switch
        {
            PropPlacementRule.AgainstWall => cellSize * 0.15f, // poco jitter
            PropPlacementRule.Corner      => cellSize * 0.1f,  // casi nada
            _                             => cellSize * 0.3f   // libre
        };

        return new Vector3(
            cellCenter.x + (float)(rng.NextDouble() * 2 - 1) * range,
            cellCenter.y,
            cellCenter.z + (float)(rng.NextDouble() * 2 - 1) * range);
    }
}
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Puente entre el sistema procedural y el sistema de gameplay.
/// 
/// RESPONSABILIDAD:
/// Después de que DungeonGenerator termina, este script:
/// 1. Spawnea al jugador en la sala Start
/// 2. Coloca RoomContext en cada sala
/// 3. Coloca SpawnPoints de oleadas en cada sala
/// 4. Coloca triggers de entrada entre salas
/// 5. Spawnea el boss en la última sala
/// 6. Configura los bounds de cámara por sala
///
/// CUÁNDO LLAMARLO:
/// Llama a Integrate() justo después de dungeonGenerator.Generate().
/// Generate() es síncrono — cuando retorna el dungeon ya existe.
///
/// COORDINADAS:
/// RoomData.Bounds está en coordenadas de celda.
/// Multiplicamos por config.cellSize para obtener posición en mundo.
/// </summary>
public class DungeonGameplayIntegrator : MonoBehaviour
{
    [Header("Referencia al generador")]
    [SerializeField] private DungeonGenerator dungeonGenerator;

    [Header("Jugador")]
    [SerializeField] private GameObject playerPrefab;
    [Tooltip("Si el jugador ya está en escena, asígnalo aquí " +
             "y playerPrefab se ignora.")]
    [SerializeField] private GameObject existingPlayer;

    [Header("Configuración de salas")]
    [Tooltip("Un array de RoomConfigSO por cada sala. " +
             "El índice 0 es la sala más fácil, el último es la sala de boss. " +
             "Si hay más salas que configs, la última config se reutiliza.")]
    [SerializeField] private RoomConfigSO[] roomConfigs;

    [Header("Spawn Points por sala")]
    [Tooltip("Cuántos spawn points colocar por sala. " +
             "Se distribuyen en las esquinas del área de la sala.")]
    [SerializeField] private int spawnPointsPerRoom = 4;

    [Header("Triggers de entrada")]
    [SerializeField] private float triggerThickness = 0.5f;
    [SerializeField] private float triggerHeight = 2f;

    [Header("Prefabs de gameplay")]
    [SerializeField] private GameObject roomEntrancePrefab;
    [SerializeField] private GameObject spawnPointMarkerPrefab; // GameObject vacío opcional
    
    [Header("Paredes entre salas")]
    [Tooltip("Alto de la pared en unidades de mundo.")]
    [SerializeField] private float wallHeight = 3f;

    [Tooltip("Material de la pared. Debe tener shader con soporte de alpha " +
             "para que el fade funcione. En URP: Lit con Surface Type Transparent.")]
    [SerializeField] private Material wallMaterial;
    
    // Guardamos las paredes por sala origen para abrirlas al completar
// Key: roomIndex de la sala que debe completarse para abrir la pared
    private Dictionary<int, List<RoomWall>> _roomWalls 
        = new Dictionary<int, List<RoomWall>>();

    // Referencias generadas — accesibles para debug
    private GameObject _playerInstance;
    private List<RoomContext> _roomContexts = new List<RoomContext>();
    private List<GameObject> _generatedObjects = new List<GameObject>();

    // ════════════════════════════════════════════════════════════════════
    // API PÚBLICA
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Llamar después de dungeonGenerator.Generate().
    /// Integra el dungeon generado con todos los sistemas de gameplay.
    /// </summary>
    public void Integrate()
    {
        List<RoomData> rooms = dungeonGenerator.GetRooms();

        if (rooms == null || rooms.Count == 0)
        {
            Debug.LogError("[DungeonGameplayIntegrator] No hay salas generadas. " +
                           "Llama Generate() antes de Integrate().");
            return;
        }

        // Limpiamos objetos de gameplay de la generación anterior
        // para que regenerar el dungeon funcione limpiamente
        ClearGeneratedObjects();

        float cellSize = dungeonGenerator.Config.cellSize;

        SpawnPlayer(rooms, cellSize);
        SetupRooms(rooms, cellSize);
        SetupEntranceTriggers(rooms, cellSize);

        // Suscribimos el evento de sala completa para abrir paredes
        RoomManager.OnRoomComplete += HandleRoomComplete;
        
        Debug.Log($"[DungeonGameplayIntegrator] Integración completa. " +
                  $"{rooms.Count} salas configuradas.");
    }
    private void HandleRoomComplete(int score)
    {
        // RoomManager sabe qué sala acaba de completarse
        int completedRoomId = RunManager.Instance.CurrentRoomIndex;

        if (_roomWalls.TryGetValue(completedRoomId, out List<RoomWall> walls))
        {
            foreach (RoomWall wall in walls)
            {
                if (wall != null)
                    wall.Open();
            }
        }
    }

    private void OnDestroy()
    {
        RoomManager.OnRoomComplete -= HandleRoomComplete;
    }

    // ════════════════════════════════════════════════════════════════════
    // JUGADOR
    // ════════════════════════════════════════════════════════════════════

    private void SpawnPlayer(List<RoomData> rooms, float cellSize)
    {
        // Buscamos la sala de tipo Start
        RoomData startRoom = rooms.Find(r => r.RoomType == RoomType.Start);

        if (startRoom == null)
        {
            Debug.LogWarning("[DungeonGameplayIntegrator] No hay sala de tipo Start. " +
                             "Usando la primera sala como fallback.");
            startRoom = rooms[0];
        }

        Vector3 spawnPos = RoomWorldCenter(startRoom, cellSize);
        spawnPos.y = 1f; // altura sobre el suelo

        if (existingPlayer != null)
        {
            // El jugador ya existe en escena — lo reposicionamos
            existingPlayer.transform.position = spawnPos;
            _playerInstance = existingPlayer;

            // Snap de la cámara para que no viaje visible
            TopDownCamera.Instance?.SnapToTarget();
        }
        else if (playerPrefab != null)
        {
            _playerInstance = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
            _generatedObjects.Add(_playerInstance);

            // Asignamos el target de la cámara al jugador recién instanciado
            TopDownCamera.Instance?.SetTarget(_playerInstance.transform);
            TopDownCamera.Instance?.SnapToTarget();
        }
        else
        {
            Debug.LogError("[DungeonGameplayIntegrator] playerPrefab no asignado " +
                           "y no hay existingPlayer. El jugador no fue spawneado.");
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // SALAS — RoomContext + SpawnPoints + Boss
    // ════════════════════════════════════════════════════════════════════

    private void SetupRooms(List<RoomData> rooms, float cellSize)
    {
        _roomContexts.Clear();

        for (int i = 0; i < rooms.Count; i++)
        {
            RoomData room = rooms[i];
            Vector3 center = RoomWorldCenter(room, cellSize);

            // ── RoomContext ────────────────────────────────────────────
            GameObject contextObj = new GameObject($"RoomContext_{room.Id}");
            contextObj.transform.position = center;
            _generatedObjects.Add(contextObj);

            RoomContext context = contextObj.AddComponent<RoomContext>();
            context.roomIndex = room.Id;

            // Elegimos la config correspondiente al índice
            // Si hay más salas que configs, reutilizamos la última
            int configIndex = Mathf.Min(i, roomConfigs.Length - 1);
            context.possibleConfigs = new RoomConfigSO[] { roomConfigs[configIndex] };

            // Bounds de cámara calculados desde el RoomData
            Vector2 boundsMin, boundsMax;
            CalculateCameraBounds(room, cellSize, out boundsMin, out boundsMax);
            context.boundsMin = boundsMin;
            context.boundsMax = boundsMax;
            context.useCameraBounds = false;


            _roomContexts.Add(context);

            // ── SpawnPoints ───────────────────────────────────────────
            Transform[] spawnPoints = CreateSpawnPoints(room, cellSize, contextObj.transform);

            // ── WaveManager de esta sala ──────────────────────────────
            // El WaveManager necesita conocer los spawn points
            // Lo buscamos en el RoomContext o lo creamos aquí
            WaveManager waveManager = contextObj.AddComponent<WaveManager>();
            waveManager.SetSpawnPoints(spawnPoints);

            // ── Boss en la última sala ────────────────────────────────
            bool isLastRoom = i == rooms.Count - 1;
            if (isLastRoom && roomConfigs[configIndex].isBossRoom)
            {
                // Pasamos la posición del centro de la sala al WaveManager
                // para que sepa dónde spawnear el boss
                waveManager.SetRoomCenter(center);
            }
        }
    }

    private Transform[] CreateSpawnPoints(RoomData room, float cellSize, Transform parent)
    {
        // Distribuimos spawn points en las esquinas y bordes de la sala
        // para que los portales aparezcan en posiciones naturales
        List<Transform> points = new List<Transform>();

        float worldWidth  = room.Bounds.width  * cellSize;
        float worldHeight = room.Bounds.height * cellSize;
        Vector3 roomMin   = new Vector3(room.Bounds.x * cellSize, 0f,
                                        room.Bounds.y * cellSize);

        // Esquinas con un offset interior para no aparecer en paredes
        float margin = cellSize * 1.5f;

        Vector3[] positions = new Vector3[]
        {
            roomMin + new Vector3(margin,         0f, margin),
            roomMin + new Vector3(worldWidth - margin, 0f, margin),
            roomMin + new Vector3(margin,         0f, worldHeight - margin),
            roomMin + new Vector3(worldWidth - margin, 0f, worldHeight - margin),
        };

        // Si se piden más de 4, agregamos puntos en los bordes medios
        if (spawnPointsPerRoom > 4)
        {
            positions = new Vector3[]
            {
                roomMin + new Vector3(margin,               0f, margin),
                roomMin + new Vector3(worldWidth - margin,  0f, margin),
                roomMin + new Vector3(margin,               0f, worldHeight - margin),
                roomMin + new Vector3(worldWidth - margin,  0f, worldHeight - margin),
                roomMin + new Vector3(worldWidth * 0.5f,    0f, margin),
                roomMin + new Vector3(worldWidth * 0.5f,    0f, worldHeight - margin),
                roomMin + new Vector3(margin,               0f, worldHeight * 0.5f),
                roomMin + new Vector3(worldWidth - margin,  0f, worldHeight * 0.5f),
            };
        }

        int count = Mathf.Min(spawnPointsPerRoom, positions.Length);
        for (int i = 0; i < count; i++)
        {
            GameObject sp = spawnPointMarkerPrefab != null
                ? Instantiate(spawnPointMarkerPrefab, positions[i], Quaternion.identity, parent)
                : new GameObject($"SpawnPoint_{i}");

            sp.transform.position = positions[i];
            sp.transform.SetParent(parent);
            points.Add(sp.transform);
        }

        return points.ToArray();
    }

    // ════════════════════════════════════════════════════════════════════
    // TRIGGERS DE ENTRADA
    // ════════════════════════════════════════════════════════════════════

    private void SetupEntranceTriggers(List<RoomData> rooms, float cellSize)
    {
        // Creamos un trigger por cada conexión entre salas
        // Usamos ConnectedRoomIds para saber qué salas conectan
        HashSet<string> processedConnections = new HashSet<string>();

        foreach (RoomData room in rooms)
        {
            foreach (int connectedId in room.ConnectedRoomIds)
            {
                // Evitamos duplicar triggers (A→B y B→A son la misma conexión)
                string key = room.Id < connectedId
                    ? $"{room.Id}_{connectedId}"
                    : $"{connectedId}_{room.Id}";

                if (processedConnections.Contains(key)) continue;
                processedConnections.Add(key);

                RoomData roomA = room;
                RoomData roomB = rooms.Find(r => r.Id == connectedId);
                if (roomB == null) continue;

                CreateTriggerBetweenRooms(roomA, roomB, cellSize);
            }
        }
    }

   private void CreateTriggerBetweenRooms(RoomData roomA, RoomData roomB, float cellSize)
{
    Vector3 centerA = RoomWorldCenter(roomA, cellSize);
    Vector3 centerB = RoomWorldCenter(roomB, cellSize);
    Vector3 midPoint = (centerA + centerB) * 0.5f;
    midPoint.y = triggerHeight * 0.5f;

    // La sala origen es la de menor índice (el jugador viene de allí)
    // La sala destino es la de mayor índice (a donde va el jugador)
    RoomData sourceRoom = roomA.Id < roomB.Id ? roomA : roomB;
    RoomData targetRoom = roomA.Id > roomB.Id ? roomA : roomB;

    RoomContext targetContext = _roomContexts.Find(
        rc => rc.roomIndex == targetRoom.Id);
    if (targetContext == null) return;

    // ── Trigger de entrada ────────────────────────────────────────────
    GameObject triggerObj = new GameObject($"Entrance_{roomA.Id}_to_{roomB.Id}");
    triggerObj.transform.position = midPoint;
    _generatedObjects.Add(triggerObj);

    BoxCollider triggerCol = triggerObj.AddComponent<BoxCollider>();
    Vector3 direction = (centerB - centerA).normalized;
    bool isHorizontal = Mathf.Abs(direction.x) > Mathf.Abs(direction.z);

    float perpendicularSize = cellSize * 4f;
    float depthSize = cellSize * 1.5f;

    triggerCol.isTrigger = true;
    triggerCol.size = isHorizontal
        ? new Vector3(depthSize, triggerHeight, perpendicularSize)
        : new Vector3(perpendicularSize, triggerHeight, depthSize);

    RoomEntrance entrance = triggerObj.AddComponent<RoomEntrance>();
    entrance.SetTargetRoom(targetContext);

    WaveManager waveManager = targetContext.GetComponent<WaveManager>();
    if (waveManager != null)
        entrance.SetWaveManager(waveManager);

    // ── Pared bloqueante ──────────────────────────────────────────────
    // La pared se abre cuando la sala ORIGEN es completada
    RoomWall wall = CreateWall(midPoint, isHorizontal, cellSize);

    if (!_roomWalls.ContainsKey(sourceRoom.Id))
        _roomWalls[sourceRoom.Id] = new List<RoomWall>();

    _roomWalls[sourceRoom.Id].Add(wall);
}

private RoomWall CreateWall(Vector3 position, bool isHorizontal, float cellSize)
{
    GameObject wallObj = new GameObject("RoomWall");
    wallObj.transform.position = position + Vector3.up * (wallHeight * 0.5f);
    _generatedObjects.Add(wallObj);

    // Mesh visible
    GameObject wallMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
    wallMesh.transform.SetParent(wallObj.transform);
    wallMesh.transform.localPosition = Vector3.zero;

    // Dimensiones — cubre todo el ancho del pasillo
    wallMesh.transform.localScale = isHorizontal
        ? new Vector3(0.3f, wallHeight, cellSize * 4f)
        : new Vector3(cellSize * 4f, wallHeight, 0.3f);

    // Collider de bloqueo — en el padre para que RoomWall lo encuentre
    BoxCollider blockCol = wallObj.AddComponent<BoxCollider>();
    blockCol.size = isHorizontal
        ? new Vector3(0.3f, wallHeight, cellSize * 4f)
        : new Vector3(cellSize * 4f, wallHeight, 0.3f);

    if (wallMaterial != null)
        wallMesh.GetComponent<Renderer>().material = wallMaterial;

    // Destruimos el collider del mesh hijo — usamos el del padre
    Destroy(wallMesh.GetComponent<Collider>());

    RoomWall roomWall = wallObj.AddComponent<RoomWall>();
    return roomWall;
}

    // ════════════════════════════════════════════════════════════════════
    // LIMPIEZA
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Limpia todos los objetos de gameplay generados por Integrate().
    /// Llamar antes de regenerar el dungeon.
    /// </summary>
    public void ClearGeneratedObjects()
    {
        foreach (GameObject obj in _generatedObjects)
        {
            if (obj != null) Destroy(obj);
        }
        _generatedObjects.Clear();
        _roomContexts.Clear();
    }

    // ════════════════════════════════════════════════════════════════════
    // HELPERS
    // ════════════════════════════════════════════════════════════════════

    private Vector3 RoomWorldCenter(RoomData room, float cellSize)
    {
        return new Vector3(
            room.CenterCellInt.x * cellSize,
            0f,
            room.CenterCellInt.y * cellSize);
    }

    private void CalculateCameraBounds(RoomData room, float cellSize,
                                       out Vector2 min, out Vector2 max)
    {
        min = new Vector2(room.Bounds.x      * cellSize,
                          room.Bounds.y      * cellSize);
        max = new Vector2((room.Bounds.x + room.Bounds.width)  * cellSize,
                          (room.Bounds.y + room.Bounds.height) * cellSize);
    }
}

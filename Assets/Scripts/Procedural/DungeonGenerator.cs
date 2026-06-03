using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(DungeonLayoutGenerator))]
[RequireComponent(typeof(SmartConnectionGenerator))]
[RequireComponent(typeof(RoomArchitectureGenerator))]
[RequireComponent(typeof(AccessibilityValidator))]
[RequireComponent(typeof(DungeonDebugger))]
public class DungeonGenerator : MonoBehaviour
{
    [Header("Config Base")]
    [SerializeField] private DungeonConfig config;

    [Header("Perfiles de Dificultad")]
    [SerializeField] private DungeonDifficultyConfig easyConfig;
    [SerializeField] private DungeonDifficultyConfig normalConfig;
    [SerializeField] private DungeonDifficultyConfig hardConfig;
    [SerializeField] private DungeonDifficultyConfig nightmareConfig;
    [SerializeField] private bool useFallbackPresets = true;

    [Header("Reintentos de generación")]
    [Tooltip("Máximo de reintentos si el validador rechaza el dungeon")]
    [SerializeField] private int maxGenerationAttempts = 10;

    private DungeonLayoutGenerator    layoutGen;
    private SmartConnectionGenerator  connectionGen;
    private RoomArchitectureGenerator archGen;
    private AccessibilityValidator    validator;
    private DungeonDebugger           debugger;
    private DungeonGraphGenerator     graphGen;
    private DoorSocketResolver        socketResolver;
    private EnemySpawner              enemySpawner;
    private LootGenerator             lootGen;
    private PropDecorator             propDecorator;

    public DungeonConfig Config => config;

    private DungeonGrid    grid;
    private List<RoomData> rooms;
    private bool           lastValid;
    private DifficultyLevel difficulty = DifficultyLevel.Normal;

    // ─────────────────────────────────────────────
    // LIFECYCLE
    // ─────────────────────────────────────────────

    private void Awake()
    {
        layoutGen      = GetComponent<DungeonLayoutGenerator>();
        connectionGen  = GetComponent<SmartConnectionGenerator>();
        archGen        = GetComponent<RoomArchitectureGenerator>();
        validator      = GetComponent<AccessibilityValidator>();
        debugger       = GetComponent<DungeonDebugger>();
        graphGen       = GetComponent<DungeonGraphGenerator>();
        socketResolver = GetComponent<DoorSocketResolver>();
        enemySpawner   = GetComponent<EnemySpawner>();
        lootGen        = GetComponent<LootGenerator>();
        propDecorator  = GetComponent<PropDecorator>();
    }
    
    // ─────────────────────────────────────────────
    // GENERACIÓN — iterativa, sin recursión
    // ─────────────────────────────────────────────

    public void Generate()
    {
        // BUG FIX: el retry era recursivo — Generate() seguía ejecutándose
        // después del retry, spawneando props de rooms_A sobre el dungeon de rooms_B.
        // Solución: loop iterativo que hace Clear() completo en cada intento
        // y sale en cuanto el validador acepta el resultado.

        int baseSeed = config.seed;

        for (int attempt = 0; attempt < maxGenerationAttempts; attempt++)
        {
            // Cada intento parte de cero — geometría, props y estado
            Clear();

            int currentSeed = baseSeed + attempt;
            config.seed = currentSeed;

            // Inicializar ambos RNG con la misma seed para reproducibilidad
            var rng = new System.Random(currentSeed);
            Random.InitState(currentSeed);

            var diff = GetDiff(difficulty);

            // [1] Grilla
            grid = new DungeonGrid(config.gridWidth, config.gridHeight, config.cellSize);

            // [2] Graph lógico
            DungeonGraph graph = null;
            if (graphGen != null)
                graph = graphGen.Generate(diff, new System.Random(currentSeed));

            // [3] Layout — genera rooms y las registra en grid
            rooms = (graph != null && layoutGen != null)
                ? layoutGen.GenerateRoomsFromGraph(grid, graph, diff, new System.Random(currentSeed))
                : layoutGen.GenerateRooms(grid, diff);

            // [4] Conexiones
            connectionGen.ConnectRooms(rooms, grid, diff);
            socketResolver?.ResolveAllSockets(rooms, grid);

            // [5] Geometría visual
            archGen.BuildGeometry(grid, rooms);

            // [6] Props — SOLO si la geometría ya es la definitiva
            if (propDecorator != null && config.currentBiome?.PropCollection != null)
            {
                foreach (var room in rooms)
                {
                    if (room.RoomType == RoomType.Start) continue;
                    propDecorator.DecorateRoom(room, config.currentBiome.PropCollection,
                                               grid, rng);
                }
            }

            Debug.Log($"[PROPS] PropDecorator={propDecorator != null} | " +
                      $"Collection={config.currentBiome?.PropCollection != null} | " +
                      $"Rooms={rooms?.Count}");

            // [7] Validación — si pasa, terminamos
            lastValid = validator.ValidateAll(rooms, grid);

            if (lastValid)
            {
                if (attempt > 0)
                    Debug.Log($"[DUNGEON] Válido en intento {attempt + 1} (seed={currentSeed})");

                debugger?.SetData(grid, rooms);
                return; // ← salida limpia, rooms y geometría son el mismo conjunto
            }

            Debug.LogWarning($"[DUNGEON] Intento {attempt + 1} inválido (seed={currentSeed}), reintentando...");
        }

        // Si agotamos los intentos, quedamos con el último generado
        Debug.LogError($"[DUNGEON] No se encontró dungeon válido tras {maxGenerationAttempts} intentos. " +
                       $"Usando el último generado.");
        lastValid = false;
        debugger?.SetData(grid, rooms);
    }

    // ─────────────────────────────────────────────
    // LIMPIEZA
    // ─────────────────────────────────────────────

    public void Clear()
    {
        archGen?.ClearGeometry();
        propDecorator?.Clear();
        grid      = null;
        rooms     = null;
        lastValid = false;
        debugger?.SetData(null, null);
    }

    // ─────────────────────────────────────────────
    // DIFICULTAD
    // ─────────────────────────────────────────────

    public DungeonDifficultyConfig GetDiff(DifficultyLevel level)
    {
        var assigned = level switch
        {
            DifficultyLevel.Easy      => easyConfig,
            DifficultyLevel.Normal    => normalConfig,
            DifficultyLevel.Hard      => hardConfig,
            DifficultyLevel.Nightmare => nightmareConfig,
            _                         => normalConfig
        };

        if (assigned != null)   return assigned;
        if (useFallbackPresets) return DungeonDifficultyConfig.CreatePreset(level);

        Debug.LogError($"[DUNGEON] No hay DifficultyConfig para {level}");
        return DungeonDifficultyConfig.CreatePreset(DifficultyLevel.Normal);
    }

    // ─────────────────────────────────────────────
    // ACCESO EXTERNO
    // ─────────────────────────────────────────────

    public List<RoomData>  GetRooms()      => rooms;
    public DungeonGrid     GetGrid()       => grid;
    public DifficultyLevel GetDifficulty() => difficulty;

    public void SetDifficultyAndRegenerate(DifficultyLevel level)
    {
        difficulty = level;
        if (config.useRandomSeed) config.seed = (int)System.DateTime.Now.Ticks;
        Generate();
    }
}
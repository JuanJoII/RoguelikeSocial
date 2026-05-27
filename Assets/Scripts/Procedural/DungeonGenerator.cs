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

    [Header("Perfiles de Dificultad (opcional — si no se asignan usa presets en código)")]
    [SerializeField] private DungeonDifficultyConfig easyConfig;
    [SerializeField] private DungeonDifficultyConfig normalConfig;
    [SerializeField] private DungeonDifficultyConfig hardConfig;
    [SerializeField] private DungeonDifficultyConfig nightmareConfig;
    [SerializeField] private bool useFallbackPresets = true;

    private DungeonLayoutGenerator    layoutGen;
    private SmartConnectionGenerator  connectionGen;
    private RoomArchitectureGenerator archGen;
    private AccessibilityValidator    validator;
    private DungeonDebugger           debugger;
    // Agrega estos campos al DungeonGenerator junto a los existentes
    private DungeonGraphGenerator  graphGen;
    private DoorSocketResolver socketResolver;
    private EnemySpawner           enemySpawner;
    private LootGenerator          lootGen;
    private PropDecorator          propDecorator;
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
        layoutGen     = GetComponent<DungeonLayoutGenerator>();
        connectionGen = GetComponent<SmartConnectionGenerator>();
        archGen       = GetComponent<RoomArchitectureGenerator>();
        validator     = GetComponent<AccessibilityValidator>();
        debugger      = GetComponent<DungeonDebugger>();
        graphGen      = GetComponent<DungeonGraphGenerator>();
        socketResolver = GetComponent<DoorSocketResolver>();
        enemySpawner  = GetComponent<EnemySpawner>();
        lootGen       = GetComponent<LootGenerator>();
        propDecorator = GetComponent<PropDecorator>();

    }

    // ─────────────────────────────────────────────
    // GUI
    // ─────────────────────────────────────────────

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(15, 15, 310, 480));

        var title = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
        var info  = new GUIStyle(GUI.skin.label) { fontSize = 11 };

        GUILayout.Label("⚔  Procedural Dungeon Generator", title);
        GUILayout.Label("Bitmask Tileset | Boss Room | MST Kruskal", info);
        GUILayout.Space(8);

        // Selector de dificultad
        GUILayout.Label("Dificultad:", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
        GUILayout.BeginHorizontal();
        DiffBtn(DifficultyLevel.Easy,      "Easy",      Color.green);
        DiffBtn(DifficultyLevel.Normal,    "Normal",    Color.yellow);
        DiffBtn(DifficultyLevel.Hard,      "Hard",      new Color(1f, .5f, 0f));
        DiffBtn(DifficultyLevel.Nightmare, "Nightmare", Color.red);
        GUILayout.EndHorizontal();
        GUILayout.Space(10);

        if (GUILayout.Button("▶  Generar Dungeon", GUILayout.Height(50)))
        {
            if (config.useRandomSeed) config.seed = (int)System.DateTime.Now.Ticks;
            Generate();
        }
        GUILayout.Space(4);
        if (GUILayout.Button("♻  Reproducir (misma semilla)", GUILayout.Height(36))) Generate();
        GUILayout.Space(4);
        if (GUILayout.Button("🗑  Limpiar", GUILayout.Height(32))) Clear();

        GUILayout.Space(10);
        GUILayout.Label("──────────────────────────────", info);

        if (rooms != null)
        {
            var diff = GetDiff(difficulty);
            var boss = rooms.Count > 0 ? rooms[rooms.Count - 1] : null;
            GUILayout.Label($"Dificultad:  {diff.displayName}", info);
            GUILayout.Label($"Salas:       {rooms.Count}", info);
            GUILayout.Label($"Boss:        {(boss != null ? boss.DebugLabel : "ninguna")}", info);
            GUILayout.Label($"Seed:        {config.seed}", info);
            GUILayout.Label($"CellSize:    {config.cellSize}u", info);
            GUILayout.Label($"Accesible:   {(lastValid ? "✓ SÍ" : "✗ NO")}", info);
        }
        else
        {
            GUILayout.Label("[ Sin dungeon generado ]", info);
        }

        GUILayout.EndArea();
    }

    private void DiffBtn(DifficultyLevel level, string label, Color active)
    {
        GUI.backgroundColor = difficulty == level ? active : Color.white;
        if (GUILayout.Button(label, GUILayout.Height(28))) difficulty = level;
        GUI.backgroundColor = Color.white;
    }

    // ─────────────────────────────────────────────
    // GENERACIÓN — 5 FASES
    // ─────────────────────────────────────────────

    public void Generate()
    {
        Clear();
        var rng = new System.Random(config.seed); // <- usar System.Random, no Unity.Random
        // Unity.Random no es reproducible entre plataformas de forma confiable
        Random.InitState(config.seed); // solo para efectos visuales de Unity

        var diff = GetDiff(difficulty);

        // [1] Grilla
        grid = new DungeonGrid(config.gridWidth, config.gridHeight, config.cellSize);

        // [2] Graph lógico — solo si el componente existe
        DungeonGraph graph = null;
        if (graphGen != null)
            graph = graphGen.Generate(GetDiff(difficulty), new System.Random(config.seed));

        // [3] Layout
        rooms = (graph != null && layoutGen != null)
            ? layoutGen.GenerateRoomsFromGraph(grid, graph, GetDiff(difficulty), new System.Random(config.seed))
            : layoutGen.GenerateRooms(grid, GetDiff(difficulty));

        // [4] Conexiones (firma original, sin cambios)
        connectionGen.ConnectRooms(rooms, grid, GetDiff(difficulty));

        // Socket resolver — solo si existe
        socketResolver?.ResolveAllSockets(rooms, grid);

        // [5] Geometría 
        archGen.BuildGeometry(grid, rooms);

        // [6] Spawning — solo si existen los componentes
        if (enemySpawner != null || lootGen != null || propDecorator != null)
        {
            int depth = config.dungeonDepth;
            var spawnDiff  = GetDiff(difficulty);
            // TODO: conectar biome config
        }

        if (propDecorator != null && config.currentBiome?.PropCollection != null)
        {
            foreach (var room in rooms)
            {
                if (room.RoomType == RoomType.Start) continue; // sala inicio sin decoración
                propDecorator.DecorateRoom(room, config.currentBiome.PropCollection, grid, rng);
            }
        }

        // [7] Validación — 
        lastValid = validator.ValidateAll(rooms, grid);
        if (!lastValid)
        {
            Debug.LogWarning("[DUNGEON] Dungeon inválido — retrying...");
            config.seed++;
            Generate(); // retry con seed+1
        }
    }

    // ─────────────────────────────────────────────
    // LIMPIEZA
    // ─────────────────────────────────────────────

    public void Clear()
    {
        archGen?.ClearGeometry();
        grid = null; rooms = null; lastValid = false;
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

        if (assigned != null)       return assigned;
        if (useFallbackPresets)     return DungeonDifficultyConfig.CreatePreset(level);

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

    private void Log(string msg)  { if (config.logStats) Debug.Log($"[DUNGEON] {msg}"); }
    private void LogH(string msg) { if (config.logStats) Debug.Log($"[DUNGEON] ══════ {msg} ══════"); }
}
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Orquestador del sistema de generación procedural de mazmorras.
/// 
/// FLUJO:
///   1. Inicializar semilla y grilla vacía
///   2. LayoutGenerator    → coloca salas (sin solapamiento, parámetros por dificultad)
///   3. ConnectionGenerator → MST + pasillos borde-a-borde (más cortos)
///   4. ArchitectureGenerator → instancia prefabs
///   5. AccessibilityValidator → BFS doble (grafo + grilla)
///   6. DungeonDebugger → actualiza gizmos
/// 
/// SETUP EN UNITY:
///   1. Crear GameObject "DungeonGenerator"
///   2. Agregar DungeonGenerator.cs (añade automáticamente los otros componentes)
///   3. Crear DungeonConfig asset (Assets > Create > Dungeon > Config)
///   4. Crear 4 DungeonDifficultyConfig assets (uno por nivel)
///      O usar los presets en código (ver campo useFallbackPresets)
///   5. Asignar prefabs de suelo y pared en DungeonConfig
///   6. Play → seleccionar dificultad → "Generar"
/// </summary>
[RequireComponent(typeof(DungeonLayoutGenerator))]
[RequireComponent(typeof(SmartConnectionGenerator))]
[RequireComponent(typeof(RoomArchitectureGenerator))]
[RequireComponent(typeof(AccessibilityValidator))]
[RequireComponent(typeof(DungeonDebugger))]
public class DungeonGenerator : MonoBehaviour
{
    [Header("Configuración Base")]
    [SerializeField] private DungeonConfig config;

    [Header("Perfiles de Dificultad (opcional — si no se asignan, se usan presets)")]
    [SerializeField] private DungeonDifficultyConfig easyConfig;
    [SerializeField] private DungeonDifficultyConfig normalConfig;
    [SerializeField] private DungeonDifficultyConfig hardConfig;
    [SerializeField] private DungeonDifficultyConfig nightmareConfig;

    [Tooltip("Si está activo y no hay assets asignados, usa los presets en código.")]
    [SerializeField] private bool useFallbackPresets = true;

    // Dificultad actual (modificable en GUI durante Play)
    private DifficultyLevel currentDifficulty = DifficultyLevel.Normal;

    // Componentes
    private DungeonLayoutGenerator    layoutGen;
    private SmartConnectionGenerator  connectionGen;
    private RoomArchitectureGenerator archGen;
    private AccessibilityValidator    validator;
    private DungeonDebugger           debugger;

    // Estado
    private DungeonGrid    currentGrid;
    private List<RoomData> currentRooms;
    private bool           lastValidationPassed;

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
    }

    // ─────────────────────────────────────────────
    // GUI (Play Mode)
    // ─────────────────────────────────────────────

    private void OnGUI()
    {
        float panelW = 300f;
        GUILayout.BeginArea(new Rect(15, 15, panelW, 500f));

        // ── Título ──────────────────────────────────────────────────
        GUILayout.Label("⚔  Procedural Dungeon Generator",
            new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold });
        GUILayout.Label("Grid-Based | MST | Borde-a-Borde | Accesible 100%",
            new GUIStyle(GUI.skin.label) { fontSize = 10 });

        GUILayout.Space(10);

        // ── Selector de dificultad ───────────────────────────────────
        GUILayout.Label("Dificultad:",
            new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });

        GUILayout.BeginHorizontal();
        DrawDifficultyButton(DifficultyLevel.Easy,      "Easy",      Color.green);
        DrawDifficultyButton(DifficultyLevel.Normal,    "Normal",    Color.yellow);
        DrawDifficultyButton(DifficultyLevel.Hard,      "Hard",      new Color(1f, 0.5f, 0f));
        DrawDifficultyButton(DifficultyLevel.Nightmare, "Nightmare", Color.red);
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        // ── Botones de generación ────────────────────────────────────
        if (GUILayout.Button("▶  Generar Dungeon", GUILayout.Height(48)))
        {
            if (config.useRandomSeed)
                config.seed = (int)System.DateTime.Now.Ticks;
            GenerateDungeon();
        }

        GUILayout.Space(4);

        if (GUILayout.Button("♻  Reproducir (misma semilla)", GUILayout.Height(36)))
            GenerateDungeon();

        GUILayout.Space(4);

        if (GUILayout.Button("🗑  Limpiar", GUILayout.Height(32)))
            ClearDungeon();

        GUILayout.Space(10);
        GUILayout.Label("──────────────────────────────",
            new GUIStyle(GUI.skin.label) { fontSize = 10 });

        // ── Stats ────────────────────────────────────────────────────
        GUIStyle info = new GUIStyle(GUI.skin.label) { fontSize = 11 };

        if (currentRooms != null)
        {
            DungeonDifficultyConfig diff = GetDifficultyConfig(currentDifficulty);
            GUILayout.Label($"Dificultad:   {diff.displayName}", info);
            GUILayout.Label($"Salas:        {currentRooms.Count}", info);
            GUILayout.Label($"Seed:         {config.seed}", info);
            GUILayout.Label($"Grilla:       {config.gridWidth}×{config.gridHeight} celdas", info);
            GUILayout.Label($"Celda:        {config.cellSize} u  |  Pasillo: {diff.corridorWidth} c", info);
            GUILayout.Label($"Accesible:    {(lastValidationPassed ? "✓ SÍ" : "✗ NO")}", info);
        }
        else
        {
            GUILayout.Label("[ No hay dungeon generado ]", info);
        }

        GUILayout.EndArea();
    }

    private void DrawDifficultyButton(DifficultyLevel level, string label, Color activeColor)
    {
        bool isActive = currentDifficulty == level;

        GUI.backgroundColor = isActive ? activeColor : Color.white;
        if (GUILayout.Button(label, GUILayout.Height(28)))
            currentDifficulty = level;
        GUI.backgroundColor = Color.white;
    }

    // ─────────────────────────────────────────────
    // GENERACIÓN
    // ─────────────────────────────────────────────

    public void GenerateDungeon()
    {
        if (config == null)
        {
            Debug.LogError("[DUNGEON] DungeonConfig no asignado.");
            return;
        }

        ClearDungeon();

        Random.InitState(config.seed);
        DungeonDifficultyConfig difficulty = GetDifficultyConfig(currentDifficulty);

        LogHeader($"GENERANDO | Dificultad: {difficulty.displayName} | Seed: {config.seed}");

        // ── [1] Grilla vacía ─────────────────────────────────────────
        Log("[1/5] Inicializando grilla...");
        currentGrid = new DungeonGrid(config.gridWidth, config.gridHeight);

        // ── [2] Colocar salas ────────────────────────────────────────
        Log("[2/5] Colocando salas...");
        currentRooms = layoutGen.GenerateRooms(currentGrid, difficulty);

        if (currentRooms.Count < 2)
        {
            Debug.LogError("[DUNGEON] Menos de 2 salas. Revisa el DifficultyConfig " +
                           "(tamaños de sala vs tamaño de grilla).");
            return;
        }

        // ── [3] Conexiones MST + pasillos ────────────────────────────
        Log("[3/5] Conectando salas...");
        connectionGen.ConnectRooms(currentRooms, currentGrid, difficulty);

        // ── [4] Geometría ────────────────────────────────────────────
        Log("[4/5] Generando arquitectura...");
        archGen.BuildGeometry(currentGrid);

        // ── [5] Validación ───────────────────────────────────────────
        Log("[5/5] Validando accesibilidad...");
        lastValidationPassed = validator.ValidateAll(currentRooms, currentGrid);

        // ── Debug ────────────────────────────────────────────────────
        debugger.SetData(currentGrid, currentRooms);

        // ── Resumen ──────────────────────────────────────────────────
        LogHeader("GENERACIÓN COMPLETA");
        Log($"  Dificultad:  {difficulty.displayName}");
        Log($"  Salas:       {currentRooms.Count}");
        Log($"  Pasillo W:   {difficulty.corridorWidth} celda(s)");
        Log($"  Extra conn:  {difficulty.extraConnectionRatio * 100:F0}%");
        Log($"  Seed:        {config.seed}");
        Log($"  Accesible:   {(lastValidationPassed ? "✓ SÍ" : "✗ NO")}");
        LogHeader("");
    }

    // ─────────────────────────────────────────────
    // LIMPIEZA
    // ─────────────────────────────────────────────

    public void ClearDungeon()
    {
        archGen?.ClearGeometry();
        currentGrid          = null;
        currentRooms         = null;
        lastValidationPassed = false;
        debugger?.SetData(null, null);
    }

    // ─────────────────────────────────────────────
    // SELECCIÓN DE DIFICULTAD
    // ─────────────────────────────────────────────

    /// <summary>
    /// Devuelve el DungeonDifficultyConfig para el nivel dado.
    /// Prioridad: asset asignado en Inspector → preset en código.
    /// </summary>
    public DungeonDifficultyConfig GetDifficultyConfig(DifficultyLevel level)
    {
        DungeonDifficultyConfig assigned = level switch
        {
            DifficultyLevel.Easy      => easyConfig,
            DifficultyLevel.Normal    => normalConfig,
            DifficultyLevel.Hard      => hardConfig,
            DifficultyLevel.Nightmare => nightmareConfig,
            _                         => normalConfig
        };

        if (assigned != null) return assigned;

        if (useFallbackPresets)
        {
            Log($"[Config] No hay asset para {level}, usando preset en código");
            return DungeonDifficultyConfig.CreatePreset(level);
        }

        Debug.LogError($"[DUNGEON] No hay DifficultyConfig para {level} y useFallbackPresets=false");
        return DungeonDifficultyConfig.CreatePreset(DifficultyLevel.Normal);
    }

    // ─────────────────────────────────────────────
    // ACCESO EXTERNO
    // ─────────────────────────────────────────────

    public List<RoomData>         GetRooms()      => currentRooms;
    public DungeonGrid            GetGrid()       => currentGrid;
    public DifficultyLevel        GetDifficulty() => currentDifficulty;

    /// <summary>
    /// Cambia la dificultad desde otro script y regenera el dungeon.
    /// </summary>
    public void SetDifficultyAndRegenerate(DifficultyLevel level)
    {
        currentDifficulty = level;
        if (config.useRandomSeed) config.seed = (int)System.DateTime.Now.Ticks;
        GenerateDungeon();
    }

    // ─────────────────────────────────────────────
    // UTILIDADES
    // ─────────────────────────────────────────────

    private void Log(string msg)
    {
        if (config != null && config.logStats) Debug.Log($"[DUNGEON] {msg}");
    }

    private void LogHeader(string msg)
    {
        if (config != null && config.logStats)
            Debug.Log($"[DUNGEON] ══════ {msg} ══════");
    }
}
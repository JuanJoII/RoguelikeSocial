# Procedural Dungeon System
 
Sistema de generación procedural de dungeons para Unity. Arquitectura modular en 7 fases.
 
---
 
## Arquitectura — Las 7 fases
 
```
Generate() ejecuta esto en orden:
 
1. DungeonGrid                → inicializa grilla vacía (fuente lógica)
2. DungeonGraphGenerator      → grafo: Start → Combat×N → [Treasure] → [Elite] → Boss
3. DungeonLayoutGenerator     → coloca RoomData en el grid
4. SmartConnectionGenerator   → conecta salas con pasillos (MST Kruskal + Manhattan)
   DoorSocketResolver         → registra sockets de puertas
5. RoomArchitectureGenerator  → instancia suelos, paredes y puertas en escena
6. EnemySpawner + LootGenerator + PropDecorator → pobla cada sala
7. AccessibilityValidator     → BFS verifica que Start → Boss sea navegable
                                 si falla: seed++ y reintenta automáticamente
```
 
---
 
## Estructura de carpetas
 
```
Procedural/
├── Architecture/   RoomArchitectureGenerator.cs
├── Connections/    SmartConnectionGenerator.cs · DoorSocketResolver.cs
├── Core/           DungeonGrid.cs · DungeonConfig.cs · DungeonDifficultyConfig.cs
│                   DungeonGraph.cs · DungeonNode.cs · RoomData.cs · DoorSocket.cs
├── Debug/          DungeonDebugger.cs          ← solo pruebas
├── Difficulty/     DifficultyScaler.cs · ThreatBudgetCalculator.cs
├── Layout/         DungeonLayoutGenerator.cs · DungeonGraphGenerator.cs
├── ScriptableObjects/ BiomeConfig · EnemyPool · EnemyData · LootTable · PropCollection · PropData
├── SnapPoint/      RoomModule.cs · SnapPoint.cs
├── Spawning/       EnemySpawner.cs · LootGenerator.cs · PropDecorator.cs
└── Validation/     AccessibilityValidator.cs · DungeonValidatorEditor.cs · DungeonGenerator.cs
```
 
---
 
## Llamar la generación desde código
 
```csharp
// Referencia al orquestador
[SerializeField] private DungeonGenerator dungeonGenerator;
 
// Generar (seed aleatoria)
dungeonGenerator.Config.useRandomSeed = true;
dungeonGenerator.Generate();
 
// Generar reproducible
dungeonGenerator.Config.seed = 12345;
dungeonGenerator.Config.useRandomSeed = false;
dungeonGenerator.Generate();
 
// Desde el hub: asignar profundidad y biome antes de generar
dungeonGenerator.Config.dungeonDepth   = 2;
dungeonGenerator.Config.currentBiome   = miBiomeConfig;
dungeonGenerator.Generate();
 
// Cambiar dificultad y regenerar
dungeonGenerator.SetDifficultyAndRegenerate(DifficultyLevel.Hard);
 
// Limpiar dungeon actual
dungeonGenerator.Clear();
```
 
---
 
## Leer salas y grid
 
```csharp
List<RoomData> salas = dungeonGenerator.GetRooms();
DungeonGrid    grid  = dungeonGenerator.GetGrid();
 
// Iterar salas
foreach (var room in salas)
{
    Debug.Log(room.DebugLabel);              // "[0:Start] 5×4 @(12,8)"
    Debug.Log(room.RoomType);               // Start, Combat, Treasure, Elite, Boss
    Debug.Log(room.Bounds);                 // RectInt — posición y tamaño en celdas
    Debug.Log(room.ConnectedRoomIds.Count); // cuántas salas conectadas
}
 
// Sala de inicio y boss
RoomData start = salas[0];
RoomData boss  = salas[^1];
 
// Centro de una sala en coordenadas mundo
Vector3 centro = grid.RectCenter(room.Bounds);
```
 
---
 
## Trabajar con el DungeonGrid
 
```csharp
// Tipos de celda
// Empty, Room, Corridor, Wall
 
// Leer una celda
CellType tipo = grid.GetCell(new Vector2Int(x, y));
 
// ¿Es sala?
bool esSala    = grid.GetCell(pos) == CellType.Room;
 
// ¿Es pasillo?
bool esPasillo = grid.GetCell(pos) == CellType.Corridor;
 
// ¿Es transitable? (sala O pasillo)
bool transitable = grid.IsFloor(pos);
 
// ¿Está vacío?
bool vacio = grid.IsEmpty(pos);
 
// Celda → posición mundo (pivot centro del prefab)
Vector3 posWorld = grid.CellCenter(new Vector2Int(x, y));
 
// Celda → posición mundo (esquina inferior izquierda)
Vector3 origen = grid.CellOrigin(new Vector2Int(x, y));
 
// Posición mundo → celda
Vector2Int celda = grid.WorldToCell(transform.position);
```
 
---
 
## API pública de DungeonGenerator
 
```csharp
void            Generate();
void            Clear();
void            SetDifficultyAndRegenerate(DifficultyLevel level);
List<RoomData>  GetRooms();
DungeonGrid     GetGrid();
DifficultyLevel GetDifficulty();
DungeonConfig   Config { get; }
```
 
---
 
## Setup en Inspector
 
El GameObject principal necesita todos estos componentes:
 
```
DungeonGenerator · DungeonLayoutGenerator · SmartConnectionGenerator
RoomArchitectureGenerator · DoorSocketResolver · DungeonGraphGenerator
EnemySpawner · LootGenerator · PropDecorator · AccessibilityValidator
DungeonDebugger (opcional, solo pruebas)
```
 
### Assets necesarios
 
| Crear con... | Asignar en |
|---|---|
| Dungeon/Config → `DungeonConfig` | Todos los sistemas que lo pidan |
| Dungeon/Difficulty Config × 4 | `DungeonGenerator` (Easy/Normal/Hard/Nightmare) |
| Dungeon/Biome Config → `BiomeConfig` | `DungeonConfig.currentBiome` |
| Dungeon/Enemy Pool | `BiomeConfig.EnemyPool` + `EnemySpawner` |
| Dungeon/Loot Table | `BiomeConfig.LootTable` |
| Dungeon/Prop Collection | `BiomeConfig.PropCollection` |
 
### DungeonConfig — valores mínimos
 
```
cellSize      = 4      ← debe coincidir con el ancho de tu prefab de pared
gridWidth     = 80
gridHeight    = 80
dungeonDepth  = 0      ← se incrementa desde el hub
useRandomSeed = true
currentBiome  = [tu BiomeConfig]
```
 
### BiomeConfig — valores mínimos
 
```
floorRoom     = [prefab suelo sala]
floorCorridor = [prefab suelo pasillo]
wallStraight  = [prefab pared]
EnemyPool     = [tu EnemyPool]
LootTable     = [tu LootTable]
PropCollection= [tu PropCollection]
```
 
---
 
## RoomData — campos útiles en runtime
 
```csharp
room.Id                  // int — identificador único
room.RoomType            // Start / Combat / Treasure / Elite / Boss
room.Bounds              // RectInt — posición y tamaño en celdas
room.CenterCellInt       // Vector2Int — centro en celdas
room.ConnectedRoomIds    // List<int> — IDs de salas conectadas
room.Sockets             // List<DoorSocket> — puertas detectadas
room.ResolvedEnemies     // List<EnemyData> — enemigos asignados
room.ResolvedLoot        // List<LootEntry> — loot asignado
room.IsCleared           // bool — todos los enemigos derrotados
room.IsVisited           // bool — el jugador entró alguna vez
 
// Llamar cuando el jugador limpia la sala
room.OnCleared();        // marca IsCleared = true y lanza evento
```
 
---
 
## Notas
 
- **Mismo seed = mismo dungeon** siempre, útil para bugs y testing.
- **BiomeConfig por puerta**: asignar distintos `BiomeConfig` a `currentBiome` antes de `Generate()` produce dungeons con temas visuales distintos desde cada puerta del hub.
- **Solo pruebas**: `DungeonDebugger`, `DungeonValidatorEditor` y `OnGUI` en `DungeonGenerator` pueden eliminarse en producción. La generación funciona igual llamando `Generate()` directamente.

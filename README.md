# 🔥 HellBound

> Top-down action roguelike para móviles con generación procedural de dungeons y backend social en tiempo real.

**Universidad Militar Nueva Granada · Desarrollo de Videojuegos para Móviles · GP5**  
Nicolas Hurtado · Juan José Camacho · Stefany López

---

## ¿Qué es HellBound?

HellBound es un roguelike de acción con perspectiva top-down diseñado para Android. El jugador explora dungeons generados proceduralmente organizados en tres biomas — **Stone**, **Fire** y **Alien** — cada uno con enemigos, decoración y un jefe final únicos. El progreso se persiste en la nube mediante Supabase, con ranking global de jugadores en tiempo real.

Cada run es diferente: el layout del dungeon, las oleadas de enemigos, la decoración y el loot se generan mediante código en cada partida.

---

## Stack Técnico

| Componente | Tecnología |
|---|---|
| Motor | Unity 2022.3 LTS |
| Lenguaje | C# + UniTask (async) |
| Input | Unity New Input System |
| UI | UI Toolkit (UIElements) |
| Backend / Auth | Supabase (Auth + Realtime DB) |
| Arquitectura | Singleton Managers + ScriptableObjects + Object Pooling |
| Render | Universal Render Pipeline (URP) |

---

## Características principales

- **Generación procedural completa** — layout de salas (MST Kruskal), paredes via SnapPoints, spawning de enemigos por oleadas, decoración ambiental por reglas de celda y loot aleatorio por tabla de probabilidades
- **Tres biomas jugables** con estética, enemigos y boss únicos por bioma
- **Sistema de progresión por bioma** — Stone desbloqueado desde el inicio; Fire y Alien se desbloquean al completar el bioma anterior
- **Backend social** — registro, login, guardado de progreso y ranking global con Supabase
- **IA de enemigos** con steering behaviors (cohesión, separación, evasión de paredes)
- **Tres bosses únicos** — GolemOakBoss, GolemFireBoss, AlienBoss
- **Controles móviles** — joystick virtual + botones táctiles

---

## Estructura del proyecto

```
Assets/
├── Scripts/
│   ├── Procedural/          # Pipeline de generación del dungeon
│   │   ├── Core/            # DungeonGrid, DungeonConfig, RoomData
│   │   ├── Architecture/    # RoomArchitectureGenerator, SnapPoint, RoomModule
│   │   ├── Connections/     # SmartConnectionGenerator, DoorSocketResolver
│   │   ├── Layout/          # DungeonLayoutGenerator, DungeonGraphGenerator
│   │   ├── Spawning/        # EnemySpawner, LootGenerator, PropDecorator
│   │   ├── ScriptableObjects/ # BiomeConfig, PropCollection, EnemyPool
│   │   └── Validation/      # DungeonGenerator, AccessibilityValidator
│   ├── Player/              # PlayerController, WeaponSystem, Health
│   ├── Enemies/             # EnemyAI, EnemyGroup, WaveManager, BossBase
│   ├── Social/              # AuthManager, DataManager, RankingPanel
│   ├── Audio/               # AudioManager, SoundData
│   └── UI/                  # UIManager, ViewsManager, PauseManager
├── Prefabs/
│   ├── Biomes/              # Prefabs de suelo, paredes y puertas por bioma
│   ├── Enemies/             # Prefabs de enemigos y bosses
│   └── Props/               # Decoración procedural por bioma
└── Packages/                # Supabase.dll, Postgrest.dll, Realtime.dll, Gotrue.dll
```

---

## Setup y configuración

### Requisitos

- Unity 2022.3 LTS con módulos **URP** y **Android Build Support**
- IDE: VS Code con extensión *Visual Studio Tools for Unity* o JetBrains Rider
- Cuenta en [Supabase](https://supabase.com) con un proyecto activo

### 1. Clonar el repositorio

```bash
git clone https://github.com/JuanJoII/RoguelikeSocial.git
cd RoguelikeSocial
```

### 2. Abrir en Unity

Abre Unity Hub → **Add project from disk** → selecciona la carpeta clonada. Unity instalará los packages automáticamente.

### 3. Configurar Supabase

1. En el editor de Unity, abre la escena `AuthScene`
2. Selecciona el GameObject `AuthManager`
3. En el Inspector, ingresa tu **Supabase URL** y **Supabase Anon Key**

> ⚠️ Nunca subas tus credenciales al repositorio. Usa un archivo de configuración local excluido por `.gitignore`.

### 4. Orden de escenas

```
AuthScene → MainMenu → DungeonScene → Gameplay
```

Asegúrate de que las escenas estén en ese orden en **Build Settings**.

---

## Pipeline de generación procedural

El dungeon se construye en 7 fases secuenciales cada vez que el jugador inicia un run:

```
DungeonGraphGenerator   →  grafo lógico de salas
DungeonLayoutGenerator  →  posición de salas en la grilla (DungeonGrid)
SmartConnectionGenerator→  pasillos via MST Kruskal
RoomArchitectureGenerator→  geometría 3D (suelos, paredes, puertas)
EnemySpawner            →  oleadas por sala según BiomeConfig
LootGenerator           →  recompensas en salas Treasure
PropDecorator           →  decoración ambiental por tipo de celda
```

---

## Sistema de biomas y progresión

| Bioma | Estado inicial | Desbloqueo | Boss |
|---|---|---|---|
| Stone | ✅ Disponible | — | GolemOakBoss |
| Fire | 🔒 Bloqueado | Completar Stone | GolemFireBoss |
| Alien | 🔒 Bloqueado | Completar Fire | AlienBoss |

Los estados (`locked` / `unlocked` / `completed`) se persisten en Supabase al terminar cada run.

---

## Estructura de datos en Supabase

```json
{
  "players": {
    "uid": {
      "username": "jugador01",
      "totalScore": 8400,
      "currentRunScore": 0,
      "maxUnlockedLevel": 1,
      "biomes": {
        "stoneState": "completed",
        "fireState": "unlocked",
        "alienState": "locked",
        "stoneBest": 4200,
        "fireBest": 0,
        "alienBest": 0
      }
    }
  },
  "ranking": {
    "uid": {
      "username": "jugador01",
      "totalScore": 8400,
      "maxUnlockedLevel": 1
    }
  }
}
```

---

## Troubleshooting

| Problema | Solución |
|---|---|
| `Missing Supabase DLLs` | Verifica que `Assets/Packages/` contenga `Supabase.dll` y sus dependencias |
| Dungeon no genera | Verifica que `DungeonGenerator` tenga un `DungeonConfig` asignado y `useRandomSeed` activo |
| IDE no muestra errores de Unity | Ve a **Edit → Preferences → External Tools → Regenerate project files** |
| Props fuera de las salas | Aumenta `maxGenerationAttempts` en `DungeonGenerator` si el validador rechaza constantemente |
| Bioma Fire inicia en Boss | Es intencional durante testing para verificar el boss directamente |

---

## Créditos

| Integrante | Rol |
|---|---|
| Nicolas Hurtado | Sistemas de enemigos, IA y bosses |
| Juan José Camacho | Backend, autenticación y ranking |
| Stefany López | Generación procedural y arquitectura del dungeon |

---

<p align="center">
  Hecho con Unity · Supabase · C# — UMNG 2025
</p>

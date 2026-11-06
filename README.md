# CAUGHT-GAME

**Simulación de inteligencia artificial para videojuegos en Unity 3D**  
**UTN — Desarrollo y Producción de Videojuegos | Parciales 1 y 2**

CAUGHT-GAME es una simulación de agentes autónomos: un cazador alienígena (Hunter) interactúa con un grupo de agentes Astra (Boids), mientras cuatro depredadores (Wolves) patrullan el escenario, perciben al cazador y coordinan su búsqueda. El proyecto reúne _steering behaviours_, máquinas de estados finitos (FSM), percepción y navegación sobre un grafo con A\* propio.

> Este repositorio conserva el trabajo de la primera entrega e incorpora las funcionalidades de la segunda. La escena integrada del Parcial 2 es la referencia para demostrar la versión actual.

## Requisitos

- **Unity Editor:** `6000.3.17f1`.
- Proyecto 3D de Unity; abrir desde Unity Hub usando la carpeta raíz del repositorio.
- La navegación del Parcial 2 utiliza una implementación propia de **A\***, **sin Unity NavMesh**.

## Entregas

### Parcial 1 — Flocking, steering y FSM

**Agentes Astra (Boids)**

- Grupo de seis agentes autónomos, sin controlador central de movimiento.
- Comportamientos **Separation**, **Alignment**, **Cohesion**, **Wander**, **Arrive** y **Evade**.
- Prevención de superposiciones, evasión de obstáculos y permanencia en el área de juego.
- Interacción con puntos de interés y ciclo de vida: daño, muerte, recolección y respawn.
- Sensores y Gizmos para visualizar información local.

**Hunter alienígena**

- FSM original con **Patrol**, **Pursuit**, **Attack** y **Gather**.
- Percepción, combate cuerpo a cuerpo y a distancia, tiempos entre ataques y animación.
- Movimiento, cámara de seguimiento y feedback de depuración.

### Parcial 2 — Percepción, grafos y pathfinding

**Navegación**

- Escenario con obstáculos, grid/grafo navegable y búsqueda de rutas mediante **A\*** propio.
- Navegación de Hunter y Wolves sin depender de NavMesh.
- Replanificación de rutas para persecuciones, búsquedas y reinicios; llegada al destino exacto cuando es transitable.

**Hunter**

- FSM de la segunda entrega: **Search**, **Gather**, **Exit**, **Flee** y **Dead**.
- Recolección de Astras, desplazamiento a Exit y reinicio del ciclo desde Spawn.
- Huida hacia una Safe Zone al detectar una amenaza y recuperación del objetivo anterior según el progreso.
- Respawn y restauración de la navegación después de la muerte o de completar Exit.

**Wolves L1–L4**

- Cuatro depredadores con patrullas independientes y estados de patrulla, persecución, búsqueda y ataque.
- Detección mediante campo de visión y comprobación de línea de visión (_line of sight_, LOS).
- Comunicación de alertas por aullidos: al detectar al Hunter, un Wolf transmite la última posición conocida; los demás pueden dirigirse a investigarla.
- Interrupción de persecución/búsqueda cuando el Hunter entra a una Safe Zone, con retorno a patrulla.
- Feedback visual de estados, emisión y recepción de alertas.

**Presentación y depuración**

- HUD compacto del Hunter con estado FSM y progreso de recolección.
- HUD individuales de los cuatro Wolves.
- Gizmos de waypoints y herramientas de diagnóstico.
- Iluminación inspirada en el escenario de la primera entrega y ambientación visual con recursos de Kenney.

> La comunicación por aullidos está implementada como evento de IA y feedback visual. No se presupone la existencia de un clip de audio asignado en todas las escenas.

## Arquitectura general

| Subsistema    | Componentes representativos                                                | Responsabilidad                              |
| ------------- | -------------------------------------------------------------------------- | -------------------------------------------- |
| Astra / Boids | `BoidAgent`, `BoidSensor`, `SteeringBehaviour` y comportamientos derivados | Percepción local, flocking y steering        |
| Hunter        | `FSMAgent`, `HunterMovement`, `HunterPerception`, `HunterCombat`           | Estados, movimiento, detección y combate     |
| Navegación    | `GraphNavigationAgent`, controlador de navegación del Hunter, grafo y A\*  | Cálculo y seguimiento de rutas               |
| Wolves        | `PredatorFSM`, `PredatorPerception`                                        | Patrulla, detección, persecución y búsqueda  |
| Comunicación  | `PredatorAlertSystem`, `PredatorAlertBootstrap`                            | Distribución de alertas entre Wolves         |
| Feedback      | `HunterDebugFeedback`, `PredatorDebugFeedback`                             | Información en pantalla para la demostración |

Los agentes conservan sus máquinas de estados; el sistema de navegación resuelve rutas y los componentes de percepción informan sobre objetivos detectados. El sistema de alertas comunica la **última posición conocida**, no un seguimiento omnisciente del Hunter.

## Cómo ejecutar

1. Clonar el repositorio:

   ```bash
   git clone https://github.com/TomasMolinaGD/CAUGHT-GAME.git
   ```

2. Abrir la carpeta `CAUGHT-GAME` desde Unity Hub con **Unity 6000.3.17f1**.
3. Abrir la escena integrada del Parcial 2:

   `Assets/Scenes/Tests/Parcial2_B3_WolfIntegration.unity`

4. Iniciar **Play Mode** y observar el comportamiento autónomo de los agentes.
5. Para consultar la entrega anterior, revisar la escena original documentada para el Parcial 1:

   `Assets/Scenes/LastSave_ExteriorPrototype.unity`

**Controles y visualización:**

- `F1`: mostrar/ocultar diagnóstico extendido del Hunter.
- `F2`: mostrar/ocultar diagnóstico extendido de Wolves.
- **Scene View > Gizmos:** mostrar rutas, marcadores y waypoints cuando estén habilitados.
- Para ver la presentación limpia, desactivar **Gizmos** en Game View.

**Nota de build:** verificar antes de generar el ejecutable que la escena B3 esté incluida y seleccionada como escena inicial en **Build Profiles / Scene List**. No asumir que la escena actual de arranque del repositorio ya apunta a B3.

## Escenario y assets

El escenario funcional mantiene la geometría, los colliders y las capas usados por el sistema de navegación y percepción. Los elementos decorativos no deben introducir obstáculos o triggers adicionales.

Se utilizan recursos de **Kenney Space Kit 2.0** para ambientación de ciencia ficción. **Los recursos de Kenney están publicados bajo licencia CC0**; esta licencia corresponde a los assets de Kenney y no define por sí misma la licencia del código del proyecto.

## Equipo y responsabilidades

### Tomás Molina — Hunter / Alien (Parcial 1)

Desarrolló la base del cazador alienígena, su FSM, movimiento, percepción, combate, recolección, animación y herramientas de feedback. Entre sus componentes: `FSMAgent`, `State`, `stateMachine`, `PatrolState`, `PursuitState`, `AttackState`, `GatherState`, `HunterMovement`, `HunterPerception`, `HunterCombat`, `HunterProjectile`, `HunterAnimationController`, `AlienVisualGrounding`, `HunterInterestSpawner`, `HunterDebugFeedback` y `HunterCameraFollow`.

### Lucas González — Astras y flocking (Parcial 1)

Desarrolló los agentes Astra, sensores, _steering behaviours_, ciclo de vida, interacción, animación y visualización. Entre sus componentes: `BoidAgent`, `BoidSensor`, `SteeringBehaviour`, `SeparationBehaviour`, `EmergencySeparationBehaviour`, `AlignmentBehaviour`, `CohesionBehaviour`, `EvadeBehaviour`, `WanderBehaviour`, `ArriveBehaviour`, `ObstacleAvoidanceBehaviour`, `PlayAreaContainmentBehaviour`, `InterestAvoidanceBehaviour`, `InterestSpacingBehaviour`, `BoidInteraction`, `BoidInterest`, `InterestLife`, `BoidLife`, `BoidActivity`, `BoidHitReaction`, `BoidRespawn`, `BoidRespawnArea`, `BoidAnimation`, `BoidGizmos` y `BoidThreat`.

### Integración del Parcial 1

La integración, pruebas y ajustes finales se realizaron de manera conjunta, incluyendo sesiones de _pair programming_. El historial de commits por sí solo no representa necesariamente las contribuciones individuales.

### Parcial 2 — Distribución de responsabilidades

**Tomás Molina — Predator / Wolves.** Responsable exclusivo del desarrollo del subsistema Predator: los cuatro Wolves, su lógica de estados, patrullaje, percepción y comportamientos de búsqueda y persecución. El comportamiento de estos agentes se integró posteriormente con el resto de los sistemas del escenario.

**Lucas González — Resto de sistemas del Parcial 2.** Responsable de los demás desarrollos de esta entrega, incluyendo navegación mediante grafo y A\* propio, adaptación del Hunter al escenario integrado, estados de búsqueda, recolección, huida, salida y reinicio, así como configuración y presentación de la escena final.

**Integración conjunta mediante pair programming.** Una vez desarrollados los subsistemas individuales, ambos integrantes trabajaron conjuntamente para integrarlos en la escena que contenía el grafo de navegación. Las sesiones de pair programming permitieron coordinar modificaciones, evitar conflictos por edición simultánea de los mismos archivos y verificar la interacción entre Hunter, Wolves, Astras, navegación y zonas funcionales. Los ajustes y pruebas de integración fueron compartidos.

Esta distribución distingue la responsabilidad principal de desarrollo del trabajo colaborativo de integración; el autor de un commit no determina por sí solo la autoría individual de cada decisión técnica.

## Repositorio

https://github.com/TomasMolinaGD/CAUGHT-GAME

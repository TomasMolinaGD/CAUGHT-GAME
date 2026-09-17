# CAUGHT-GAME

Simulación de agentes autónomos desarrollada en Unity 3D para el Parcial 1. El proyecto integra flocking, steering behaviours y una máquina de estados finita para representar un grupo de agentes Astra y un NPC cazador alienígena.

## Integrantes y responsabilidades

### Tomas Molina - NPC cazador Alien

Tomas desarrolló la base del alien, su máquina de estados y sus comportamientos de caza. Sus scripts son:

| Script | Responsabilidad |
| --- | --- |
| `FSMAgent.cs` | Coordina la FSM del cazador, conserva el objetivo actual y conecta movimiento, percepción, combate y animación. |
| `State.cs` | Define la clase base común de todos los estados. |
| `stateMachine.cs` | Registra estados, ejecuta el estado actual y controla exclusivamente las transiciones. |
| `PatrolState.cs` | Recorre los waypoints, detecta objetivos y permite generar puntos de interés. |
| `PursuitState.cs` | Persigue al mismo Astra hasta recuperar el ataque o alcanzar rango melee. |
| `AttackState.cs` | Decide entre ataque melee y ataque a distancia, ejecuta el golpe y respeta el TBA. |
| `GatherState.cs` | Se acerca a un Astra eliminado, espera el tiempo de recolección y lo recoge. |
| `HunterMovement.cs` | Mueve y rota al alien, lo mantiene en el suelo y dentro del área jugable. |
| `HunterPerception.cs` | Detecta Astras vivos o eliminados dentro de radios limitados y con línea de visión. |
| `HunterCombat.cs` | Administra daño, rangos, cooldown, proyectiles y la continuación melee después del disparo. |
| `HunterProjectile.cs` | Crea y desplaza el proyectil hasta el objetivo y aplica el daño al impactar. |
| `HunterAnimationController.cs` | Controla las animaciones de correr y atacar y espera que el ataque visual termine. |
| `AlienVisualGrounding.cs` | Compensa los offsets de la animación para mantener el modelo apoyado en el suelo. |
| `HunterInterestSpawner.cs` | Genera puntos de interés en posiciones válidas, con un máximo de cinco activos. |
| `HunterDebugFeedback.cs` | Muestra estado, acción, objetivo, detecciones, TBA y transiciones de la FSM. |
| `HunterCameraFollow.cs` | Mantiene la cámara siguiendo al cazador desde una vista elevada. |

### Lucas González - agentes Astra y Flocking

Lucas desarrolló los agentes autónomos Astra, sus sensores, ciclo de vida y steering behaviours. Sus scripts son:

| Script | Responsabilidad |
| --- | --- |
| `BoidAgent.cs` | Integra las fuerzas de steering, mueve cada Astra de forma individual y resuelve colisiones. |
| `BoidSensor.cs` | Obtiene vecinos, amenazas e intereses usando únicamente percepción local. |
| `SteeringBehaviour.cs` | Clase base para los comportamientos de steering, con peso y prioridad configurables. |
| `SeparationBehaviour.cs` | Aleja al Astra de vecinos cercanos para evitar superposiciones. |
| `EmergencySeparationBehaviour.cs` | Aplica una separación urgente cuando dos agentes están demasiado juntos. |
| `AlignmentBehaviour.cs` | Ajusta la dirección del Astra según el movimiento de sus vecinos. |
| `CohesionBehaviour.cs` | Lleva al agente hacia el centro local del grupo sin usar información global. |
| `EvadeBehaviour.cs` | Hace que el Astra escape del alien con prioridad sobre el flocking. |
| `WanderBehaviour.cs` | Genera recorridos autónomos variables dentro del nivel. |
| `ArriveBehaviour.cs` | Acerca suavemente al Astra al punto de interés sin sobrepasarlo. |
| `ObstacleAvoidanceBehaviour.cs` | Detecta obstáculos por delante y elige una dirección segura para rodearlos. |
| `PlayAreaContainmentBehaviour.cs` | Mantiene al agente dentro del perímetro permitido. |
| `InterestAvoidanceBehaviour.cs` | Evita que los agentes atraviesen o se amontonen sobre el punto de interés. |
| `InterestSpacingBehaviour.cs` | Distribuye al grupo alrededor del objeto mientras interactúa con él. |
| `BoidInteraction.cs` | Reduce periódicamente la vida del punto de interés cuando está en alcance. |
| `BoidInterest.cs` | Identifica un objeto como punto de interés para los sensores. |
| `InterestLife.cs` | Administra la vida del punto de interés y lo destruye al llegar a cero. |
| `BoidLife.cs` | Administra vida y estados Alive, Dead y Respawning del Astra. |
| `BoidActivity.cs` | Activa o desactiva movimiento, sensores, animación, render y colisiones según su vida. |
| `BoidHitReaction.cs` | Detiene brevemente al Astra al recibir daño para hacer visible el impacto melee. |
| `BoidRespawn.cs` | Hace desaparecer al Astra recolectado y lo regenera después de una demora. |
| `BoidRespawnArea.cs` | Busca una posición aleatoria válida dentro de la zona de respawn. |
| `BoidAnimation.cs` | Sincroniza la animación de caminar con la velocidad real del agente. |
| `BoidGizmos.cs` | Dibuja radios de percepción y vínculos de detección para depuración. |
| `BoidThreat.cs` | Identifica al cazador como amenaza detectable por los sensores Astra. |

### Integración conjunta

Después de completar cada parte por separado, se realizó el merge de ambos sistemas. La integración, las pruebas y los ajustes finales se hicieron en conjunto por Discord, compartiendo pantalla y trabajando mediante pair programming. Por ese motivo el historial puede mostrar una mayor cantidad de commits a nombre de Lucas, aunque las decisiones y correcciones de integración fueron realizadas por ambos integrantes.

`BuildExteriorPrototype.cs` reúne el escenario, los agentes, los waypoints, los perímetros, los puntos de interés y la configuración de cámara. `AstraAnimationImportSettings.cs` mantiene una importación consistente de las animaciones Astra.

## Funcionalidades principales

- Seis agentes Astra autónomos sin líder ni controlador global.
- Separation, Alignment y Cohesion con percepción local.
- Evade prioritario al detectar al cazador.
- Arrive e interacción gradual con puntos de interés.
- Prevención de superposición, obstáculos y salida del perímetro.
- Vida, muerte, recolección y respawn aleatorio.
- Alien controlado por FSM con Patrol, Pursuit, Attack y Gather.
- Ataques ranged y melee regulados por TBA.
- Panel de feedback con estado, objetivo, detecciones y acciones.

## Ejecución

- Versión requerida: Unity `6000.3.17f1`.
- Escena principal: `Assets/Scenes/LastSave_ExteriorPrototype.unity`.
- La escena ya está configurada como primera escena de Build Settings.
- Durante Play, `F1` muestra u oculta el panel de diagnóstico.

## Repositorio

https://github.com/TomasMolinaGD/CAUGHT-GAME

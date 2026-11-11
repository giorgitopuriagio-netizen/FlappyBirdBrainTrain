# How the project works

This project teaches Flappy Bird agents to survive by evolving the weights of a neural network. A human can watch a saved agent or compete against it. Training, evaluation, and human play use the same simulation and collision rules.

## Scene flow

| Scene | Responsibility |
| --- | --- |
| MainMenu | Start new training, continue the latest checkpoint, or open evaluation. |
| Training | Run the population, adjust simulation speed, pause, stop/save, and open evaluation. |
| AITest | Select a generation brain and launch Watch bot or Human vs bot directly. |

SceneEntry creates the appropriate screen controller. AppServices supplies the repository and carries the active TrainingSession across scene transitions. It does not hold global bird-death counters or a shared target list.

Evaluation has one brain dropdown containing all archived runs. When generation numbers repeat across runs, options include a run number; run 1 is the newest archived run. Changing the brain or course requires stopping the active round. The two mode buttons stay available and create a fresh round immediately, including when restarting the same mode.

## Round state and input

EvaluationSession owns one complete round. Its mode cannot change independently of its world, pilots, and action array.

| Mode | Birds | Initial state |
| --- | --- | --- |
| Watch bot | One neural-network-controlled bird | Running |
| Human vs bot | The bot and one human-controlled bird | WaitingForHuman |

In competition, neither bird nor the course advances until the first human flap. Space, tapping/clicking the field, and Flap / Space queue the same action. The first press starts both birds and flaps the human. Later presses each produce one flap; holding a key does not repeatedly flap.

The pointing hand and Get Ready graphic appear only before the first flap of a competition round. The hand disappears once the round starts. Scores use separate numeral sprites, so it never appears inside a number such as 14.

Backgrounding pauses an active round. Watch bot resumes on return. Competition waits for Space or a field tap to resume; this also works if the human has crashed and only the bot is still flying. The initial hand hint is not shown for an ordinary mid-round pause.

A round completes naturally when all birds crash. In competition, the longer survivor wins; equal survival ticks produce a draw. Stop round / Escape interrupts play without declaring a winner. A mode-button click creates a new round and resets time, queued input, course, and birds together.

## Fixed-step simulation

FlappyWorld contains game state. Controllers supply one boolean flap action per bird. Every step is exactly 1/60 second:

1. Living bots read the sensors and decide whether to flap.
2. Pipes move left; off-screen pipes are removed and new ones are generated ahead.
3. A flap replaces vertical velocity with the configured upward velocity.
4. Gravity changes velocity, terminal fall speed is applied, and vertical position advances.
5. Circle-versus-rectangle checks detect pipes; separate bounds detect the ground and ceiling.
6. Each collision kills a bird once. Living birds receive one point per fully cleared pipe.

Rendering displays state but does not decide collisions or fitness. Dead birds can visually fall to the ground in evaluation without changing their recorded survival result.

| Rule | Value |
| --- | --- |
| Field | 288 by 512 pixels |
| Ground height | 112 pixels |
| Bird horizontal position | 72 pixels |
| Bird collision radius | 10 pixels |
| Gravity | 900 pixels per second squared |
| Flap velocity | 270 pixels per second upward |
| Maximum fall speed | 480 pixels per second downward |
| Pipe speed | 120 pixels per second |
| Pipe width / gap / spacing | 52 / 100 / 168 pixels |

These rules recreate familiar Flappy Bird behavior; they do not claim binary equivalence to the closed original game. Training acceleration runs more identical steps rather than changing physics or decision frequency within simulated time.

## Neural-network inputs

NeuralPilot reads six normalized sensors before each step. Let y be bird height, v vertical velocity, x the next pipe's left edge, and c the gap center. The playable height is 512 - 112 = 400 pixels.

| Input | Calculation | Meaning |
| --- | --- | --- |
| 0 | 2 * (y - 112) / 400 - 1 | Height above the ground |
| 1 | v / 480 | Direction and speed of vertical movement |
| 2 | (x - 72) / 288 | Horizontal distance to the next pipe |
| 3 | (c - y) / 400 | Signed distance to the gap center |
| 4 | (c + 50 - y) / 400 | Signed distance to the top of the gap |
| 5 | (c - 50 - y) / 400 | Signed distance to the bottom of the gap |

Layers are 6 -> 12 -> 8 -> 1. Hidden neurons use tanh; the output uses sigmoid. Each non-input neuron has a trainable bias. All weights and biases form a flat genome of 197 genes. An output greater than 0.5 requests a flap. Feed-forward evaluation neither accumulates signals between steps nor modifies the genome.

## Evolution

New training initializes 200 random genomes. Birds start together and face the same pipes. Course randomness is independent of genetic randomness.

Fitness primarily measures survived simulation ticks. Ties use pipe count, distance to the gap center at death, and a stable individual index. Simultaneous crashes therefore have an explicit deterministic tie rule.

When all birds are dead, the session archives the winner and creates the next population:

- Four elites are copied unchanged.
- Tournament selection samples four candidates per parent choice and selects the highest-ranked one.
- Uniform crossover chooses each weight or bias from either parent.
- Each non-elite gene has an 8% mutation probability, with Gaussian perturbation strength 0.3; evolved values are bounded.
- Five percent of the population consists of newly initialized random immigrants.

Deep copies prevent mutation from changing parents or elites. The next generation receives a new course. Performance can vary between courses and is not guaranteed to improve every generation.

There is no automatic overall stop, generation cap, or timeout for a successful survivor. The user ends training with Stop and save. The last living bird is archived immediately, even if it keeps flying indefinitely. Stopping while several birds remain checkpoints them without fabricating a winner.

## Exact saving and continuation

ITrainingRepository separates application logic from storage. JsonTrainingRepository writes to Application.persistentDataPath/Training. Each run has its own checkpoint and generation files; latest.txt points to the run restored by Continue training.

A checkpoint contains the full population, settings, generation, birds, pipes, simulation tick, pipe IDs, course seed, and random-generator states. Pilots are rebuilt without consuming randomness. Resumed simulation and later evolution can therefore match uninterrupted execution.

Readable JSON is accompanied by Base64-encoded IEEE-754 double bits. Unity's native JSON parser can otherwise change some doubles by one ULP. TrainingJson restores exact numeric values before validating the reconstructed session.

Writes flush a temporary file and replace the target while retaining a backup. The loader tries the backup if the checkpoint is unreadable. Invalid versions, genome shapes, incompatible populations, malformed worlds, and invalid numeric values are rejected. Unreadable generation records are skipped and counted in the evaluation screen.

Saves occur every 30 real seconds, when a sole survivor becomes available, after a generation ends, on pause/backgrounding, on application exit, and before leaving training. Failed saving pauses training and prevents the requested navigation until saving succeeds.

Existing checkpoints and generation archives retain their format and remain compatible with the English interface and direct mode buttons. The interface changes do not retrain or alter saved brains.

## Source-code responsibilities

| Location | Responsibilities |
| --- | --- |
| Assets/Scripts/Core | World simulation, genomes, neural network, genetic algorithm, training, evaluation sessions, deterministic randomness; no Unity dependency. |
| Assets/Scripts/Persistence | Repository interface, validated JSON, atomic writes, and exact-number encoding. |
| Assets/Scripts/Presentation | Screens, shared UI creation, rendering, input, and scene navigation. |
| Assets/Editor | Commands to open the main menu and build Windows. |
| Assets/Tests | Unity persistence and scene-flow regression checks. |
| Tests/CoreChecks | Standalone checks of physics, networks, evolution, continuation, and evaluation state transitions. |

BoardView samples the original pixel atlas with UV rectangles. Digit 1 and the pointing hand have separate rectangles and lifecycles. Input and rendering are separate from simulation so bot and human controllers share physical rules.

## Verification and building

Run the core-check commands in README.md. Unity Test Runner provides Edit Mode and Play Mode checks. Evaluation regression tests exercise direct mode launches, repeated switching, restarting after completion, English controls, the first-flap gate, the hand hint, and numeric score rendering. Tests use isolated save directories and do not alter the user's archive.

Use Flappy Bird > Build Windows to produce Builds/Windows/FlappyBird.exe. Keep the executable alongside its data directory, UnityPlayer.dll, and runtime files.

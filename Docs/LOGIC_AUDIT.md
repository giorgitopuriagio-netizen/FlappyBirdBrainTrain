# Logic audit and fixes

The original architecture was replaced with the user's approval. The learning approach remains a feed-forward neural network evolved by a genetic algorithm.

## Original model and learning defects

| Original issue | Effect | Correction |
| --- | --- | --- |
| Neurons, networks, and the genetic algorithm inherited MonoBehaviour but were constructed with new. | Unity lifecycles were mixed with plain data objects and constructors. | Core models are ordinary C# classes without Unity dependencies. |
| Large unnormalized distances passed through three 40-neuron sigmoid layers without biases. | Saturation made inputs ineffective; the search was unnecessarily large and could not independently shift thresholds. | Normalized sensors, trainable biases, a 6-12-8-1 network, and tanh hidden layers. |
| Vertical velocity was missing. | Rising and falling at the same position looked identical despite needing different decisions. | Velocity and signed offsets to the gap are explicit sensors. |
| Each bird constructed an unnecessary random network before its real network. | Wasted work and consumed shared randomness even while restoring inherited weights. | One pilot per genome; separate, explicitly saved random-generator states. |
| The middle-parent branch tested i < 3 after rejecting i < 5. | The intended middle selection group was unreachable. | Bounded tournament selection using explicit fitness. |
| Ten unique parents were searched for in narrow percentile bands. | Small populations could hang or produce invalid indices. | Validated settings and bounded tournaments, supporting populations of two or more birds. |
| Crossover used the first neuron's input count as the chromosome length. | Most genes came from one parent; splits were restricted to the beginning. | Uniform crossover covers every weight and bias. |
| Parents and offspring shared array references. | Genome independence was not guaranteed against later mutation. | Deep copies of genomes, with parent and elite immutability checks. |
| Mutation replaced a whole neuron's weights, used an off-by-one probability, and adapted to exact absolute-time equality. | Large destructive changes and frame-dependent mutation decisions. | Small Gaussian changes per gene with a strict probability comparison; elites do not mutate. |
| Fitness was represented by death callback order and absolute Unity time. | Simultaneous deaths lacked an explicit tie rule. | Survival ticks, then score, gap-center distance, and a stable index for complete equality. |
| Death handling had no alive guard. | Multiple callbacks before deferred destruction could count a bird twice and finish a generation early. | Each living bird transitions to dead only once during a simulation step. |
| Birds removed pipes from a shared static list and updated a global score. | Reading a target affected other agents; personal scores were unavailable. | The world owns pipe movement/removal, sensors are read-only, and scores belong to each bird. |
| Decisions, transform movement, and Rigidbody updates ran on separate schedules. | Decisions and collision timing depended on frame rate and update order. | One fixed 1/60-second step for decisions, motion, collisions, and scoring. |
| Death bounds lay outside the camera view and obstacle shapes differed from classic Flappy Bird. | Visible gameplay and training conditions were inconsistent. | A shared field, ground, ceiling, pipe pairs, dimensions, and atlas in every mode. |
| SaveToJson wrote locale-dependent comma-and-pipe-delimited text with a .json extension. | Files were not JSON and had no schema; the disabled loader targeted a hard-coded generation. | Versioned JSON, explicit validation, and a working archive loader. |
| Only a winning weight set was saved. | Population, world, and randomness could not be resumed. | Full checkpoints plus individual generation archives. |
| Generation changes reloaded scene index 0 and depended on static counters. | Adding a main menu broke that transition; independent game modes were missing. | Named scenes, explicit sessions, user-controlled stopping, and checkpoint restoration. |
| The atlas named .png contained WebP data. | Unity reported Unsupported image type: WebP and could not import it. | Re-encoded as true PNG and verified identical decoded RGBA pixels. |

## Evaluation and presentation fixes

The reported IndexOutOfRangeException occurred after a bot-only round when selecting Human vs bot. The mode flag changed while the displayed world still contained one bird. The next statistics update read bird index 1.

An EvaluationSession now owns its immutable mode, matching bird population, action array, pilot, and round state. Clicking a mode button constructs the complete replacement before exposing it to the interface. The UI reads Bot and optional Human from that same session. A separate mode flag cannot disagree with the world.

Regression checks cover switching modes while running, restarting the same mode, switching after a manual stop or natural completion, waiting before the first human flap, and rendering statistics on subsequent frames.

The score's digit 1 previously used the rectangle for a pointing hand. It now uses the real numeral at pixel rectangle (136, 39, 8, 18). Numeric spacing respects each glyph's width. The hand has its own view and appears only while waiting to start Human vs bot.

Evaluation now has one generation-brain list and direct Watch bot / Human vs bot launch buttons. The training-run selector, separate Start/Repeat button, and training-navigation shortcut were removed. Interface text, errors, editor commands, and documentation are in English.

## Exact continuation

Testing Unity's JsonUtility revealed a one-ULP change for some doubles when parsing JSON. Saves carry readable numbers plus a Base64 payload of their IEEE-754 bits. Loading restores exact weights, evolution parameters, positions, and velocities before validation. Tests compare future decisions and generation transitions with uninterrupted execution and replay an archived winner on its recorded course.

## Survivor handling and cleanup

Training has no automatic overall stop. When only one bird remains, its brain becomes available for evaluation without killing it or ending its generation. Once it dies, the final result is archived and the next generation starts. Stopping earlier checkpoints the current world and population; a winner is not invented while several birds remain alive.

All agents within a generation share one course; later generations receive different courses. Human and bot share a course in competition. New-course evaluation helps reveal overfitting; individual generations are not guaranteed to improve monotonically.

Before the original cleanup, retained assets/settings were checked for references to obsolete files. Original assets and scripts, including the previously modified Flappy.cs, were backed up to %TEMP%/FlappyBirdBrainTrain-before-refactor-20261111.zip. Existing editor, package, and Unity-upgrade changes outside the refactoring scope were preserved.

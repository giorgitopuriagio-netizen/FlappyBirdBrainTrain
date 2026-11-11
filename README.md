# Flappy Bird Brain Train

A Unity **6000.3.8f1** project that trains Flappy Bird agents with a neural network and a genetic algorithm.

Open [MainMenu.unity](Assets/Scenes/MainMenu.unity) and press **Play**, or use **Flappy Bird > Open Main Menu** in the editor.

## Main menu

- **New training** creates a separate run with 200 birds. Earlier runs remain in the archive.
- **Continue training** restores the latest population, generation, birds, pipes, and random-generator states.
- **AI evaluation / Human vs bot** opens the saved-brain evaluation scene.

Training has no generation, score, or survival-time limit. Use **Stop and save** to end the session. An unfinished generation resumes from that exact checkpoint. Pause, backgrounding, application exit, generation completion, and a 30-second timer also save progress.

## Evaluate a brain

1. Select a saved brain in **Generation brain**. One list includes all archived runs; a run number distinguishes duplicate generation numbers when necessary.
2. Optionally select a new course or the selected generation's recorded course.
3. Click **Watch bot** to immediately start bot-only play, or **Human vs bot** to create a two-bird round.
4. In Human vs bot, press **Space**, tap the field, or click **Flap / Space** to begin together. Each press triggers one flap.

The mode buttons also restart or switch the current round. There is no separate Start/Repeat button, training-run selector, or shortcut back to training on this screen. Use **Main menu** to return.

Yellow is the bot; blue is the human. Both use the same physics and course and do not collide with each other. If one crashes, the other continues. The longer survivor wins; crashing on the same simulation tick is a draw. **Stop round** or **Escape** interrupts play without declaring a winner.

The pointing-hand hint appears only before the first flap of a Human vs bot round. Scores use numeric sprites, including the actual digit 1.

## Saved training

Files live under Application.persistentDataPath/Training:

| File | Purpose |
| --- | --- |
| RUN_ID/checkpoint.json | Full state of the current training session |
| RUN_ID/checkpoint.json.bak | Previous successful checkpoint |
| RUN_ID/generation-00000001.json | Brain and result of a generation's last survivor |
| latest.txt | Run to load with Continue training |

The last living bird is archived as soon as it is the only survivor. Its generation keeps running; stopping updates its snapshot. Once it dies, the archive is updated with the completed result. When several birds remain alive, no winner is invented.

Writes use temporary files, atomic replacement, and backups. A failed save pauses training and blocks navigation away from it. JSON includes exact numeric bits alongside readable values, avoiding rounding changes when Unity parses doubles.

Earlier text files named Bot N Weights.json are not compatible checkpoints. They are left in the user's data directory.

## Architecture and documentation

- [Project guide](Docs/PROJECT_GUIDE.md): scene flow, simulation, sensors, network, evolution, persistence, and code responsibilities.
- [Logic audit](Docs/LOGIC_AUDIT.md): original defects and fixes, including the evaluation-mode exception and score-sprite correction.

Scenes: MainMenu, Training, AITest. The main menu is first in Build Settings. Core simulation and AI logic are plain C# without Unity dependencies; presentation and persistence are separate layers.

The original project atlas is retained at Assets/Resources/FlappyAtlas.png. Its original .png file contained WebP and was converted to a true PNG with identical decoded RGBA pixels. Old bee artwork, unused prefabs, the sample scene, and unused scripts were removed. Attribution and rights remain those of the original asset.

## Checks and builds

Core checks require .NET SDK 10; the Unity game itself does not:

    dotnet run --project Tests/CoreChecks/CoreChecks.csproj --configuration Release
    dotnet run --project Tests/CoreChecks/CoreChecks.csproj --configuration Release -- --training-smoke

Unity Test Runner includes Edit Mode persistence checks and Play Mode scene-flow, direct mode-switching, and UI regression checks. Tests use isolated temporary saves, not the user's training. Preview images are written to TestResults/Previews.

**Flappy Bird > Build Windows** writes the playable build to Builds/Windows/FlappyBird.exe.

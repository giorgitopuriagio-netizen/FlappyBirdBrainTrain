using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FlappyBird.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlappyBird.Presentation
{
    public sealed class AITestScreen : MonoBehaviour
    {
        private BoardView board;
        private TMP_Dropdown generations;
        private Button watchButton;
        private Button versusButton;
        private Button courseButton;
        private Button flapButton;
        private Button stopButton;
        private TMP_Text courseLabel;
        private TMP_Text stats;
        private TMP_Text message;
        private IReadOnlyList<GenerationChampion> choices = Array.Empty<GenerationChampion>();
        private bool recordedCourse;
        private double accumulator;
        private GenerationChampion selected;

        public EvaluationSession CurrentRound { get; private set; }

        private void Start()
        {
            var ui = new UIFactory();
            ui.Header("AI evaluation");
            board = BoardView.Create(ui);
            board.Pressed = QueueFlap;
            ui.Label(ui.Root, "Choose your challenger", 560, 152, 632, 44, 32, bold: true);
            ui.Label(ui.Root, "Saved generation", 560, 212, 632, 28, 18, UIFactory.Muted, bold: true);
            generations = ui.Dropdown(ui.Root, 560, 250, 632, SelectGeneration);
            ui.Label(ui.Root, "Choose a mode to start a fresh round", 560, 338, 632, 28, 18, UIFactory.Muted);
            watchButton = ui.Button(ui.Root, "Watch bot", 560, 380, 300, 64,
                () => BeginRound(EvaluationMode.WatchBot));
            versusButton = ui.Button(ui.Root, "Human vs bot", 884, 380, 308, 64,
                () => BeginRound(EvaluationMode.HumanVsBot));
            courseButton = ui.Button(ui.Root, "Course: new", 560, 468, 632, 54, () =>
            {
                recordedCourse = !recordedCourse;
                courseLabel.text = recordedCourse ? "Course: selected generation" : "Course: new";
            }, false, 20);
            courseLabel = courseButton.GetComponentInChildren<TMP_Text>();
            flapButton = ui.Button(ui.Root, "Flap / Space", 560, 548, 300, 56, QueueFlap);
            stopButton = ui.Button(ui.Root, "Stop round", 884, 548, 308, 56, StopRound);
            stats = ui.Label(ui.Root, "", 560, 636, 632, 78, 28, bold: true);
            message = ui.Label(ui.Root, "", 560, 728, 632, 90, 19, UIFactory.Muted);
            ui.Button(ui.Root, "Main menu", 560, 832, 632, 44, AppServices.MainMenu, false, 20);
            ui.Label(ui.Root, "Yellow: bot  |  Blue: you  |  Space or tap to flap",
                56, 918, 1168, 28, 18, UIFactory.Muted);
            LoadArchive();
            RefreshControls();
        }

        private void LoadArchive()
        {
            try
            {
                choices = AppServices.Repository.ListChampions(out int unreadable);
                var runIds = choices.Select(champion => champion.runId).Distinct().ToList();
                generations.ClearOptions();
                generations.AddOptions(choices.Count == 0
                    ? new List<string> { "No saved brains yet" }
                    : choices.Select(champion => "Generation " + champion.generation +
                        (runIds.Count > 1 ? " (run " + (runIds.IndexOf(champion.runId) + 1) + ")" : "") +
                        "  ·  " + champion.score + " pipes  ·  " + (champion.generationCompleted
                            ? (champion.survivalTicks * GameRules.Step).ToString("F1", CultureInfo.InvariantCulture) + " s"
                            : "last survivor")).ToList());
                generations.SetValueWithoutNotify(0);
                SelectGeneration(0);
                if (unreadable > 0) message.text += "\nUnreadable records skipped: " + unreadable + ".";
            }
            catch (Exception error)
            {
                choices = Array.Empty<GenerationChampion>();
                selected = null;
                stats.text = "Archive unavailable";
                message.text = "Could not read the archive: " + error.Message;
                Debug.LogException(error);
            }
        }

        private void SelectGeneration(int index)
        {
            if (CurrentRound != null && CurrentRound.IsActive) return;
            selected = index >= 0 && index < choices.Count ? choices[index] : null;
            CurrentRound = null;
            accumulator = 0;
            board.SetReady(false);
            board.SetGameOver(false);
            if (selected == null)
            {
                stats.text = "No saved brains";
                message.text = "Start New training from the main menu. Saved birds appear here when a generation has one survivor or finishes.";
            }
            else
            {
                stats.text = (selected.generationCompleted ? "Generation winner: " : "Last survivor: generation ") + selected.generation;
                message.text = "Choose a mode to start. Both birds face the same pipes." +
                    (!selected.generationCompleted ? "\nSaved while its training generation was still running." : "") +
                    (selected.tiedSurvivors > 1 ? "\nThe final birds tied; the training tie rule selected this winner." : "");
            }
            RefreshControls();
        }

        private void BeginRound(EvaluationMode mode)
        {
            if (selected == null) return;
            try
            {
                uint seed = recordedCourse ? selected.courseSeed : unchecked((uint)DateTime.UtcNow.Ticks);
                // Construct the entire replacement before changing the displayed round.
                var replacement = new EvaluationSession(selected.brain, seed, mode);
                CurrentRound = replacement;
                accumulator = 0;
                board.SetReady(replacement.State == EvaluationState.WaitingForHuman);
                board.SetGameOver(false);
                message.text = replacement.HasHuman
                    ? "Press Space or tap the field to start together with the bot."
                    : "The bot is playing. Click a mode button to restart or switch modes.";
                RefreshControls();
            }
            catch (Exception error)
            {
                message.text = "Could not start the round: " + error.Message;
                Debug.LogException(error);
            }
        }

        private void RefreshControls()
        {
            bool active = CurrentRound != null && CurrentRound.IsActive;
            generations.interactable = !active && choices.Count > 0;
            courseButton.interactable = !active && selected != null;
            watchButton.interactable = versusButton.interactable = selected != null;
            stopButton.interactable = active;
            flapButton.interactable = active && CurrentRound.HasHuman &&
                (CurrentRound.Human.alive || CurrentRound.State == EvaluationState.Paused);
            SetModeColor(watchButton, CurrentRound != null && !CurrentRound.HasHuman);
            SetModeColor(versusButton, CurrentRound != null && CurrentRound.HasHuman);
        }

        private static void SetModeColor(Button button, bool selectedMode)
        {
            UIFactory.Emphasize(button, selectedMode);
        }

        private void QueueFlap()
        {
            if (CurrentRound == null || !CurrentRound.IsActive || !CurrentRound.HasHuman) return;
            bool resuming = CurrentRound.State != EvaluationState.Running;
            CurrentRound.RequestFlap();
            if (resuming)
            {
                accumulator = 0;
                board.SetReady(false);
                message.text = "Press Space or tap the field once for each flap.";
            }
        }

        private void Update()
        {
            if (board == null) return;
            if (Input.GetKeyDown(KeyCode.Space)) QueueFlap();
            if (Input.GetKeyDown(KeyCode.Escape)) StopRound();
            var round = CurrentRound;
            if (round != null && round.State == EvaluationState.Running)
            {
                accumulator += Math.Min(0.25, Time.unscaledDeltaTime);
                while (round.State == EvaluationState.Running && accumulator >= GameRules.Step)
                {
                    accumulator -= GameRules.Step;
                    round.Tick();
                }
                if (round.State == EvaluationState.Completed) FinishRound();
            }
            board.Render(round?.World, round != null && round.HasHuman);
            if (round != null)
            {
                stats.text = "Bot: " + round.Bot.score + (round.Bot.alive ? "" : " (out)") +
                    (round.HasHuman ? "\nHuman: " + round.Human.score + (round.Human.alive ? "" : " (out)") : "");
                if (round.State == EvaluationState.Running && round.HasHuman)
                {
                    if (!round.Human.alive) message.text = "You are out. The bot is still flying.\nWait for the result or stop the round.";
                    else if (!round.Bot.alive) message.text = "The bot is out. Keep going!";
                }
            }
            RefreshControls();
        }

        private void StopRound()
        {
            if (CurrentRound == null || !CurrentRound.IsActive) return;
            CurrentRound.Stop();
            FinishRound();
        }

        private void FinishRound()
        {
            accumulator = 0;
            board.SetReady(false);
            board.SetGameOver(true);
            if (CurrentRound.State == EvaluationState.Stopped)
                message.text = "Round stopped. Select another brain or click a mode button to play again.";
            else if (!CurrentRound.HasHuman)
                message.text = "Round complete. The bot passed " + CurrentRound.Bot.score + " pipes.";
            else
            {
                int difference = CurrentRound.Human.survivalTicks.CompareTo(CurrentRound.Bot.survivalTicks);
                message.text = difference > 0 ? "You survived longer than the bot!" : difference < 0
                    ? "The bot survived longer. Try again!" : "Draw: both birds crashed on the same tick.";
            }
            RefreshControls();
        }

        private void OnApplicationPause(bool value)
        {
            if (CurrentRound == null || !CurrentRound.IsActive) return;
            if (value)
            {
                CurrentRound.Pause();
                accumulator = 0;
                board.SetReady(CurrentRound.State == EvaluationState.WaitingForHuman);
                message.text = CurrentRound.HasHuman ? "Round paused. Press Space or tap the field to resume." : "Round paused.";
            }
            else if (!CurrentRound.HasHuman)
            {
                CurrentRound.Resume();
                message.text = "The bot is playing.";
            }
        }
    }
}

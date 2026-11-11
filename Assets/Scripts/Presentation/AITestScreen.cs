using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FlappyBird.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FlappyBird.Presentation
{
    public sealed class AITestScreen : MonoBehaviour
    {
        private BoardView board;
        private Dropdown generations;
        private Button watchButton;
        private Button versusButton;
        private Button courseButton;
        private Button flapButton;
        private Button stopButton;
        private Text courseLabel;
        private Text stats;
        private Text message;
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
            ui.Label(ui.Root, "Generation brain", 550, 110, 620, 32, 21, UIFactory.Muted);
            generations = ui.Dropdown(ui.Root, 550, 155, 620, SelectGeneration);
            watchButton = ui.Button(ui.Root, "Watch bot", 550, 245, 295, 65,
                () => BeginRound(EvaluationMode.WatchBot));
            versusButton = ui.Button(ui.Root, "Human vs bot", 865, 245, 305, 65,
                () => BeginRound(EvaluationMode.HumanVsBot));
            courseButton = ui.Button(ui.Root, "Course: new", 550, 340, 620, 54, () =>
            {
                recordedCourse = !recordedCourse;
                courseLabel.text = recordedCourse ? "Course: selected generation" : "Course: new";
            }, false, 21);
            courseLabel = courseButton.GetComponentInChildren<Text>();
            flapButton = ui.Button(ui.Root, "Flap / Space", 550, 425, 295, 56, QueueFlap);
            stopButton = ui.Button(ui.Root, "Stop round", 865, 425, 305, 56, StopRound);
            stats = ui.Label(ui.Root, "", 550, 525, 620, 100, 27);
            message = ui.Label(ui.Root, "", 550, 650, 620, 130, 21, UIFactory.Muted);
            ui.Button(ui.Root, "Main menu", 550, 815, 620, 48, AppServices.MainMenu, false, 21);
            ui.Label(ui.Root, "Yellow: bot. Blue: human. Press Space or tap the field to flap.",
                60, 855, 460, 40, 17, UIFactory.Muted);
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
                message.text = "Train birds from the main menu first. A brain is saved when one bird remains and when its generation ends.";
            }
            else
            {
                stats.text = (selected.generationCompleted ? "Generation winner: " : "Last survivor: generation ") + selected.generation;
                message.text = "Click Watch bot or Human vs bot to start the selected mode." +
                    (!selected.generationCompleted ? "\nThis is a living survivor; its training generation is still in progress." : "") +
                    (selected.tiedSurvivors > 1 ? "\nSeveral birds crashed on the final tick; the recorded winner was selected by the tie rule." : "");
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
            button.GetComponent<Image>().color = selectedMode ? UIFactory.Green : UIFactory.Cream;
            button.GetComponentInChildren<Text>().color = selectedMode ? Color.white : UIFactory.Ink;
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

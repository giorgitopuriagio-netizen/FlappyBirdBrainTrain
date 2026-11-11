using System;
using System.Diagnostics;
using FlappyBird.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace FlappyBird.Presentation
{
    public sealed class TrainingScreen : MonoBehaviour
    {
        private TrainingSession session;
        private BoardView board;
        private TMP_Text statistics;
        private TMP_Text message;
        private TMP_Text pauseLabel;
        private Button pauseButton;
        private Button stopButton;
        private Button testButton;
        private GenerationChampion pendingChampion;
        private int lastChampionGeneration;
        private int lastChampionScore;
        private int savedLivingGeneration;
        private static readonly int[] Speeds = { 1, 5, 10, 30 };
        private readonly Button[] speedButtons = new Button[Speeds.Length];
        private TMP_Text generationValue;
        private TMP_Text aliveValue;
        private TMP_Text scoreValue;
        private TMP_Text bestValue;
        private int speed = 1;
        private bool paused;
        private bool stopped;
        private double accumulator;
        private float nextAutosave;
        private readonly Stopwatch budget = new Stopwatch();

        private void Start()
        {
            var ui = new UIFactory();
            ui.Header("Training");
            board = BoardView.Create(ui);
            ui.Label(ui.Root, "Train a stronger bird", 560, 152, 632, 44, 32, bold: true);
            ui.Label(ui.Root, "One flock. The same pipes. A new chance each generation.",
                560, 204, 632, 48, 21, UIFactory.Muted);
            generationValue = ui.Metric("GENERATION", 560, 260, 300);
            aliveValue = ui.Metric("BIRDS ALIVE", 884, 260, 308);
            scoreValue = ui.Metric("CURRENT SCORE", 560, 368, 300);
            bestValue = ui.Metric("BEST SCORE", 884, 368, 308);
            statistics = ui.Label(ui.Root, "", 560, 480, 632, 56, 20, UIFactory.Muted);
            ui.Label(ui.Root, "Simulation speed", 560, 550, 632, 28, 18, UIFactory.Muted, bold: true);
            for (int i = 0; i < Speeds.Length; i++)
            {
                int value = Speeds[i];
                speedButtons[i] = ui.Button(ui.Root, "\u00d7" + value, 560 + i * 162, 588, 146, 48, () => speed = value);
            }
            pauseButton = ui.Button(ui.Root, "Pause", 560, 660, 300, 56, TogglePause);
            pauseLabel = pauseButton.GetComponentInChildren<TMP_Text>();
            stopButton = ui.Button(ui.Root, "Stop and save", 884, 660, 308, 56, StopTraining, true);
            ui.Button(ui.Root, "Main menu", 560, 736, 300, 50, () => { if (StopAndSave()) AppServices.MainMenu(); });
            testButton = ui.Button(ui.Root, "Evaluate AI", 884, 736, 308, 50, () => { if (StopAndSave()) AppServices.OpenTest(); });
            message = ui.Label(ui.Root, "", 560, 808, 632, 70, 18, UIFactory.Muted);
            ui.Label(ui.Root, "Every generation's last survivor joins your saved birds.", 56, 918, 1168, 28, 18, UIFactory.Muted);

            session = AppServices.Training;
            if (session == null)
            {
                stopped = true;
                statistics.text = "No active training.";
                message.text = "Return to the main menu to start a new run or continue your latest checkpoint.";
            }
            else message.text = "Stop and save whenever you're ready.\nProgress saves automatically every 30 seconds and after each generation.";
            nextAutosave = Time.unscaledTime + 30;
            Refresh();
        }

        private void TogglePause()
        {
            if (paused)
            {
                if (!Save()) return;
                paused = false;
            }
            else
            {
                paused = true;
                Save();
            }
            accumulator = 0;
            Refresh();
        }

        private void Update()
        {
            if (session == null) return;
            if (Input.GetKeyDown(KeyCode.Escape)) StopTraining();
            if (!stopped && !paused)
            {
                accumulator = Math.Min(2, accumulator + Math.Min(0.25, Time.unscaledDeltaTime) * speed);
                budget.Restart();
                while (accumulator >= GameRules.Step && budget.ElapsedMilliseconds < 14)
                {
                    accumulator -= GameRules.Step;
                    pendingChampion = session.Tick();
                    if (pendingChampion != null)
                    {
                        lastChampionGeneration = pendingChampion.generation;
                        lastChampionScore = pendingChampion.score;
                        if (!Save()) break;
                    }
                    else if (session.World.AliveCount == 1 && savedLivingGeneration != session.Generation)
                    {
                        if (!Save()) break;
                        savedLivingGeneration = session.Generation;
                    }
                }
                if (Time.unscaledTime >= nextAutosave)
                {
                    Save();
                    nextAutosave = Time.unscaledTime + 30;
                }
            }
            board.Render(session.World);
            Refresh();
        }

        private bool Save()
        {
            if (session == null) return true;
            try
            {
                AppServices.Repository.Save(session, pendingChampion);
                pendingChampion = null;
                return true;
            }
            catch (Exception error)
            {
                paused = true;
                message.text = "Could not save: " + error.Message + "\nResolve the issue and press Stop and save again.";
                Debug.LogException(error);
                return false;
            }
        }

        private void StopTraining() { StopAndSave(); }

        private bool StopAndSave()
        {
            if (!Save()) return false;
            stopped = true;
            accumulator = 0;
            if (session != null) message.text = "Progress saved. Continue training resumes from this moment.\nUse Evaluate AI to try a saved bird.";
            Refresh();
            return true;
        }

        private void Refresh()
        {
            if (session != null)
            {
                int currentScore = 0;
                foreach (var bird in session.World.Birds) currentScore = Math.Max(currentScore, bird.score);
                generationValue.text = session.Generation.ToString(System.Globalization.CultureInfo.InvariantCulture);
                aliveValue.text = session.World.AliveCount + " / " + session.PopulationSize;
                scoreValue.text = currentScore.ToString(System.Globalization.CultureInfo.InvariantCulture);
                bestValue.text = session.BestScore.ToString(System.Globalization.CultureInfo.InvariantCulture);
                statistics.text = (stopped ? "Saved" : paused ? "Paused" : "Training") + "  |  " +
                    (session.World.Ticks * GameRules.Step).ToString("F1", System.Globalization.CultureInfo.InvariantCulture) +
                    " s this generation" + (lastChampionGeneration > 0
                        ? "\nLast winner: generation " + lastChampionGeneration + " | " + lastChampionScore + " pipes" : "");
            }
            pauseButton.interactable = session != null && !stopped;
            stopButton.interactable = session != null && !stopped;
            testButton.interactable = session != null && (session.Generation > 1 || session.World.AliveCount == 1);
            pauseLabel.text = paused ? "Resume" : "Pause";
            for (int i = 0; i < speedButtons.Length; i++)
            {
                speedButtons[i].interactable = session != null && !stopped;
                UIFactory.Emphasize(speedButtons[i], speed == Speeds[i]);
            }
        }

        private void OnApplicationPause(bool value)
        {
            if (!value || session == null || stopped) return;
            paused = true;
            accumulator = 0;
            Save();
        }

        private void OnApplicationQuit() { if (session != null) Save(); }
    }
}

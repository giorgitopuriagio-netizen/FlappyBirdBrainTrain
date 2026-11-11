using System;
using System.IO;
using TMPro;
using UnityEngine;

namespace FlappyBird.Presentation
{
    public sealed class MainMenuScreen : MonoBehaviour
    {
        private BoardView board;
        private TMP_Text message;

        private void Start()
        {
            var ui = new UIFactory();
            ui.Header("Main menu");
            board = BoardView.Create(ui);
            ui.Label(ui.Root, "Classic flight. Evolving intelligence.", 56, 918, 1168, 28, 18, UIFactory.Muted);
            ui.Label(ui.Root, "NEURAL NETWORKS + EVOLUTION", 560, 160, 632, 28, 15, UIFactory.Green, bold: true);
            ui.Label(ui.Root, "Small bird.\nBig learning curve.", 560, 208, 632, 120, 46, bold: true);
            ui.Label(ui.Root, "Train a flock. Save each generation's best bird.\nWatch it fly, or challenge it yourself.",
                560, 350, 632, 86, 23, UIFactory.Muted);
            ui.Button(ui.Root, "New training", 560, 454, 632, 64, () => Try(AppServices.StartNewTraining), true);
            var resume = ui.Button(ui.Root, "Continue training", 560, 534, 632, 64, () => Try(AppServices.ContinueTraining));
            resume.interactable = File.Exists(Path.Combine(AppServices.Repository.RootPath, "latest.txt"));
            ui.Button(ui.Root, "Evaluate AI", 560, 614, 632, 64, AppServices.OpenTest);
            ui.Label(ui.Root, "Train at your own pace. Stop and save whenever you like.\nContinue picks up exactly where you left off.\nA new run keeps your previously saved birds.",
                560, 716, 632, 90, 20, UIFactory.Muted);
            message = ui.Label(ui.Root, resume.interactable ? "Checkpoint found. Your latest run is ready to continue." : "First visit? Start with New training.",
                560, 829, 632, 50, 18, UIFactory.Muted);
        }

        private void Try(Action action)
        {
            try { action(); }
            catch (Exception error)
            {
                message.color = new Color32(158, 60, 41, 255);
                message.text = "Could not open training: " + error.Message;
                Debug.LogException(error);
            }
        }

        private void Update() { if (board != null) board.Render(null); }
    }
}

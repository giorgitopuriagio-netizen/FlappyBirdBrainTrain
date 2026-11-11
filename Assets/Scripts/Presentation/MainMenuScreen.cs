using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace FlappyBird.Presentation
{
    public sealed class MainMenuScreen : MonoBehaviour
    {
        private BoardView board;
        private Text message;

        private void Start()
        {
            var ui = new UIFactory();
            ui.Header("Main menu");
            board = BoardView.Create(ui);
            ui.Label(ui.Root, "Classic pipes. One flap. One more try.", 60, 855, 1150, 30, 19, UIFactory.Muted);
            ui.Label(ui.Root, "PLAY AND EVOLVE", 550, 115, 660, 50, 34);
            ui.Label(ui.Root, "Train birds to pass the pipes, choose a generation's brain, and put it to the test.",
                550, 185, 610, 105, 25, UIFactory.Muted);
            ui.Button(ui.Root, "New training", 550, 310, 620, 68, () => Try(AppServices.StartNewTraining), true);
            var resume = ui.Button(ui.Root, "Continue training", 550, 396, 620, 68, () => Try(AppServices.ContinueTraining));
            resume.interactable = File.Exists(Path.Combine(AppServices.Repository.RootPath, "latest.txt"));
            ui.Button(ui.Root, "AI evaluation / Human vs bot", 550, 482, 620, 68, AppServices.OpenTest);
            ui.Label(ui.Root, "Training runs until you stop it.\nContinue restores the last saved state.\nNew training keeps earlier generations in the archive.",
                550, 595, 620, 120, 21, UIFactory.Muted);
            message = ui.Label(ui.Root, "", 550, 740, 620, 100, 20, new Color32(158, 60, 41, 255));
        }

        private void Try(Action action)
        {
            try { action(); }
            catch (Exception error) { message.text = "Could not open training: " + error.Message; Debug.LogException(error); }
        }

        private void Update() { if (board != null) board.Render(null); }
    }
}

using System;
using System.Collections;
using System.IO;
using System.Linq;
using FlappyBird.Core;
using FlappyBird.Persistence;
using FlappyBird.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace FlappyBird.Tests
{
    public sealed class SceneFlowTests
    {
        private string directory;

        private static Button FindButton(string caption)
        {
            return UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .Single(button => button.GetComponentInChildren<TMP_Text>()?.text == caption);
        }

        private static IEnumerator Capture(string name)
        {
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../TestResults/Previews"));
            Directory.CreateDirectory(output);
            yield return null;
            var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            var camera = Camera.main;
            Assert.IsNotNull(canvas);
            Assert.IsNotNull(camera);
            // Render at twice the 1280x960 layout resolution for sharp README images.
            var target = new RenderTexture(2560, 1920, 24);
            var pixels = new Texture2D(2560, 1920, TextureFormat.RGBA32, false);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var previousMode = canvas.renderMode;
            var previousCamera = canvas.worldCamera;
            var previousScale = canvas.scaleFactor;
            try
            {
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                canvas.scaleFactor = 2;
                Canvas.ForceUpdateCanvases();
                foreach (var label in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
                    Assert.IsFalse(label.isTextOverflowing, "Clipped interface text: " + label.text);
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 2560, 1920), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(output, name + ".png"), pixels.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode = previousMode;
                canvas.worldCamera = previousCamera;
                canvas.scaleFactor = previousScale;
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                UnityEngine.Object.Destroy(pixels);
                target.Release();
                UnityEngine.Object.Destroy(target);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator MenuTrainingArchiveAndBothGameModesWork()
        {
            directory = Path.Combine(Path.GetTempPath(), "FlappyBirdSceneTests-" + Guid.NewGuid().ToString("N"));
            AppServices.Repository = new JsonTrainingRepository(directory);
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;
            yield return null;
            Assert.IsNotNull(FindButton("New training"));
            Assert.IsNotNull(FindButton("Evaluate AI"));
            Assert.IsNotNull(UnityEngine.Object.FindFirstObjectByType<BoardView>());
            yield return Capture("01-main-menu");

            var session = new TrainingSession("scene-smoke", new EvolutionSettings(), 314159);
            AppServices.Repository.Save(session);
            AppServices.Training = session;
            yield return SceneManager.LoadSceneAsync("Training");
            yield return null;
            yield return null;
            FindButton("×30").onClick.Invoke();
            float deadline = Time.realtimeSinceStartup + 12;
            while (session.Generation < 2 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.GreaterOrEqual(session.Generation, 2, "Training did not advance.");
            FindButton("\u00d71").onClick.Invoke();
            // Capture the real population after the first pipes enter the field.
            for (int i = 0; i < 180; i++) session.Tick();
            yield return null;
            yield return Capture("02-training-live");
            FindButton("Stop and save").onClick.Invoke();
            string stopped = JsonUtility.ToJson(session.Capture());
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.AreEqual(stopped, JsonUtility.ToJson(session.Capture()), "Stopped training still runs.");
            Assert.AreEqual(stopped, JsonUtility.ToJson(AppServices.Repository.LoadLatest().Capture()));
            yield return Capture("02-training");

            FindButton("Evaluate AI").onClick.Invoke();
            yield return null;
            yield return null;
            yield return null;
            Assert.AreEqual("AITest", SceneManager.GetActiveScene().name);
            var dropdowns = UnityEngine.Object.FindObjectsByType<TMP_Dropdown>(FindObjectsSortMode.None);
            Assert.AreEqual(1, dropdowns.Length, "Evaluation should have a single generation-brain list.");
            Assert.IsTrue(dropdowns.All(dropdown => dropdown.options.Count > 0));
            dropdowns[0].Show();
            yield return null;
            dropdowns[0].Hide();
            FindButton("Watch bot").onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.2f);
            var screen = UnityEngine.Object.FindFirstObjectByType<AITestScreen>();
            Assert.AreEqual(1, screen.CurrentRound.World.Birds.Length);
            Assert.IsTrue(FindButton("Human vs bot").interactable);
            FindButton("Stop round").onClick.Invoke();
            FindButton("Human vs bot").onClick.Invoke();
            yield return null;
            Assert.AreEqual(2, screen.CurrentRound.World.Birds.Length);
            Assert.AreEqual(EvaluationState.WaitingForHuman, screen.CurrentRound.State);
            var board = UnityEngine.Object.FindFirstObjectByType<BoardView>();
            var hint = board.transform.Find("Overlay/Tap hint").gameObject;
            Assert.IsTrue(hint.activeSelf);
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.AreEqual(0, screen.CurrentRound.World.Ticks, "The course advanced before the first human flap.");
            yield return Capture("03-human-start-hint");
            FindButton("Flap / Space").onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.IsFalse(hint.activeSelf);
            Assert.IsTrue(UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None).Any(text => text.text.Contains("Human:")));
            yield return new WaitForSecondsRealtime(0.4f);
            FindButton("Flap / Space").onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.6f);
            FindButton("Flap / Space").onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Capture("04-human-versus-bot");

            // Reproduce the original one-bird/two-bird mismatch in both directions,
            // allowing Update to render statistics after each mode-button click.
            for (int i = 0; i < 4; i++)
            {
                FindButton("Watch bot").onClick.Invoke();
                yield return null;
                Assert.AreEqual(1, screen.CurrentRound.World.Birds.Length);
                Assert.IsNull(screen.CurrentRound.Human);
                Assert.IsFalse(hint.activeSelf);
                FindButton("Human vs bot").onClick.Invoke();
                yield return null;
                Assert.AreEqual(2, screen.CurrentRound.World.Birds.Length);
                Assert.IsTrue(hint.activeSelf);
                FindButton("Flap / Space").onClick.Invoke();
            }
            FindButton("Stop round").onClick.Invoke();
            FindButton("Main menu").onClick.Invoke();
            yield return null;
            yield return null;
            yield return null;
            Assert.AreEqual("MainMenu", SceneManager.GetActiveScene().name);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RestartAfterCompletionAndDigitOneAreCorrect()
        {
            directory = Path.Combine(Path.GetTempPath(), "FlappyBirdSceneTests-" + Guid.NewGuid().ToString("N"));
            AppServices.Repository = new JsonTrainingRepository(directory);
            var initial = new TrainingSession("restart-smoke", new EvolutionSettings { populationSize = 8, eliteCount = 2 }, 71).Capture();
            foreach (var brain in initial.population)
            {
                Array.Clear(brain.genes, 0, brain.genes.Length);
                brain.genes[brain.genes.Length - 1] = 10; // Always flap, then hit the ceiling.
            }
            var session = new TrainingSession(initial);
            GenerationChampion champion = null;
            for (int i = 0; i < 1000 && champion == null; i++) champion = session.Tick();
            Assert.IsNotNull(champion);
            AppServices.Repository.Save(session, champion);
            yield return SceneManager.LoadSceneAsync("AITest");
            yield return null;
            yield return null;
            var screen = UnityEngine.Object.FindFirstObjectByType<AITestScreen>();
            var board = UnityEngine.Object.FindFirstObjectByType<BoardView>();
            var hint = board.transform.Find("Overlay/Tap hint").GetComponent<RawImage>();
            FindButton("Watch bot").onClick.Invoke();
            screen.CurrentRound.Bot.score = 14; // Renderer regression fixture for the reported number.
            yield return null;
            Assert.AreNotEqual(hint.uvRect, board.transform.Find("Overlay/Score digit 0").GetComponent<RawImage>().uvRect,
                "Digit one still samples the pointing hand.");
            Assert.IsFalse(hint.gameObject.activeSelf);
            yield return Capture("05-score-14");
            float deadline = Time.realtimeSinceStartup + 5;
            while (screen.CurrentRound.IsActive && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(EvaluationState.Completed, screen.CurrentRound.State);
            FindButton("Human vs bot").onClick.Invoke();
            yield return null;
            Assert.AreEqual(2, screen.CurrentRound.World.Birds.Length);
            Assert.AreEqual(EvaluationState.WaitingForHuman, screen.CurrentRound.State);
            Assert.IsTrue(hint.gameObject.activeSelf);
            FindButton("Watch bot").onClick.Invoke();
            yield return null;
            Assert.AreEqual(1, screen.CurrentRound.World.Birds.Length);
            Assert.AreEqual(0, screen.CurrentRound.Bot.score);
            Assert.IsFalse(hint.gameObject.activeSelf);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;
            AppServices.Training = null;
            if (directory != null && Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}

using System;
using System.Collections.Generic;
using FlappyBird.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FlappyBird.Presentation
{
    public sealed class BoardView : MonoBehaviour, IPointerDownHandler
    {
        private Texture2D atlas;
        private RectTransform pipeRoot;
        private RectTransform birdRoot;
        private RectTransform overlayRoot;
        private RawImage[] ground;
        private readonly List<RawImage[]> pipeViews = new List<RawImage[]>();
        private readonly List<RawImage> birdViews = new List<RawImage>();
        private readonly List<RawImage> scoreDigits = new List<RawImage>();
        private static readonly Rect[] DigitSources = {
            new Rect(496, 434, 12, 18), new Rect(136, 39, 8, 18),
            new Rect(292, 334, 12, 18), new Rect(306, 334, 12, 18),
            new Rect(320, 334, 12, 18), new Rect(334, 334, 12, 18),
            new Rect(292, 310, 12, 18), new Rect(306, 310, 12, 18),
            new Rect(320, 310, 12, 18), new Rect(334, 310, 12, 18)
        };
        private RawImage ready;
        private RawImage tapHint;
        private RawImage gameOver;
        private RawImage title;
        private FlappyWorld displayedWorld;
        private float[] deathTimes = Array.Empty<float>();
        public Action Pressed { get; set; }

        public static BoardView Create(UIFactory ui)
        {
            ui.Panel(ui.Root, 52, 124, 440, 776, UIFactory.Ink);
            var rect = UIFactory.Rect("Flappy Bird field", ui.Root, 56, 128, 288, 512);
            // One atlas pixel occupies exactly three display pixels at the reference resolution.
            rect.localScale = Vector3.one * 1.5f;
            rect.gameObject.AddComponent<RectMask2D>();
            var view = rect.gameObject.AddComponent<BoardView>();
            view.Initialize();
            return view;
        }

        private void Initialize()
        {
            atlas = Resources.Load<Texture2D>("FlappyAtlas");
            if (atlas == null) throw new InvalidOperationException("FlappyAtlas is missing from Resources.");
            var background = Crop(transform, new Rect(0, 256, 144, 256), 0, 0, 288, 512);
            background.raycastTarget = true;
            pipeRoot = UIFactory.Rect("Pipes", transform, 0, 0, 288, 512);
            birdRoot = UIFactory.Rect("Birds", transform, 0, 0, 288, 512);
            var groundRoot = UIFactory.Rect("Ground", transform, 0, 400, 288, 112);
            ground = new[] { Crop(groundRoot, new Rect(292, 456, 168, 56), 0, 0, 336, 112),
                Crop(groundRoot, new Rect(292, 456, 168, 56), 336, 0, 336, 112) };
            overlayRoot = UIFactory.Rect("Overlay", transform, 0, 0, 288, 512);
            title = Crop(overlayRoot, new Rect(351, 397, 89, 24), 55, 95, 178, 48);
            ready = Crop(overlayRoot, new Rect(292, 428, 98, 25), 46, 128, 196, 50);
            tapHint = Crop(overlayRoot, new Rect(315, 372, 11, 18), 98, 228, 22, 36);
            tapHint.gameObject.name = "Tap hint";
            gameOver = Crop(overlayRoot, new Rect(395, 432, 96, 21), 48, 130, 192, 42);
            ready.gameObject.SetActive(false);
            tapHint.gameObject.SetActive(false);
            gameOver.gameObject.SetActive(false);
            Render(null);
        }

        private Rect UV(Rect rect) { return new Rect(rect.x / atlas.width, rect.y / atlas.height, rect.width / atlas.width, rect.height / atlas.height); }

        private RawImage Crop(Transform parent, Rect source, float x, float y, float width, float height)
        {
            var rect = UIFactory.Rect("Sprite", parent, x, y, width, height);
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = atlas;
            image.uvRect = UV(source);
            image.raycastTarget = false;
            return image;
        }

        public void SetReady(bool value)
        {
            ready.gameObject.SetActive(value);
            tapHint.gameObject.SetActive(value);
        }
        public void SetGameOver(bool value) { gameOver.gameObject.SetActive(value); }
        public void OnPointerDown(PointerEventData eventData) { if (eventData.button == PointerEventData.InputButton.Left) Pressed?.Invoke(); }

        public void Render(FlappyWorld world, bool versus = false)
        {
            if (!ReferenceEquals(displayedWorld, world))
            {
                displayedWorld = world;
                deathTimes = new float[world == null ? 0 : world.Birds.Length];
                for (int i = 0; i < deathTimes.Length; i++) deathTimes[i] = -1;
            }
            int pipeCount = world == null ? 0 : world.Pipes.Count;
            while (pipeViews.Count < pipeCount)
                pipeViews.Add(new[] { Crop(pipeRoot, new Rect(56, 29, 26, 160), 0, 0, 52, 320),
                    Crop(pipeRoot, new Rect(84, 29, 26, 160), 0, 0, 52, 320) });
            for (int i = 0; i < pipeViews.Count; i++)
            {
                foreach (var image in pipeViews[i]) image.gameObject.SetActive(i < pipeCount);
                if (i >= pipeCount) continue;
                var pipe = world.Pipes[i];
                float topY = (float)(GameRules.Height - pipe.gapCenter - GameRules.PipeGap / 2 - 320);
                float bottomY = (float)(GameRules.Height - pipe.gapCenter + GameRules.PipeGap / 2);
                pipeViews[i][0].rectTransform.anchoredPosition = new Vector2((float)pipe.x, -topY);
                pipeViews[i][1].rectTransform.anchoredPosition = new Vector2((float)pipe.x, -bottomY);
            }

            int birdCount = world == null ? 1 : world.Birds.Length;
            while (birdViews.Count < birdCount)
                birdViews.Add(Crop(birdRoot, new Rect(3, 9, 17, 12), 55, 200, 34, 24));
            int bestScore = 0;
            for (int i = 0; i < birdViews.Count; i++)
            {
                var bird = world != null && i < birdCount ? world.Birds[i] : null;
                bool show = i < birdCount && (bird == null || bird.alive || birdCount <= 2);
                birdViews[i].gameObject.SetActive(show);
                if (bird != null) bestScore = Math.Max(bestScore, bird.score);
                if (!show) continue;
                int frame = world == null ? (int)(Time.unscaledTime * 9) % 3 : world.Ticks / 7 % 3;
                Rect source = versus && i == 1
                    ? (frame == 0 ? new Rect(87, 9, 17, 12) : new Rect(115, frame == 1 ? 171 : 145, 17, 12))
                    : new Rect(3 + frame * 28, 9, 17, 12);
                birdViews[i].uvRect = UV(source);
                birdViews[i].color = new Color(1, 1, 1, birdCount > 2 ? 0.38f : 1);
                var rect = birdViews[i].rectTransform;
                rect.pivot = new Vector2(0.5f, 0.5f);
                float y = (float)(bird == null ? GameRules.StartY + Math.Sin(Time.unscaledTime * 3) * 4 : bird.y);
                bool falling = bird != null && !bird.alive;
                if (falling)
                {
                    if (deathTimes[i] < 0) deathTimes[i] = Time.unscaledTime;
                    double elapsed = Time.unscaledTime - deathTimes[i];
                    y = (float)Math.Max(GameRules.Ground + 12, bird.y + Math.Min(0, bird.velocity) * elapsed - 450 * elapsed * elapsed);
                }
                rect.anchoredPosition = new Vector2((float)GameRules.BirdX, -(512 - y));
                rect.localRotation = Quaternion.Euler(0, 0, falling ? -85 : bird == null ? 0 : Mathf.Clamp((float)bird.velocity * 0.12f, -85, 25));
            }
            string digits = bestScore.ToString(System.Globalization.CultureInfo.InvariantCulture);
            while (scoreDigits.Count < digits.Length)
            {
                var digit = Crop(overlayRoot, DigitSources[0], 0, 32, 24, 36);
                digit.gameObject.name = "Score digit " + scoreDigits.Count;
                scoreDigits.Add(digit);
            }
            float totalWidth = (digits.Length - 1) * 4;
            foreach (char digit in digits) totalWidth += DigitSources[digit - '0'].width * 2;
            float digitX = 144 - totalWidth / 2;
            for (int i = 0; i < scoreDigits.Count; i++)
            {
                scoreDigits[i].gameObject.SetActive(world != null && i < digits.Length);
                if (i >= digits.Length) continue;
                var source = DigitSources[digits[i] - '0'];
                scoreDigits[i].uvRect = UV(source);
                scoreDigits[i].rectTransform.sizeDelta = new Vector2(source.width * 2, source.height * 2);
                scoreDigits[i].rectTransform.anchoredPosition = new Vector2(digitX, -32);
                digitX += source.width * 2 + 4;
            }
            title.gameObject.SetActive(world == null);
            float offset = world == null ? Time.unscaledTime * 25 % 336 : (float)(world.Ticks * GameRules.Step * GameRules.PipeSpeed % 336);
            for (int i = 0; i < ground.Length; i++) ground[i].rectTransform.anchoredPosition = new Vector2(i * 336 - offset, 0);
        }
    }
}

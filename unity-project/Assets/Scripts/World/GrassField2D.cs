using System;
using System.Collections.Generic;
using Halka.Game.Player;
using UnityEngine;

namespace Halka.Game.World
{
    public sealed class GrassField2D : MonoBehaviour
    {
        [SerializeField] private GridWorld2D world;
        [SerializeField] private PlayerMover player;
        [SerializeField] private Sprite idleSprite;
        [SerializeField] private Sprite[] rustleFrames;
        [SerializeField] private float[] frameSeconds = { 0.09f, 0.09f, 0.09f, 0.09f, 0.42f };

        private readonly Dictionary<Vector2Int, SpriteRenderer> grassByCell = new Dictionary<Vector2Int, SpriteRenderer>();
        private readonly Dictionary<Vector2Int, ActiveRustle> active = new Dictionary<Vector2Int, ActiveRustle>();
        private readonly List<Vector2Int> completed = new List<Vector2Int>();

        private sealed class ActiveRustle
        {
            public SpriteRenderer Renderer;
            public int Frame;
            public float Elapsed;
        }

        public bool HasGrass(Vector2Int cell) => grassByCell.ContainsKey(cell);

        private void Awake()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (world == null || player == null || idleSprite == null || rustleFrames == null ||
                frameSeconds == null || rustleFrames.Length == 0 || rustleFrames.Length != frameSeconds.Length)
                throw new InvalidOperationException("Grass field sprites and frame times must match");

            for (var i = 0; i < rustleFrames.Length; i++)
                if (rustleFrames[i] == null || frameSeconds[i] <= 0f)
                    throw new InvalidOperationException("Grass rustle has an invalid frame");

            active.Clear();
            grassByCell.Clear();
            foreach (var renderer in GetComponentsInChildren<SpriteRenderer>())
            {
                renderer.sprite = idleSprite;
                var cell = world.WorldToCell(renderer.transform.position);
                if (!grassByCell.TryAdd(cell, renderer))
                    throw new InvalidOperationException($"Duplicate grass cell: {cell}");
            }
        }

        private void OnEnable()
        {
            if (player != null) player.StepStarted += OnStepStarted;
        }

        private void OnDisable()
        {
            if (player != null) player.StepStarted -= OnStepStarted;
            foreach (var state in active.Values) state.Renderer.sprite = idleSprite;
            active.Clear();
        }

        private void OnStepStarted(Vector2Int targetCell) => RustleAt(targetCell);

        public bool RustleAt(Vector2Int targetCell)
        {
            if (!grassByCell.TryGetValue(targetCell, out var renderer)) return false;
            renderer.sprite = rustleFrames[0];
            active[targetCell] = new ActiveRustle { Renderer = renderer };
            return true;
        }

        private void Update()
        {
            AdvanceAnimations(Time.deltaTime);
        }

        private void AdvanceAnimations(float deltaTime)
        {
            if (active.Count == 0) return;
            completed.Clear();
            foreach (var pair in active)
            {
                var state = pair.Value;
                state.Elapsed += Mathf.Max(0f, deltaTime);
                while (state.Elapsed >= frameSeconds[state.Frame])
                {
                    state.Elapsed -= frameSeconds[state.Frame];
                    state.Frame++;
                    if (state.Frame == rustleFrames.Length)
                    {
                        state.Renderer.sprite = idleSprite;
                        completed.Add(pair.Key);
                        break;
                    }
                    state.Renderer.sprite = rustleFrames[state.Frame];
                }
            }
            foreach (var cell in completed) active.Remove(cell);
        }
    }
}

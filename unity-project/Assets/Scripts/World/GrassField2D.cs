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
        [SerializeField] private SpriteRenderer frontOverlay;
        [SerializeField] private Sprite idleFrontSprite;
        [SerializeField] private Sprite[] rustleFrontFrames;
        [SerializeField] private float[] frameSeconds = { 0.09f, 0.09f, 0.09f, 0.09f, 0.42f };

        private readonly Dictionary<Vector2Int, SpriteRenderer> grassByCell = new Dictionary<Vector2Int, SpriteRenderer>();
        private readonly Dictionary<Vector2Int, ActiveRustle> active = new Dictionary<Vector2Int, ActiveRustle>();
        private readonly List<Vector2Int> completed = new List<Vector2Int>();
        private Vector2Int? frontCell;

        private sealed class ActiveRustle
        {
            public SpriteRenderer Renderer;
            public int Frame;
            public float Elapsed;
        }

        private void Awake()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (world == null || player == null || idleSprite == null || frontOverlay == null ||
                idleFrontSprite == null || rustleFrames == null || rustleFrontFrames == null ||
                frameSeconds == null || rustleFrames.Length == 0 ||
                rustleFrames.Length != frameSeconds.Length ||
                rustleFrames.Length != rustleFrontFrames.Length)
                throw new InvalidOperationException("Grass field sprites and frame times must match");

            for (var i = 0; i < rustleFrames.Length; i++)
                if (rustleFrames[i] == null || rustleFrontFrames[i] == null || frameSeconds[i] <= 0f)
                    throw new InvalidOperationException("Grass rustle has an invalid frame");

            grassByCell.Clear();
            foreach (var renderer in GetComponentsInChildren<SpriteRenderer>())
            {
                var cell = world.WorldToCell(renderer.transform.position);
                if (!grassByCell.TryAdd(cell, renderer))
                    throw new InvalidOperationException($"Duplicate grass cell: {cell}");
            }
            ShowFrontAt(world.WorldToCell(player.transform.position));
        }

        private void OnEnable()
        {
            if (player != null) player.StepStarted += OnStepStarted;
            if (grassByCell.Count > 0) ShowFrontAt(world.WorldToCell(player.transform.position));
        }

        private void OnDisable()
        {
            if (player != null) player.StepStarted -= OnStepStarted;
            foreach (var state in active.Values) state.Renderer.sprite = idleSprite;
            active.Clear();
            if (frontOverlay != null)
            {
                frontOverlay.sprite = idleFrontSprite;
                frontOverlay.enabled = false;
            }
            frontCell = null;
        }

        private void OnStepStarted(Vector2Int targetCell)
        {
            ShowFrontAt(targetCell);
            if (!grassByCell.TryGetValue(targetCell, out var renderer)) return;
            renderer.sprite = rustleFrames[0];
            active[targetCell] = new ActiveRustle { Renderer = renderer };
            frontOverlay.sprite = rustleFrontFrames[0];
        }

        private void ShowFrontAt(Vector2Int cell)
        {
            if (!grassByCell.ContainsKey(cell))
            {
                frontCell = null;
                frontOverlay.enabled = false;
                return;
            }
            frontCell = cell;
            frontOverlay.transform.position = world.CellToWorld(cell);
            frontOverlay.sprite = active.TryGetValue(cell, out var state)
                ? rustleFrontFrames[state.Frame] : idleFrontSprite;
            frontOverlay.enabled = true;
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
                        if (frontCell == pair.Key) frontOverlay.sprite = idleFrontSprite;
                        completed.Add(pair.Key);
                        break;
                    }
                    state.Renderer.sprite = rustleFrames[state.Frame];
                    if (frontCell == pair.Key) frontOverlay.sprite = rustleFrontFrames[state.Frame];
                }
            }
            foreach (var cell in completed) active.Remove(cell);
        }
    }
}

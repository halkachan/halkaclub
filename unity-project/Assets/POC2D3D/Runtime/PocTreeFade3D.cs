using System.Collections.Generic;
using UnityEngine;

namespace Halka.Poc2D3D
{
    // Phase 4: the 3D view's see-through trees (the 2D view's rule, measured the 3D way). A tree
    // (FadeInFront3D) that actually stands between the 3D camera and ﾊﾙｶﾁｬﾝ - its triangles cut a
    // sight line to her body - fades to its FadeOpacity (40%) over FadeSeconds (0.25 s), and back
    // once nothing of her is behind it. The renderer swaps to the fade material only while it is
    // see-through and gets its own material back when opaque, so the 2D view, the outline and the
    // shadows are untouched. The crow and the player are separate renderers and never fade.
    public sealed class PocTreeFade3D
    {
        // Sight lines from the camera to these points of the player (feet to head, both sides).
        public static readonly Vector3[] BodySamples =
        {
            new Vector3(0f, 0.12f, 0f), new Vector3(0f, 0.32f, 0f), new Vector3(0f, 0.52f, 0f),
            new Vector3(0f, 0.8f, 0f), new Vector3(0.14f, 0.5f, 0f), new Vector3(-0.14f, 0.5f, 0f)
        };

        private readonly Material fadeMaterial;
        private readonly Dictionary<PocObjectView, Faded> faded = new Dictionary<PocObjectView, Faded>();
        private readonly List<PocObjectView> order = new List<PocObjectView>();
        private MaterialPropertyBlock block;

        private sealed class Faded
        {
            public float Alpha = 1f;
            public bool Swapped;
            public Renderer[] Renderers;
            public Material[] Originals;
        }

        public PocTreeFade3D(Material material) => fadeMaterial = material;

        public bool Enabled { get; set; } = true;
        public int FadingCount => order.Count;
        public float AlphaOf(PocObjectView view) => view != null && faded.TryGetValue(view, out var f) ? f.Alpha : 1f;
        public bool IsSwapped(PocObjectView view) => view != null && faded.TryGetValue(view, out var f) && f.Swapped;
        public IEnumerable<PocObjectView> FadingViews => order;

        // True when the view's triangles cut a sight line from the camera to the player's body.
        public static bool HidesPlayer(PocObjectView view, Vector3 cameraPosition, Vector3 playerFeet, Quaternion cameraYaw)
        {
            foreach (var sample in BodySamples)
            {
                var point = playerFeet + cameraYaw * sample;
                var direction = point - cameraPosition;
                var length = direction.magnitude;
                if (length < 0.01f) continue;
                var ray = new Ray(cameraPosition, direction / length);
                foreach (var part in view.Renderers)
                    if (view.RaycastPart(part, ray, out var hit) && hit < length - 0.05f) return true;
            }
            return false;
        }

        public void Update(IReadOnlyList<PocObjectView> views, Vector3 cameraPosition, Vector3 playerFeet,
            Quaternion cameraYaw, float deltaTime)
        {
            var targets = new HashSet<PocObjectView>();
            if (Enabled)
                foreach (var view in views)
                    if (view != null && view.FadeInFront3D && HidesPlayer(view, cameraPosition, playerFeet, cameraYaw))
                        targets.Add(view);
            foreach (var view in targets)
                if (!faded.ContainsKey(view))
                {
                    faded[view] = new Faded();
                    order.Add(view);
                }
            for (var i = order.Count - 1; i >= 0; i--)
            {
                var view = order[i];
                if (view == null)
                {
                    faded.Remove(view);
                    order.RemoveAt(i);
                    continue;
                }
                var state = faded[view];
                var opacity = Mathf.Clamp01(view.FadeOpacity);
                var seconds = Mathf.Max(0.0001f, view.FadeSeconds);
                var step = deltaTime / seconds * (1f - opacity);
                var inFront = targets.Contains(view);
                state.Alpha = Mathf.MoveTowards(state.Alpha, inFront ? opacity : 1f, step);
                if (!inFront && state.Alpha >= 1f)
                {
                    Restore(state);
                    faded.Remove(view);
                    order.RemoveAt(i);
                    continue;
                }
                Apply(view, state);
            }
        }

        private void Apply(PocObjectView view, Faded state)
        {
            if (fadeMaterial == null) return;
            if (!state.Swapped)
            {
                state.Renderers = view.Renderers;
                state.Originals = new Material[state.Renderers.Length];
                for (var i = 0; i < state.Renderers.Length; i++)
                {
                    var item = state.Renderers[i];
                    if (item == null) continue;
                    state.Originals[i] = item.sharedMaterial;
                    item.sharedMaterial = fadeMaterial;
                }
                state.Swapped = true;
            }
            if (block == null) block = new MaterialPropertyBlock();
            block.SetFloat("_Alpha", state.Alpha);
            foreach (var item in state.Renderers)
                if (item != null) item.SetPropertyBlock(block);
        }

        private static void Restore(Faded state)
        {
            if (!state.Swapped) return;
            for (var i = 0; i < state.Renderers.Length; i++)
            {
                var item = state.Renderers[i];
                if (item == null) continue;
                item.sharedMaterial = state.Originals[i];
                item.SetPropertyBlock(null);
            }
            state.Swapped = false;
        }

        // Leaving the 3D view (or rebuilding the map): every tree opaque with its own material.
        public void RestoreAll()
        {
            foreach (var view in order)
                if (view != null && faded.TryGetValue(view, out var state)) Restore(state);
            faded.Clear();
            order.Clear();
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Halka.Poc2D3D
{
    // Presentation Metadata: how one Object Definition ID looks in 3D. It never stores cells,
    // footprints or blocking; those stay in the Map JSON v4 Catalog and Map instances.
    [Serializable]
    public sealed class PocPresentationBinding
    {
        public string definitionId;
        public GameObject model;
        [Tooltip("Shape variations; an Instance ID always picks the same one (FNV-1a hash).")]
        public List<GameObject> variants = new List<GameObject>();
        [Tooltip("Extra yaw after the model's South-facing default.")]
        public float yawDegrees;
        public Vector3 meshOffset;
        public float uniformScale = 1f;
        [Tooltip("Hidden in 3D when it stands between the camera and the player.")]
        public bool cameraOccluder = true;
        public bool castShadows = true;
        [Tooltip("Child transform exported from Blender, e.g. a future Crow perch point.")]
        public string perchAnchorName;
        [Tooltip("A Phase 1 technical stand-in, not a finished design.")]
        public bool placeholder;
        [Tooltip("2D Pixel View outline strength (0 = none).")]
        [Range(0f, 1f)] public float outlineStrength = 1f;

        public GameObject Pick(string instanceId)
        {
            if (variants == null || variants.Count == 0) return model;
            return variants[(int)(PocStableHash.Of(instanceId) % (uint)variants.Count)];
        }
    }

    // How the Map's Surfaces and derived grass are dressed in 3D. The pieces come from Blender;
    // which cell gets which piece is decided from Map Data every time the map is built.
    [Serializable]
    public sealed class PocEnvironmentPresentation
    {
        public List<GameObject> grassTiles = new List<GameObject>();
        [Tooltip("Quarter-cell road pieces for the north-east quadrant (rotated per quadrant).")]
        public GameObject dirtFull;
        public GameObject dirtEdge;
        public GameObject dirtCorner;
        public List<GameObject> grassTufts = new List<GameObject>();
        [Range(0f, 1f)] public float tuftChance = 0.8f;
        [Range(0f, 1f)] public float roadsideTuftChance = 0.55f;
        [Min(0)] public int outsideMargin = 8;
        public Color outsideTint = new Color(0.9f, 0.93f, 0.88f, 1f);

        public bool IsComplete => grassTiles.Count > 0 && dirtFull != null && dirtEdge != null &&
            dirtCorner != null && grassTufts.Count > 0;
    }

    // How the player is drawn. Swapping in the official ﾊﾙｶﾁｬﾝ model later means changing this
    // entry only: Grid position, facing, steps and interaction never read the model.
    [Serializable]
    public sealed class PocActorPresentation
    {
        public GameObject model;
        [Tooltip("Turns the model so it looks along the actor's facing (Blender front = South => 180).")]
        public float yawOffsetDegrees = GridSpace3D.ModelFrontYawOffset;
        public Vector3 pivotOffset;
        public float uniformScale = 1f;
        [Tooltip("Extra scale only in the 2D Pixel View (readability); 1 = same as 3D.")]
        public float pixelViewScale = 0.82f;
        [Tooltip("2D Pixel View only: lean the model back about its feet so its front faces the 2D " +
                 "camera like the hand-drawn sprites (1 = fully facing the camera, 0 = upright).")]
        [Range(0f, 1f)] public float pixelViewFaceCamera = 1f;
        [Tooltip("2D Pixel View only: move the model this far toward the orthographic camera. The " +
                 "screen position does not change; it only keeps the leaning head in front of " +
                 "things North of the player, like the sprite draw order.")]
        public float pixelViewDepthBias = 0.35f;
        [Tooltip("Optional child names swung procedurally while walking (no Rig needed).")]
        public string armLeft = "ArmL";
        public string armRight = "ArmR";
        public string legLeft = "LegL";
        public string legRight = "LegR";
        public float swingDegrees = 26f;
        [Range(0f, 1f)] public float outlineStrength = 1f;
        public bool placeholder = true;
    }

    [CreateAssetMenu(menuName = "HALKA WORLD/POC Presentation Catalog")]
    public sealed class PocPresentationCatalog : ScriptableObject
    {
        public const string FallbackNote = "Not modeled yet: generated box from blocked cells";

        [SerializeField] private GameObject playerModel;
        [SerializeField] private Material vertexColorMaterial;
        [SerializeField] private Material overlayMaterial;
        [SerializeField] private List<PocPresentationBinding> bindings = new List<PocPresentationBinding>();
        [SerializeField] private PocEnvironmentPresentation environment = new PocEnvironmentPresentation();
        [SerializeField] private PocActorPresentation player = new PocActorPresentation();

        public GameObject PlayerModel => playerModel;
        public Material VertexColorMaterial => vertexColorMaterial;
        public Material OverlayMaterial => overlayMaterial;
        public IReadOnlyList<PocPresentationBinding> Bindings => bindings;
        public PocEnvironmentPresentation Environment => environment;
        public PocActorPresentation Player => player;
        public void ConfigurePlayer(PocActorPresentation presentation) => player = presentation;

        public void Configure(GameObject player, Material vertexColor, Material overlay,
            List<PocPresentationBinding> entries, PocEnvironmentPresentation dressing = null)
        {
            playerModel = player;
            vertexColorMaterial = vertexColor;
            overlayMaterial = overlay;
            bindings = entries;
            environment = dressing ?? new PocEnvironmentPresentation();
        }

        public PocPresentationBinding Find(string definitionId)
        {
            foreach (var binding in bindings)
                if (binding.definitionId == definitionId && (binding.model != null ||
                    binding.variants != null && binding.variants.Count > 0)) return binding;
            return null;
        }
    }

    // Stable across runs, platforms and .NET versions (string.GetHashCode is not guaranteed).
    public static class PocStableHash
    {
        public static uint Of(string text)
        {
            var hash = 2166136261u;
            foreach (var character in text ?? "")
            {
                hash ^= character;
                hash *= 16777619u;
            }
            return hash;
        }

        public static uint Of(string mapId, Vector2Int cell, string salt) =>
            Of(mapId + ":" + cell.x + "," + cell.y + ":" + salt);

        // 0..1 from the hash, for chances and jitter.
        public static float Unit(uint hash) => (hash % 100000u) / 100000f;
    }
}

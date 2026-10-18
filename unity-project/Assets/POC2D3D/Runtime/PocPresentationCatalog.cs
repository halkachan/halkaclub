using System;
using System.Collections.Generic;
using Halka.Game.Player;
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
        [Tooltip("2D Pixel View: drawn see-through while it stands in front of the player sprite.")]
        public bool fadeInFront2D;
        [Tooltip("3D view (Phase 4): drawn see-through while it hides the player from the camera.")]
        public bool fadeInFront3D;
        [Tooltip("See-through opacity, the same in 2D and 3D.")]
        [Range(0f, 1f)] public float fadeOpacity = 0.4f;
        [Tooltip("Seconds to fade out (and back in), the same in 2D and 3D.")]
        [Min(0f)] public float fadeSeconds = 0.25f;
        [Tooltip("3D: hide only the parts (e.g. the house roof) that stand between camera and player.")]
        public bool partialOcclusion;
        [Tooltip("Clicks: treat the object as at least this large (0 = its real bounds), for small things.")]
        [Min(0f)] public float minPickSize;

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

    // How interior maps are built in 3D (Phase 3): Blender floor tiles on every floor cell, and
    // the blocking wall Surfaces as a low base (always shown) plus an upper part that is cut
    // away on the camera's side. The doorway sits on the interior_exit marker cell.
    [Serializable]
    public sealed class PocInteriorPresentation
    {
        public List<GameObject> floorTiles = new List<GameObject>();
        public GameObject wallBase;
        public GameObject wallUpper;
        public GameObject doorwayBase;
        public GameObject doorwayUpper;
        [Tooltip("Height of the wall base that stays visible when a wall is cut away.")]
        public float wallBaseHeight = 0.14f;

        public bool IsComplete => floorTiles.Count > 0 && floorTiles.TrueForAll(t => t != null) &&
            wallBase != null && wallUpper != null && doorwayBase != null && doorwayUpper != null;
    }

    // The existing hand-drawn ﾊﾙｶﾁｬﾝ sprites, referenced as they are (the files are never
    // modified). Same frames, order and selection as CharacterVisual in the 2D game.
    [Serializable]
    public sealed class PocSpriteSet
    {
        public Sprite[] frontIdle;
        public Sprite[] backIdle;
        public Sprite[] leftIdle;
        public Sprite[] rightIdle;
        public Sprite[] frontWalk;
        public Sprite[] backWalk;
        public Sprite[] leftWalk;
        public Sprite[] rightWalk;
        [Tooltip("Local Y of the sprite above the player's cell in the 2D game (FirstDay: 0.25).")]
        public float offsetY = 0.25f;
        [Tooltip("Phase 4: the seated artwork (bench_sit.png, CharacterVisual.benchSit).")]
        public Sprite benchSit;

        // The seated artwork for a seat pose: bench_sit.png (CharacterVisual.benchSit) for the bench
        // and the sofa, which use the bench_sit pose.
        public Sprite SeatSprite(string pose) => benchSit;

        public bool IsComplete => Has(frontIdle) && Has(backIdle) && Has(leftIdle) && Has(rightIdle) &&
            Has(frontWalk) && Has(backWalk) && Has(leftWalk) && Has(rightWalk);

        // Same selection as CharacterVisual.SelectFrames.
        public Sprite[] Select(FacingDirection facing, bool walking)
        {
            switch (facing)
            {
                case FacingDirection.Up: return walking ? backWalk : backIdle;
                case FacingDirection.Left: return walking ? leftWalk : leftIdle;
                case FacingDirection.Right: return walking ? rightWalk : rightIdle;
                default: return walking ? frontWalk : frontIdle;
            }
        }

        private static bool Has(Sprite[] frames) => frames != null && frames.Length > 0 && frames[0] != null;
    }

    // How the player is drawn: the 2D Pixel View uses the existing sprites, the 3D view uses the
    // model. Swapping in the official ﾊﾙｶﾁｬﾝ model later means changing this entry only: Grid
    // position, facing, steps and interaction never read the model or the sprites.
    [Serializable]
    public sealed class PocActorPresentation
    {
        [Tooltip("3D view only.")]
        public GameObject model;
        [Tooltip("Turns the model so it looks along the actor's facing (Blender front = South => 180).")]
        public float yawOffsetDegrees = GridSpace3D.ModelFrontYawOffset;
        public Vector3 pivotOffset;
        public float uniformScale = 1f;
        [Tooltip("Optional child names swung procedurally while walking (no Rig needed).")]
        public string armLeft = "ArmL";
        public string armRight = "ArmR";
        public string legLeft = "LegL";
        public string legRight = "LegR";
        public float swingDegrees = 26f;
        public bool placeholder = true;
        [Tooltip("2D Pixel View only: the original illustration, drawn instead of the model.")]
        public PocSpriteSet sprites = new PocSpriteSet();
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
        [SerializeField] private PocInteriorPresentation interior = new PocInteriorPresentation();

        public GameObject PlayerModel => playerModel;
        public Material VertexColorMaterial => vertexColorMaterial;
        public Material OverlayMaterial => overlayMaterial;
        public IReadOnlyList<PocPresentationBinding> Bindings => bindings;
        public PocEnvironmentPresentation Environment => environment;
        public PocActorPresentation Player => player;
        public void ConfigurePlayer(PocActorPresentation presentation) => player = presentation;
        public PocInteriorPresentation Interior => interior;
        public void ConfigureInterior(PocInteriorPresentation presentation) => interior = presentation;

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

    // Phase 3: which Object Definitions still lack a real 3D model. A placeholder or no binding
    // (drawn as a generated box) counts as missing, so a newly added Catalog object shows up.
    public static class PocBindingAudit
    {
        public static IEnumerable<string> Missing(PocPresentationCatalog catalog, IEnumerable<string> definitionIds)
        {
            foreach (var id in definitionIds)
            {
                var binding = catalog != null ? catalog.Find(id) : null;
                if (binding == null || binding.placeholder) yield return id;
            }
        }

        public static string Report(PocPresentationCatalog catalog, IEnumerable<string> definitionIds)
        {
            var text = new System.Text.StringBuilder();
            text.AppendLine("| Definition ID | 3D model | Status |");
            text.AppendLine("|---|---|---|");
            foreach (var id in definitionIds)
            {
                var binding = catalog.Find(id);
                var model = binding == null ? "-" : binding.Pick(id).name;
                var status = binding == null ? "missing (generated box)" : binding.placeholder ? "placeholder" : "Blender model";
                text.Append("| ").Append(id).Append(" | ").Append(model).Append(" | ").Append(status).AppendLine(" |");
            }
            return text.ToString();
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

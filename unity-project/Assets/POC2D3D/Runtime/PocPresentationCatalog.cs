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
        [Tooltip("Extra yaw after the model's South-facing default.")]
        public float yawDegrees;
        public Vector3 meshOffset;
        public float uniformScale = 1f;
        [Tooltip("Hidden in 3D when it stands between the camera and the player.")]
        public bool cameraOccluder = true;
    }

    [CreateAssetMenu(menuName = "HALKA WORLD/POC Presentation Catalog")]
    public sealed class PocPresentationCatalog : ScriptableObject
    {
        public const string FallbackNote = "Not modeled yet: generated box from blocked cells";

        [SerializeField] private GameObject playerModel;
        [SerializeField] private Material vertexColorMaterial;
        [SerializeField] private Material overlayMaterial;
        [SerializeField] private List<PocPresentationBinding> bindings = new List<PocPresentationBinding>();

        public GameObject PlayerModel => playerModel;
        public Material VertexColorMaterial => vertexColorMaterial;
        public Material OverlayMaterial => overlayMaterial;
        public IReadOnlyList<PocPresentationBinding> Bindings => bindings;

        public void Configure(GameObject player, Material vertexColor, Material overlay,
            List<PocPresentationBinding> entries)
        {
            playerModel = player;
            vertexColorMaterial = vertexColor;
            overlayMaterial = overlay;
            bindings = entries;
        }

        public PocPresentationBinding Find(string definitionId)
        {
            foreach (var binding in bindings)
                if (binding.definitionId == definitionId && binding.model != null) return binding;
            return null;
        }
    }
}

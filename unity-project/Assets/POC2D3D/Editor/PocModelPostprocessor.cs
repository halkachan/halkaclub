using UnityEditor;

namespace Halka.Poc2D3D.EditorTools
{
    // Import rules for Blender exports in Assets/POC2D3D/Models only. Shape, colors and pivots
    // come from the FBX; Unity does not rescale, re-orient or recolor them.
    public sealed class PocModelPostprocessor : AssetPostprocessor
    {
        public const string ModelFolder = "Assets/POC2D3D/Models/";

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ModelFolder, System.StringComparison.Ordinal)) return;
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.importBlendShapes = false;
            importer.importVisibility = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.preserveHierarchy = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = true; // POC checks read vertex colors and positions.
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;
            importer.weldVertices = false; // Keep hard edges and per-face colors.
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.None;
            importer.generateSecondaryUV = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        }
    }
}

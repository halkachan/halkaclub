using Halka.Game.World;
using UnityEditor;
using UnityEngine;

namespace Halka.Game.Editor
{
    [CustomEditor(typeof(MapDefinition))]
    public sealed class GeneratedMapDefinitionInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var map = (MapDefinition)target;
            EditorGUILayout.HelpBox(
                "GENERATED FROM: Assets/Content/Maps/Authoring/" + map.MapId +
                ".hwmap.json\nDO NOT EDIT DIRECTLY. Use HALKA WORLD MAP EDITOR.exe.",
                MessageType.Info);
            EditorGUILayout.LabelField("Map", map.MapId);
            EditorGUILayout.LabelField("Bounds", map.MinCell + " .. " + map.MaxCell);
            EditorGUILayout.LabelField("Surfaces", map.Surfaces.Count.ToString());
            EditorGUILayout.LabelField("Objects", map.Objects.Count.ToString());
            if (GUILayout.Button("Sync from Authoring JSON")) MapAuthoringImporter.SyncAll();
        }
    }
}

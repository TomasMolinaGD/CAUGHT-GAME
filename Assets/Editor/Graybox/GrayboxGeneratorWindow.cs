using CaughtGame.Navigation;
using UnityEditor;
using UnityEngine;

namespace CaughtGame.Navigation.Editor
{
    public sealed class GrayboxGeneratorWindow : EditorWindow
    {
        private SceneAsset targetScene;
        private NavigationGrid navigationGrid;
        private Material floorMaterial;
        private Material wallMaterial;
        private float wallHeight = 4f;
        private float floorThickness = 0.5f;

        [MenuItem("Tools/CAUGHT/Graybox Generator")]
        private static void Open()
        {
            GetWindow<GrayboxGeneratorWindow>("Graybox Generator");
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "This tool only modifies the loaded Parcial2_Main_Grid scene in memory. It does not save the scene. " +
                "Generated objects are isolated beneath Level/GeneratedGeometry.", MessageType.Info);
            targetScene = (SceneAsset)EditorGUILayout.ObjectField("Target Scene", targetScene, typeof(SceneAsset), false);
            navigationGrid = (NavigationGrid)EditorGUILayout.ObjectField("Navigation Grid", navigationGrid,
                typeof(NavigationGrid), true);
            wallHeight = EditorGUILayout.FloatField("Wall Height", wallHeight);
            floorThickness = EditorGUILayout.FloatField("Floor Thickness", floorThickness);
            floorMaterial = (Material)EditorGUILayout.ObjectField("Floor Material", floorMaterial, typeof(Material), false);
            wallMaterial = (Material)EditorGUILayout.ObjectField("Wall Material", wallMaterial, typeof(Material), false);

            using (new EditorGUI.DisabledScope(targetScene == null || navigationGrid == null))
            {
                if (GUILayout.Button("Generate / Regenerate Graybox"))
                {
                    if (!EditorUtility.DisplayDialog("Generate Graybox?",
                            "Build a staged floor and wall set from the selected grid in the loaded scene? The scene will not be saved automatically.",
                            "Generate", "Cancel"))
                        return;

                    bool succeeded = GrayboxGeometryBuilder.GenerateInOpenScene(targetScene, navigationGrid,
                        wallHeight, floorThickness, floorMaterial, wallMaterial, out string message);
                    if (succeeded) Debug.Log(message, navigationGrid);
                    else Debug.LogWarning(message, navigationGrid);
                }
            }
        }
    }
}

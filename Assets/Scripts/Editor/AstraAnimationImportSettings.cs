using UnityEditor;

[InitializeOnLoad]
public static class AstraAnimationImportSettings
{
    private const string WalkingAssetPath = "Assets/Animations/NPCs/Walk/Walking.fbx";

    static AstraAnimationImportSettings()
    {
        EditorApplication.delayCall += EnsureWalkingLoops;
    }

    private static void EnsureWalkingLoops()
    {
        ModelImporter importer = AssetImporter.GetAtPath(WalkingAssetPath) as ModelImporter;
        if (importer == null)
        {
            return;
        }

        ModelImporterClipAnimation[] configuredClips = importer.clipAnimations;
        bool needsSerializedOverride = configuredClips == null || configuredClips.Length == 0;

        if (needsSerializedOverride)
        {
            configuredClips = importer.defaultClipAnimations;
        }

        bool settingsChanged = needsSerializedOverride;

        foreach (ModelImporterClipAnimation clip in configuredClips)
        {
            if (!clip.loopTime || !clip.loopPose)
            {
                clip.loopTime = true;
                clip.loopPose = true;
                settingsChanged = true;
            }
        }

        if (!settingsChanged)
        {
            return;
        }

        importer.clipAnimations = configuredClips;
        importer.SaveAndReimport();
    }
}

#if UNITY_EDITOR
using System.IO;
using UnityEditor;

/// <summary>
/// One-shot build trigger used to produce the Levels 1-6 playable zip without
/// needing a second Unity instance while the editor is open.
/// </summary>
[InitializeOnLoad]
public static class AutoBuildLevels1to6
{
    const string Marker = @"D:\unity_project\_build_levels1_6.trigger";
    const string Done = @"D:\unity_project\_build_levels1_6.done";

    static AutoBuildLevels1to6()
    {
        EditorApplication.delayCall += Check;
    }

    static void Check()
    {
        if (!File.Exists(Marker)) return;
        File.Delete(Marker);
        try
        {
            GameBuild.BuildWindowsLevels1to6();
            File.WriteAllText(Done, "ok");
        }
        catch (System.Exception e)
        {
            File.WriteAllText(Done, "error: " + e);
        }
    }
}
#endif

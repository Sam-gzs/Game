using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Scene flow and level progress for the whole game.</summary>
public static class GameFlow
{
    public const int MaxLevelCount = 10;
    public const string MenuSceneName = "MainMenu";

    /// <summary>
    /// Levels actually present in this build. A six-level playable build therefore
    /// exposes only six level buttons and Level 6's "next" returns to the menu.
    /// Scene 0 is the main menu, so the level count is sceneCount - 1.
    /// </summary>
    public static int LevelCount
    {
        get
        {
            int fromBuild = SceneManager.sceneCountInBuildSettings - 1;
            return Mathf.Clamp(fromBuild, 1, MaxLevelCount);
        }
    }

    public static string LevelSceneName(int index)
    {
        return "Level" + (index + 1);
    }

    public static bool IsCleared(int index)
    {
        return PlayerPrefs.GetInt("stickman_cleared_" + index, 0) == 1;
    }

    public static void MarkCleared(int index)
    {
        PlayerPrefs.SetInt("stickman_cleared_" + index, 1);
        PlayerPrefs.Save();
    }

    public static void LoadMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(MenuSceneName);
    }

    /// <summary>Quit from anywhere. In the editor this just stops Play mode.</summary>
    public static void QuitGame()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public static void LoadLevel(int index)
    {
        index = Mathf.Clamp(index, 0, LevelCount - 1);
        Time.timeScale = 1f;
        SceneManager.LoadScene(LevelSceneName(index));
    }

    public static void LoadNext(int current)
    {
        if (current + 1 < LevelCount) LoadLevel(current + 1);
        else LoadMenu();
    }
}

/// <summary>Tells the in-level UI which level this scene is.</summary>
public class LevelInfo : MonoBehaviour
{
    public int levelIndex;
}

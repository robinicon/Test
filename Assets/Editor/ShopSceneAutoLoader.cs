#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

// Guarantees there is always a scene to play, so the shop runs the moment
// you press Play even if the project was opened without any scene.
[InitializeOnLoad]
public static class ShopSceneAutoLoader
{
    static ShopSceneAutoLoader()
    {
        EditorApplication.playModeStateChanged += EnsureSceneOnPlay;
    }

    static void EnsureSceneOnPlay(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            if (SceneManager.sceneCount == 0)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }
    }
}
#endif

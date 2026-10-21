using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class EnsureGameViewOnPlay
{
    static EnsureGameViewOnPlay()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType != null)
            {
                var gameView = EditorWindow.GetWindow(gameViewType);
                if (gameView != null)
                {
                    gameView.Show();
                    gameView.Focus();
                }
            }
        }
    }

    [MenuItem("Tools/FLIP//FALL/Open Game View")]
    public static void OpenGameView()
    {
        var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        if (gameViewType != null)
        {
            var gameView = EditorWindow.GetWindow(gameViewType);
            if (gameView != null)
            {
                gameView.Show();
                gameView.Focus();
            }
        }
    }
}

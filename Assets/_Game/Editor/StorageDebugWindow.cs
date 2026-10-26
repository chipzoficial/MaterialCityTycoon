using UnityEditor;

namespace MaterialCityTycoon.EditorTools
{
    // Redirect previously saved Editor layouts to the consolidated window.
    public sealed class StorageDebugWindow : EditorWindow
    {
        private void OnEnable()
        {
            if (EditorApplication.isPlaying) StoreDebugWindow.Open();
            Close();
        }
    }
}

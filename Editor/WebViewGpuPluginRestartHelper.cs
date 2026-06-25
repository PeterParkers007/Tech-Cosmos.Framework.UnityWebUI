#if UNITY_EDITOR
using UnityEditor;

namespace UnityWebUI.Editor
{
    /// <summary>
    /// Used by restart-unity-apply-gpu.bat via Unity -executeMethod.
    /// </summary>
    public static class WebViewGpuPluginRestartHelper
    {
        public static void EnterPlayModeAfterLoad()
        {
            EditorApplication.update += WaitForReadyThenPlay;
        }

        static void WaitForReadyThenPlay()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.update -= WaitForReadyThenPlay;
                return;
            }

            EditorApplication.update -= WaitForReadyThenPlay;
            EditorApplication.isPlaying = true;
        }
    }
}
#endif

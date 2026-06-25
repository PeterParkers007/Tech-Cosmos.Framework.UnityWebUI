#if UNITY_EDITOR
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityWebUI.WebView;

namespace UnityWebUI.Editor
{
    static class WebViewGpuBuildMenu
    {
        const string BuildBat = "Assets/UnityWebUI/Native/Windows/build.bat";

        [MenuItem("Window/Unity Web UI/Diagnose GPU Backend")]
        public static void DiagnoseGpuBackend()
        {
            UnityEngine.Debug.Log("[UnityWebUI] GPU probe:\n" + WindowsGpuWebViewNative.ProbeDiagnostics());
        }

        [MenuItem("Window/Unity Web UI/Build Windows GPU Plugin")]
        public static void BuildGpuPlugin()
        {
            WebViewGpuPluginDeploy.ClosePreviewWindows();

            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var batPath = Path.Combine(projectRoot, BuildBat.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(batPath))
            {
                EditorUtility.DisplayDialog("Unity Web UI", "build.bat not found:\n" + batPath, "OK");
                return;
            }

            var logPath = Path.Combine(Path.GetDirectoryName(batPath), "build-last.log");
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{batPath}\"",
                WorkingDirectory = Path.GetDirectoryName(batPath),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            string log;
            int exitCode = -1;
            using (var process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    log = "Failed to start cmd.exe for build.bat.";
                }
                else
                {
                    var stdout = process.StandardOutput.ReadToEnd();
                    var stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    exitCode = process.ExitCode;
                    log = string.IsNullOrEmpty(stderr) ? stdout : stdout + stderr;
                }
            }

            File.WriteAllText(logPath, log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            UnityEngine.Debug.Log($"UnityWebUI GPU build log (exit {exitCode}):\n{log}");

            var builtDll = WebViewGpuPluginDeploy.BuiltDllPath;
            var compileOk = exitCode == 0 && File.Exists(builtDll);

            if (!compileOk)
            {
                var tail = GetLogTail(log, 12);
                var hint = log.Contains("LNK1104")
                    ? "\n\nHint: Unity is locking the GPU plugin DLL. Close Unity, run Assets/UnityWebUI/Native/Windows/apply-gpu-plugin.bat, then reopen."
                    : string.Empty;
                EditorUtility.DisplayDialog(
                    "Unity Web UI",
                    $"Build failed (exit {exitCode}).\n\n{tail}{hint}\n\nFull log: Assets/UnityWebUI/Native/Windows/build-last.log",
                    "OK");
                return;
            }

            if (WebViewGpuPluginDeploy.TryDeploy(out var deployMessage))
            {
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog(
                    "Unity Web UI",
                    "GPU plugin build finished.\n" + deployMessage +
                    "\n\nRestart Unity once so the new native DLL is loaded into memory.",
                    "OK");
                return;
            }

            EditorUtility.DisplayDialog(
                "Unity Web UI",
                "Compile succeeded.\n\n" +
                deployMessage + "\n\n" +
                "Native plugin is locked while Unity is open.\n" +
                "• Saved to .pending — it will auto-apply when you close Unity\n" +
                "• Next time you open the project, the new DLL is used\n" +
                "• No need to run apply-gpu-plugin.bat manually if you quit Unity normally",
                "OK");
        }

        static string GetLogTail(string log, int maxLines)
        {
            if (string.IsNullOrWhiteSpace(log))
                return "(no build output — check that build.bat exists and VS 2022 C++ workload is installed)";

            var lines = log.Replace("\r\n", "\n").Split('\n');
            var start = System.Math.Max(0, lines.Length - maxLines);
            return string.Join("\n", lines, start, lines.Length - start);
        }
    }
}
#endif

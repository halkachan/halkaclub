using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Halka.Poc2D3D.EditorTools
{
    // Builds only the POC scene to a folder outside the repository. It never calls
    // ProjectBuilder, never touches halkaworld/webgl and does not change Build Settings.
    public static class PocBuilder
    {
        public const string OutputFolderName = "HARUKA_WORLD_ver3.0_PHASE2_PREVIEW";

        public static string OutputPath => Path.GetFullPath(Path.Combine(PocSceneBuilder.RepositoryRoot, "..", OutputFolderName));

        [MenuItem("HALKA/POC 2D3D/Build Phase 2 Preview WebGL outside repository")]
        public static void BuildFromMenu() => Build();

        public static void BuildBatch()
        {
            try
            {
                Build();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void Build()
        {
            var output = OutputPath;
            var publicWebGl = Path.GetFullPath(Path.Combine(PocSceneBuilder.RepositoryRoot, "halkaworld"));
            if (output.StartsWith(publicWebGl, StringComparison.OrdinalIgnoreCase) ||
                output.StartsWith(PocSceneBuilder.RepositoryRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("POC builds must stay outside the repository: " + output);
            if (EditorUserBuildSettings.development)
                throw new InvalidOperationException("Expected a non-development configuration");
            Directory.CreateDirectory(output);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { PocSceneBuilder.ScenePath },
                locationPathName = output,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("POC WebGL build failed: " + report.summary.result);
            var tools = Path.Combine(PocSceneBuilder.RepositoryRoot, "tools", "poc2d3d");
            File.Copy(Path.Combine(tools, "serve_poc.py"), Path.Combine(output, "serve_poc.py"), true);
            File.Copy(Path.Combine(tools, "README_POC.txt"), Path.Combine(output, "README_POC.txt"), true);
            // The shared WebGL template is not edited; only this outside-repository page gets the
            // user-facing name of the preview.
            var page = Path.Combine(output, "index.html");
            File.WriteAllText(page, File.ReadAllText(page).Replace("<title>HALKA WORLD</title>",
                "<title>" + PocDebugPanel.PreviewTitle + "</title>"));
            Debug.Log($"HALKA POC WebGL: {output} ({report.summary.totalSize} bytes, {report.summary.totalTime})");
        }
    }
}

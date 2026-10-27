using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Halka.Poc2D3D.EditorTools
{
    // Builds only the POC scene to a folder outside the repository. It never calls
    // ProjectBuilder, never touches halkaworld/webgl and does not change Build Settings.
    public static class PocBuilder
    {
        public const string OutputFolderName = "HARUKA_WORLD_ver3.0_PHASE4_PREVIEW";
        // Phase 5: はるかわーるど ver3.0 Release Candidate 1, outside the repository. The game sits
        // at halkaworld/webgl/ (the public site's path) below a staging copy of the game page.
        // Phase 6: RC2 in its own folder (RC1 is kept as it is). Phase 7: RC3 (RC1 and RC2 kept).
        // ver3.1: a local check build in its own folder (RC3 = the published ver3.0 is kept as it
        // is); the kit has no publishing files.
        public const string ReleaseCandidateFolderName = "HARUKA_WORLD_ver3.4_LOCAL";
        public const string ReleaseCandidateKit = "v34";
        // ver3.2: the chunk test build (generated test worlds, memory saves) in its own folder.
        public const string ChunkTestFolderName = "HARUKA_WORLD_ver3.2_CHUNKTEST";
        public const string ReleaseCandidateGamePath = "halkaworld/webgl";

        public static string ReleaseCandidatePath =>
            Path.GetFullPath(Path.Combine(PocSceneBuilder.RepositoryRoot, "..", ReleaseCandidateFolderName));

        public static string OutputPath => Path.GetFullPath(Path.Combine(PocSceneBuilder.RepositoryRoot, "..", OutputFolderName));

        [MenuItem("HALKA/POC 2D3D/Build Phase 4 Preview WebGL outside repository")]
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

        public static void BuildReleaseCandidateBatch()
        {
            try
            {
                BuildReleaseCandidate();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        // The RC folder: halkaworld/webgl (the game, RC scene only), the staging page beside it,
        // the start script, README and test documents (tools/poc2d3d/rc3 since Phase 7), and the
        // ver3.0 public page prepared for the release (release/halkaworld, not published).
        public static void BuildReleaseCandidate()
        {
            var root = ReleaseCandidatePath;
            var output = Path.Combine(root, ReleaseCandidateGamePath.Replace('/', Path.DirectorySeparatorChar));
            var report = BuildScene(PocSceneBuilder.RcScenePath, output);
            var page = Path.Combine(output, "index.html");
            // RC1 feedback: phones play upright again (no rotation guide), the title is はるかわーるど.
            File.WriteAllText(page, PreviewPage(File.ReadAllText(page), PocDebugPanel.ReleaseCandidateTitle, false));
            var kit = Path.Combine(PocSceneBuilder.RepositoryRoot, "tools", "poc2d3d", ReleaseCandidateKit);
            foreach (var file in Directory.GetFiles(kit, "*", SearchOption.AllDirectories))
            {
                var relative = file.Substring(kit.Length + 1);
                if (relative == "make_pages.py") continue;
                if (relative.StartsWith("staging" + Path.DirectorySeparatorChar)) relative = relative.Substring("staging".Length + 1);
                var target = Path.Combine(root, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file, target, true);
            }
            var build = BuildId(output);
            foreach (var built in new[] { Path.Combine(root, "halkaworld", "index.html"), Path.Combine(root, "release", "halkaworld", "index.html") })
                if (File.Exists(built)) File.WriteAllText(built, File.ReadAllText(built).Replace("__BUILD__", build));
            File.WriteAllText(Path.Combine(root, "BUILD_INFO.txt"),
                PocDebugPanel.ReleaseCandidateName + "\n" +
                "shown version: " + PocDebugPanel.ReleaseCandidateVersionLabel + " (menu, save files)\n" +
                "scene: " + PocSceneBuilder.RcScenePath + "\n" +
                "build: " + build + "\n" +
                "built: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n" +
                "size: " + report.summary.totalSize + " bytes (Unity report)\n" +
                "development build: " + EditorUserBuildSettings.development + ", script debugging: " + EditorUserBuildSettings.allowDebugging +
                ", options: " + report.summary.options + "\n" +
                "webgl: compression " + PlayerSettings.WebGL.compressionFormat + ", decompression fallback " + PlayerSettings.WebGL.decompressionFallback +
                ", exceptions " + PlayerSettings.WebGL.exceptionSupport + ", data caching " + PlayerSettings.WebGL.dataCaching +
                ", hashed file names " + PlayerSettings.WebGL.nameFilesAsHashes + ", template " + PlayerSettings.WebGL.template +
                ", splash " + PlayerSettings.SplashScreen.show + "\n");
            Debug.Log($"HALKA ver3.1 WebGL: {output} build {build} ({report.summary.totalSize} bytes, {report.summary.totalTime})");
        }

        // Like the public page's build= value: changes whenever the code or data files change.
        private static string BuildId(string output)
        {
            using (var sha = SHA256.Create())
            {
                var files = Directory.GetFiles(Path.Combine(output, "Build")).OrderBy(f => f, StringComparer.Ordinal);
                var all = files.SelectMany(f => sha.ComputeHash(File.ReadAllBytes(f))).ToArray();
                return BitConverter.ToString(sha.ComputeHash(all)).Replace("-", "").Substring(0, 32).ToLowerInvariant();
            }
        }

        // ver3.2: the chunk test scene, built outside the repository (development: true adds the
        // profiler counters such as draw calls, for that measurement only).
        public static void BuildChunkTestBatch() => BuildChunkTestBatch(false);
        public static void BuildChunkTestDevelopmentBatch() => BuildChunkTestBatch(true);

        private static void BuildChunkTestBatch(bool development)
        {
            try
            {
                PocSceneBuilder.BuildChunkTestScene();
                var root = Path.GetFullPath(Path.Combine(PocSceneBuilder.RepositoryRoot, "..", ChunkTestFolderName));
                var output = Path.Combine(root, development ? "webgl_dev" : "webgl");
                var report = BuildScene(PocSceneBuilder.ChunkTestScenePath, output, development);
                var page = Path.Combine(output, "index.html");
                File.WriteAllText(page, PreviewPage(File.ReadAllText(page), "はるかわーるど ver3.2 chunk test", false));
                File.WriteAllText(Path.Combine(root, development ? "BUILD_INFO_dev.txt" : "BUILD_INFO.txt"),
                    "はるかわーるど ver3.2 chunk test build (" + (development ? "development" : "release settings") + ")\n" +
                    "not for publishing; saves stay in memory (no browser save is read or written)\n" +
                    "URL: webgl/index.html?world=32|64|128|256&chunk=8..64&debug=1\n" +
                    "build: " + BuildId(output) + "\nsize: " + report.summary.totalSize + " bytes\n");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static BuildReport BuildScene(string scene, string output) => BuildScene(scene, output, false);

        private static BuildReport BuildScene(string scene, string output, bool development)
        {
            var publicWebGl = Path.GetFullPath(Path.Combine(PocSceneBuilder.RepositoryRoot, "halkaworld"));
            if (output.StartsWith(publicWebGl, StringComparison.OrdinalIgnoreCase) ||
                output.StartsWith(PocSceneBuilder.RepositoryRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("POC builds must stay outside the repository: " + output);
            if (!development && (EditorUserBuildSettings.development || EditorUserBuildSettings.allowDebugging))
                throw new InvalidOperationException("Expected a non-development configuration (no script debugging)");
            Directory.CreateDirectory(output);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { scene },
                locationPathName = output,
                target = BuildTarget.WebGL,
                options = development ? BuildOptions.Development : BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("POC WebGL build failed: " + report.summary.result);
            return report;
        }

        public static void Build()
        {
            var output = OutputPath;
            var report = BuildScene(PocSceneBuilder.ScenePath, output);
            var tools = Path.Combine(PocSceneBuilder.RepositoryRoot, "tools", "poc2d3d");
            File.Copy(Path.Combine(tools, "serve_poc.py"), Path.Combine(output, "serve_poc.py"), true);
            File.Copy(Path.Combine(tools, "README_POC.txt"), Path.Combine(output, "README_POC.txt"), true);
            // The shared WebGL template is not edited; only this outside-repository page gets the
            // user-facing name of the preview and the Phase 4 phone page (landscape guide, safe area).
            var page = Path.Combine(output, "index.html");
            File.WriteAllText(page, PreviewPage(File.ReadAllText(page)));
            Debug.Log($"HALKA POC WebGL: {output} ({report.summary.totalSize} bytes, {report.summary.totalTime})");
        }

        // The page the template produced, plus: the preview title; on phones and tablets held
        // upright a rotation guide over the game ("スマホを横向きにしてください"; the game itself
        // pauses, see PocOrientationGuard; the guide uses the same touch rule as HalkaTouchUiMode);
        // the canvas kept inside the safe area (notch, home indicator); one try of the Screen
        // Orientation API on the first touch where a browser allows it. Desktop layout unchanged.
        public static string PreviewPage(string html) => PreviewPage(html, PocDebugPanel.PreviewTitle);

        public static string PreviewPage(string html, string title) => PreviewPage(html, title, true);

        // landscapeGuide false (the RC): any orientation is played, the canvas still keeps inside
        // the safe area and follows turns and full screen (resize events).
        public static string PreviewPage(string html, string title, bool landscapeGuide)
        {
            html = html.Replace("\r\n", "\n");
            html = ReplaceOnce(html, "<title>HALKA WORLD</title>", "<title>" + title + "</title>");
            html = ReplaceOnce(html, "    </style>", (landscapeGuide ? GuideStyle : ProbeStyle) + "    </style>");
            html = ReplaceOnce(html, "    <div id=\"loading\">", (landscapeGuide ? GuideMarkup : ProbeMarkup) + "    <div id=\"loading\">");
            html = ReplaceOnce(html, TemplateLayout, landscapeGuide ? PreviewLayout : AnyOrientationLayout);
            return html;
        }

        private const string ProbeStyle =
@"      #safe-area-probe { position: fixed; inset: 0; box-sizing: border-box; visibility: hidden; pointer-events: none;
        padding: env(safe-area-inset-top, 0px) env(safe-area-inset-right, 0px) env(safe-area-inset-bottom, 0px) env(safe-area-inset-left, 0px); }
";

        private const string ProbeMarkup =
@"    <div id=""safe-area-probe"" aria-hidden=""true""></div>
";

        private const string AnyOrientationLayout =
@"      // はるかわーるど: phones are played upright (as in the 2D game) or sideways; the canvas
      // stays inside the safe area (notch, home indicator) and follows turns and full screen.
      // On a large landscape screen, keep the 576-pixel world projection at exactly one CSS
      // pixel per authored sprite pixel, centred with integer offsets.
      const safeProbe = document.getElementById(""safe-area-probe"");
      function safeInsets() {
        const style = getComputedStyle(safeProbe);
        const px = (value) => Math.max(0, parseFloat(value) || 0);
        return { top: px(style.paddingTop), right: px(style.paddingRight), bottom: px(style.paddingBottom), left: px(style.paddingLeft) };
      }
      function layoutCanvas() {
        const inset = safeInsets();
        const width = window.innerWidth - inset.left - inset.right;
        const height = window.innerHeight - inset.top - inset.bottom;
        const desktop = width >= 800 && height >= 576 && width >= height;
        // Full screen: this page's own, or the game page's frame made full screen by its button.
        const filling = document.fullscreenElement || (window.innerWidth >= screen.width - 1 && window.innerHeight >= screen.height - 1);
        if (!desktop || filling) {
          canvas.style.left = `${inset.left}px`;
          canvas.style.top = `${inset.top}px`;
          canvas.style.width = `calc(100% - ${inset.left + inset.right}px)`;
          canvas.style.height = `calc(100% - ${inset.top + inset.bottom}px)`;
          return;
        }
        const scale = Math.min(1, 576 / height);
        const canvasWidth = Math.floor(width * scale);
        const canvasHeight = Math.floor(height * scale);
        canvas.style.width = `${canvasWidth}px`;
        canvas.style.height = `${canvasHeight}px`;
        canvas.style.left = `${Math.floor(inset.left + (width - canvasWidth) / 2)}px`;
        canvas.style.top = `${Math.floor(inset.top + (height - canvasHeight) / 2)}px`;
      }
      window.addEventListener(""resize"", layoutCanvas);
      window.addEventListener(""orientationchange"", layoutCanvas);
      document.addEventListener(""fullscreenchange"", layoutCanvas);";

        // Line endings follow the checkout; both sides are compared with LF.
        private static string ReplaceOnce(string text, string find, string replacement)
        {
            find = find.Replace("\r\n", "\n");
            replacement = replacement.Replace("\r\n", "\n");
            var at = text.IndexOf(find, StringComparison.Ordinal);
            if (at < 0 || text.IndexOf(find, at + find.Length, StringComparison.Ordinal) >= 0)
                throw new InvalidOperationException("The WebGL template page changed; expected once: " + find);
            return text.Substring(0, at) + replacement + text.Substring(at + find.Length);
        }

        private const string GuideStyle =
@"      #safe-area-probe { position: fixed; inset: 0; box-sizing: border-box; visibility: hidden; pointer-events: none;
        padding: env(safe-area-inset-top, 0px) env(safe-area-inset-right, 0px) env(safe-area-inset-bottom, 0px) env(safe-area-inset-left, 0px); }
      #rotate-guide { position: fixed; inset: 0; z-index: 10; display: flex; flex-direction: column; align-items: center;
        justify-content: center; gap: 20px; padding: 24px; box-sizing: border-box; background: #e7eecf; color: #3f4733;
        font: 700 19px/1.6 ""Hiragino Maru Gothic ProN"", ""BIZ UDPGothic"", ""Yu Gothic"", ""Noto Sans JP"", sans-serif;
        text-align: center; touch-action: none; user-select: none; -webkit-user-select: none; }
      #rotate-guide[hidden] { display: none; }
      #rotate-guide svg { width: 112px; height: 112px; }
";

        private const string GuideMarkup =
@"    <div id=""safe-area-probe"" aria-hidden=""true""></div>
    <div id=""rotate-guide"" role=""alert"" hidden>
      <svg viewBox=""0 0 112 112"" aria-hidden=""true"" fill=""none"" stroke=""currentColor"" stroke-linecap=""round"" stroke-linejoin=""round"">
        <rect x=""12"" y=""14"" width=""34"" height=""58"" rx=""6"" stroke-width=""5"" opacity=""0.38"" />
        <rect x=""40"" y=""64"" width=""60"" height=""34"" rx=""6"" stroke-width=""5"" />
        <path d=""M56 18 C74 18 86 30 88 46"" stroke-width=""5"" />
        <path d=""M80 40 L88 49 L96 40"" stroke-width=""5"" />
      </svg>
      <div>スマホを横向きにしてください</div>
    </div>
";

        private const string TemplateLayout =
@"      // On a large landscape screen, keep the 576-pixel world projection at
      // exactly one CSS pixel per authored sprite pixel. Center with integer
      // offsets, leaving the existing mobile layout untouched.
      function layoutCanvas() {
        const width = window.innerWidth;
        const height = window.innerHeight;
        const desktop = width >= 800 && height >= 576 && width >= height;
        const scale = desktop ? Math.min(1, 576 / height) : 1;
        const canvasWidth = Math.floor(width * scale);
        const canvasHeight = Math.floor(height * scale);
        canvas.style.width = `${canvasWidth}px`;
        canvas.style.height = `${canvasHeight}px`;
        canvas.style.left = `${Math.floor((width - canvasWidth) / 2)}px`;
        canvas.style.top = `${Math.floor((height - canvasHeight) / 2)}px`;
      }";

        private const string PreviewLayout =
@"      // Phase 4 Preview: phones are played in landscape. The same touch rule as the game's
      // touch controls (HalkaTouchUiMode); upright, a guide covers the paused game.
      const rotateGuide = document.getElementById(""rotate-guide"");
      const safeProbe = document.getElementById(""safe-area-probe"");
      const touchMode = new URLSearchParams(window.location.search).get(""touchControls"") === ""1"" ? 2 :
        navigator.maxTouchPoints > 0 && window.matchMedia && window.matchMedia(""(pointer: coarse)"").matches ? 1 : 0;
      function safeInsets() {
        const style = getComputedStyle(safeProbe);
        const px = (value) => Math.max(0, parseFloat(value) || 0);
        return { top: px(style.paddingTop), right: px(style.paddingRight), bottom: px(style.paddingBottom), left: px(style.paddingLeft) };
      }
      function updateRotateGuide() {
        rotateGuide.hidden = !(touchMode > 0 && window.innerHeight > window.innerWidth);
      }
      // Where a browser allows it (mostly Android in fullscreen or an installed app) ask for
      // landscape once; elsewhere the request is refused quietly and the guide does the job.
      function tryLockLandscape() {
        try {
          const request = screen.orientation && screen.orientation.lock ? screen.orientation.lock(""landscape"") : null;
          if (request && request.catch) request.catch(() => {});
        } catch (ignored) {}
      }
      if (touchMode > 0) window.addEventListener(""pointerup"", tryLockLandscape, { once: true });
      // On a large landscape screen, keep the 576-pixel world projection at
      // exactly one CSS pixel per authored sprite pixel. Center with integer
      // offsets, leaving the existing mobile layout untouched. The canvas stays
      // inside the safe area (notch, home indicator); the insets are zero on desktops.
      // Phones size it relative to the window, so a turn never leaves a canvas wider than the
      // new window (which made the browser zoom the page out).
      function layoutCanvas() {
        const inset = safeInsets();
        const width = window.innerWidth - inset.left - inset.right;
        const height = window.innerHeight - inset.top - inset.bottom;
        const desktop = width >= 800 && height >= 576 && width >= height;
        if (!desktop) {
          canvas.style.left = `${inset.left}px`;
          canvas.style.top = `${inset.top}px`;
          canvas.style.width = `calc(100% - ${inset.left + inset.right}px)`;
          canvas.style.height = `calc(100% - ${inset.top + inset.bottom}px)`;
          updateRotateGuide();
          return;
        }
        const scale = Math.min(1, 576 / height);
        const canvasWidth = Math.floor(width * scale);
        const canvasHeight = Math.floor(height * scale);
        canvas.style.width = `${canvasWidth}px`;
        canvas.style.height = `${canvasHeight}px`;
        canvas.style.left = `${Math.floor(inset.left + (width - canvasWidth) / 2)}px`;
        canvas.style.top = `${Math.floor(inset.top + (height - canvasHeight) / 2)}px`;
        updateRotateGuide();
      }
      window.addEventListener(""resize"", layoutCanvas);
      window.addEventListener(""orientationchange"", layoutCanvas);";
    }
}

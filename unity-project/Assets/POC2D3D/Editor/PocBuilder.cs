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
        public const string OutputFolderName = "HARUKA_WORLD_ver3.0_PHASE4_PREVIEW";

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
        public static string PreviewPage(string html)
        {
            html = html.Replace("\r\n", "\n");
            html = ReplaceOnce(html, "<title>HALKA WORLD</title>", "<title>" + PocDebugPanel.PreviewTitle + "</title>");
            html = ReplaceOnce(html, "    </style>", GuideStyle + "    </style>");
            html = ReplaceOnce(html, "    <div id=\"loading\">", GuideMarkup + "    <div id=\"loading\">");
            html = ReplaceOnce(html, TemplateLayout, PreviewLayout);
            return html;
        }

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
      function layoutCanvas() {
        const inset = safeInsets();
        const width = window.innerWidth - inset.left - inset.right;
        const height = window.innerHeight - inset.top - inset.bottom;
        const desktop = width >= 800 && height >= 576 && width >= height;
        const scale = desktop ? Math.min(1, 576 / height) : 1;
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

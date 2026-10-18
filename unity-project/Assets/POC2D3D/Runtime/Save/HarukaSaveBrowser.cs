using UnityEngine;

namespace Halka.Poc2D3D.Save
{
    public enum HarukaImportState { Idle = 0, Waiting = 1, Cancelled = 2, Ready = 3, Failed = 4 }

    // The browser side of saving (HarukaSave.jslib in WebGL): download, file chooser, page
    // hidden, the multi-tab revision marker and the storage sync. Elsewhere (Editor, checks)
    // HarukaEditorSaveBrowser records what would happen and replays chosen files.
    public interface IHarukaSaveBrowser
    {
        // Rect: the button in GUI pixels (origin top-left); an empty rect disarms.
        void ArmExport(string json, Rect guiRect);
        void ExportPressed();
        int TakeExportResult();          // 0 nothing yet, 1 handed to the browser, 2 failed
        void ArmImport(Rect guiRect);
        void ImportPressed();
        HarukaImportState ImportState { get; }
        string TakeImportText();
        void ResetImport();
        long ReadRevisionMarker();
        void WriteRevisionMarker(long revision);
        void Sync();
    }

    public sealed class HarukaWebGLSaveBrowser : IHarukaSaveBrowser
    {
        public const string FilePrefix = "harukaworld_save_";
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")] private static extern void HarukaSaveInstall(int maxBytes);
        [System.Runtime.InteropServices.DllImport("__Internal")] private static extern void HarukaSaveArmExport(string json, string prefix, float x, float y, float w, float h);
        [System.Runtime.InteropServices.DllImport("__Internal")] private static extern int HarukaSaveTakeExportResult();
        [System.Runtime.InteropServices.DllImport("__Internal")] private static extern void HarukaSaveArmImport(float x, float y, float w, float h);
        [System.Runtime.InteropServices.DllImport("__Internal")] private static extern int HarukaSaveImportState();
        [System.Runtime.InteropServices.DllImport("__Internal")] private static extern string HarukaSaveTakeImportText();
        [System.Runtime.InteropServices.DllImport("__Internal")] private static extern void HarukaSaveResetImport();
        [System.Runtime.InteropServices.DllImport("__Internal")] private static extern int HarukaSaveReadMarker();
        [System.Runtime.InteropServices.DllImport("__Internal")] private static extern void HarukaSaveWriteMarker(int revision);
        [System.Runtime.InteropServices.DllImport("__Internal")] private static extern void HarukaSaveSync();

        private string armedJson;

        public HarukaWebGLSaveBrowser() => HarukaSaveInstall(HarukaSaveData.MaxFileBytes);

        public void ArmExport(string json, Rect r)
        {
            if (r.width <= 0f || string.IsNullOrEmpty(json)) { if (armedJson != null) HarukaSaveArmExport("", FilePrefix, 0, 0, 0, 0); armedJson = null; return; }
            // The page keeps its own copy; send it again only when it changed.
            if (json != armedJson) armedJson = json;
            HarukaSaveArmExport(json, FilePrefix, r.x, r.y, r.width, r.height);
        }
        public void ExportPressed() { }
        public int TakeExportResult() => HarukaSaveTakeExportResult();
        public void ArmImport(Rect r) => HarukaSaveArmImport(r.x, r.y, r.width, r.height);
        public void ImportPressed() { }
        public HarukaImportState ImportState => (HarukaImportState)HarukaSaveImportState();
        public string TakeImportText() => HarukaSaveTakeImportText();
        public void ResetImport() => HarukaSaveResetImport();
        public long ReadRevisionMarker() => HarukaSaveReadMarker();
        public void WriteRevisionMarker(long revision) => HarukaSaveWriteMarker((int)Mathf.Min(revision, int.MaxValue));
        public void Sync() => HarukaSaveSync();
#else
        public void ArmExport(string json, Rect r) { }
        public void ExportPressed() { }
        public int TakeExportResult() => 2;
        public void ArmImport(Rect r) { }
        public void ImportPressed() { }
        public HarukaImportState ImportState => HarukaImportState.Idle;
        public string TakeImportText() => "";
        public void ResetImport() { }
        public long ReadRevisionMarker() => -1;
        public void WriteRevisionMarker(long revision) { }
        public void Sync() { }
#endif
    }

    // Editor and checks: an export "downloads" into Exported; a pressed インポート takes the
    // next text from NextImport (null: the person cancels the chooser).
    public sealed class HarukaEditorSaveBrowser : IHarukaSaveBrowser
    {
        public string ArmedJson { get; private set; }
        public Rect ArmedExportRect { get; private set; }
        public Rect ArmedImportRect { get; private set; }
        public string Exported { get; private set; }
        public int ExportCount { get; private set; }
        public bool FailNextExport { get; set; }
        public string NextImport { get; set; }
        public bool ImportTooLarge { get; set; }
        public long Marker { get; set; } = -1;
        public int Syncs { get; private set; }
        private int exportResult;
        private string importText;

        public HarukaImportState ImportState { get; private set; }

        public void ArmExport(string json, Rect r) { ArmedJson = r.width > 0f ? json : null; ArmedExportRect = r; }

        public void ExportPressed()
        {
            if (ArmedJson == null) return;
            if (FailNextExport) { FailNextExport = false; exportResult = 2; return; }
            Exported = ArmedJson;
            ExportCount++;
            exportResult = 1;
        }

        public int TakeExportResult() { var r = exportResult; exportResult = 0; return r; }
        public void ArmImport(Rect r) => ArmedImportRect = r;

        public void ImportPressed()
        {
            if (ImportTooLarge) { ImportState = HarukaImportState.Failed; return; }
            if (NextImport == null) { ImportState = HarukaImportState.Cancelled; return; }
            importText = NextImport;
            ImportState = HarukaImportState.Ready;
        }

        public string TakeImportText() { var t = importText ?? ""; importText = null; ImportState = HarukaImportState.Idle; return t; }
        public void ResetImport() { importText = null; ImportState = HarukaImportState.Idle; }
        public long ReadRevisionMarker() => Marker;
        public void WriteRevisionMarker(long revision) => Marker = revision;
        public void Sync() => Syncs++;
    }
}

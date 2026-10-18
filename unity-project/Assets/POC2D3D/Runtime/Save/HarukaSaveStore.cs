using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Halka.Poc2D3D.Save
{
    // Where save texts live: one file per name. The game uses files under
    // Application.persistentDataPath (in WebGL that is the page's IndexedDB storage, kept per
    // origin and page path like PlayerPrefs); the checks use memory.
    public interface IHarukaSaveStorage
    {
        string Read(string name);
        bool Write(string name, string text);
    }

    public sealed class HarukaFileStorage : IHarukaSaveStorage
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);
        public string Directory { get; }

        public HarukaFileStorage(string directory) { Directory = directory; }

        public string Read(string name)
        {
            try
            {
                var path = Path.Combine(Directory, name);
                return File.Exists(path) ? File.ReadAllText(path, Utf8) : null;
            }
            catch (Exception) { return null; }
        }

        public bool Write(string name, string text)
        {
            try
            {
                System.IO.Directory.CreateDirectory(Directory);
                File.WriteAllText(Path.Combine(Directory, name), text, Utf8);
                return true;
            }
            catch (Exception) { return false; }
        }
    }

    public sealed class HarukaMemoryStorage : IHarukaSaveStorage
    {
        public readonly Dictionary<string, string> Files = new Dictionary<string, string>();
        public bool FailWrites { get; set; }
        // A write cut off midway (power loss, a closed tab): only part of the text arrives.
        public int TruncateNextWriteTo { get; set; } = -1;

        public string Read(string name) => Files.TryGetValue(name, out var text) ? text : null;

        public bool Write(string name, string text)
        {
            if (FailWrites) return false;
            if (TruncateNextWriteTo >= 0)
            {
                Files[name] = text.Substring(0, Math.Min(text.Length, TruncateNextWriteTo));
                TruncateNextWriteTo = -1;
                return true;
            }
            Files[name] = text;
            return true;
        }
    }

    public sealed class HarukaSnapshotInfo
    {
        public string Name;
        public HarukaSaveReadStatus Status;
        public HarukaSaveData Data;
        public string Problem;
    }

    public sealed class HarukaSnapshotScan
    {
        public readonly List<HarukaSnapshotInfo> Slots = new List<HarukaSnapshotInfo>();
        public HarukaSaveData Latest;
        public long HeadRevision = -1;
        public long HighestRevision => Math.Max(HeadRevision, Latest?.Revision ?? -1);
        public bool HasNewerSchema => Slots.Exists(s => s.Status == HarukaSaveReadStatus.NewerSchema);
        public int CorruptCount => Slots.FindAll(s => s.Status == HarukaSaveReadStatus.Corrupt).Count;
        public bool AnyFile => Slots.Exists(s => s.Status != HarukaSaveReadStatus.Missing);
        // The head names a later save than the newest readable one: that save was damaged or cut
        // off, and the game goes on from an older generation (the player is told).
        public bool RecoveredFromBackup => Latest != null && HeadRevision > Latest.Revision;
    }

    // Three generations of complete snapshots. A new save goes into the slot of the oldest (or
    // an empty / damaged one), never over the newest good one, then it is read back and checked;
    // only then the head moves. A cut-off write loses at most that one new snapshot. A damaged
    // file is copied aside (damaged_N.json) before its slot is reused, never just deleted.
    public sealed class HarukaSnapshotStore
    {
        public const int Generations = 3;
        public const string HeadName = "head.json";
        public const string RecoveryName = "import_recovery.json";
        public static readonly string[] SlotNames = { "snapshot_1.json", "snapshot_2.json", "snapshot_3.json" };

        private readonly IHarukaSaveStorage storage;
        public IHarukaSaveStorage Storage => storage;
        public string LastProblem { get; private set; }

        public HarukaSnapshotStore(IHarukaSaveStorage store) { storage = store; }

        public HarukaSnapshotScan Scan()
        {
            var scan = new HarukaSnapshotScan();
            foreach (var name in SlotNames)
            {
                var info = new HarukaSnapshotInfo { Name = name };
                info.Status = HarukaSaveData.TryRead(storage.Read(name), out info.Data, out info.Problem);
                scan.Slots.Add(info);
                if (info.Status == HarukaSaveReadStatus.Valid && (scan.Latest == null || info.Data.Revision > scan.Latest.Revision))
                    scan.Latest = info.Data;
            }
            scan.HeadRevision = ReadHead();
            return scan;
        }

        private long ReadHead()
        {
            var text = storage.Read(HeadName);
            if (!HarukaSaveJson.TryParse(text, out var root, out _) || !(root is Dictionary<string, object> o) ||
                !o.TryGetValue("revision", out var value) || !(value is long revision) || revision < 0)
                return -1;
            return revision;
        }

        // Writes data as the next revision (at least minimumRevision). Returns the written copy,
        // or null (nothing changed for the newest good snapshot) with LastProblem set.
        public HarukaSaveData Write(HarukaSaveData data, long minimumRevision = 0)
        {
            LastProblem = null;
            var scan = Scan();
            if (scan.HasNewerSchema) { LastProblem = "a newer schema is stored"; return null; }
            var copy = data.Copy();
            copy.Revision = Math.Max(scan.HighestRevision + 1, Math.Max(minimumRevision, 1));
            copy.Seal();
            var text = copy.ToJson();
            if (HarukaSaveData.TryRead(text, out var check, out var problem) != HarukaSaveReadStatus.Valid ||
                check.Revision != copy.Revision)
            {
                LastProblem = "candidate invalid: " + problem;
                return null;
            }
            var target = ChooseSlot(scan);
            if (target.Status == HarukaSaveReadStatus.Corrupt) Quarantine(storage.Read(target.Name));
            if (!storage.Write(target.Name, text)) { LastProblem = "write failed"; return null; }
            if (HarukaSaveData.TryRead(storage.Read(target.Name), out var back, out problem) != HarukaSaveReadStatus.Valid ||
                back.Revision != copy.Revision)
            {
                LastProblem = "read back failed: " + problem;
                return null;
            }
            storage.Write(HeadName, "{ \"revision\": " + copy.Revision.ToString(CultureInfo.InvariantCulture) + " }\n");
            return copy;
        }

        private static HarukaSnapshotInfo ChooseSlot(HarukaSnapshotScan scan)
        {
            foreach (var slot in scan.Slots) if (slot.Status == HarukaSaveReadStatus.Missing) return slot;
            foreach (var slot in scan.Slots) if (slot.Status == HarukaSaveReadStatus.Corrupt) return slot;
            HarukaSnapshotInfo oldest = null;
            foreach (var slot in scan.Slots)
                if (slot.Data != scan.Latest && (oldest == null || slot.Data.Revision < oldest.Data.Revision)) oldest = slot;
            return oldest ?? scan.Slots[0];
        }

        private void Quarantine(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            // Up to three damaged copies; the oldest of them makes room.
            string target = null;
            for (var i = 1; i <= 3 && target == null; i++)
                if (storage.Read("damaged_" + i + ".json") == null) target = "damaged_" + i + ".json";
            if (target == null)
            {
                var next = ReadDamagedCursor() % 3 + 1;
                target = "damaged_" + next + ".json";
                storage.Write("damaged_cursor.txt", next.ToString(CultureInfo.InvariantCulture));
            }
            storage.Write(target, text);
        }

        private int ReadDamagedCursor()
        {
            var text = storage.Read("damaged_cursor.txt");
            return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n >= 1 && n <= 3 ? n : 0;
        }

        // Before an import replaces the save: the current state, kept until the next import.
        public bool WriteRecovery(HarukaSaveData data)
        {
            var copy = data.Copy();
            copy.Seal();
            var text = copy.ToJson();
            return storage.Write(RecoveryName, text) &&
                HarukaSaveData.TryRead(storage.Read(RecoveryName), out _, out _) == HarukaSaveReadStatus.Valid;
        }

        public HarukaSaveData ReadRecovery() =>
            HarukaSaveData.TryRead(storage.Read(RecoveryName), out var data, out _) == HarukaSaveReadStatus.Valid ? data : null;
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Lucerne.ProjectSites.Sync
{
    public class SyncRun
    {
        public string Job { get; set; }
        public DateTime StartedUtc { get; set; }
        public DateTime FinishedUtc { get; set; }
        public bool Succeeded { get; set; }
        public int Created { get; set; }
        public int Updated { get; set; }
        public int Failed { get; set; }
        public string Message { get; set; }
    }

    /// <summary>Watermarks and run history. SQL (EF6) in production, a JSON file or memory for development.</summary>
    public interface ISyncStateStore
    {
        DateTime? GetWatermark(string name);
        void SetWatermark(string name, DateTime valueUtc);
        void RecordRun(SyncRun run);
    }

    public class InMemorySyncStateStore : ISyncStateStore
    {
        private readonly Dictionary<string, DateTime> _watermarks = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        public InMemorySyncStateStore()
        {
            Runs = new List<SyncRun>();
        }

        public List<SyncRun> Runs { get; private set; }

        public DateTime? GetWatermark(string name)
        {
            DateTime v;
            return _watermarks.TryGetValue(name, out v) ? v : (DateTime?)null;
        }

        public void SetWatermark(string name, DateTime valueUtc)
        {
            _watermarks[name] = valueUtc;
        }

        public void RecordRun(SyncRun run)
        {
            Runs.Add(run);
        }
    }

    public class JsonFileSyncStateStore : ISyncStateStore
    {
        private readonly string _path;
        private readonly object _gate = new object();

        public JsonFileSyncStateStore(string path)
        {
            _path = path;
        }

        public DateTime? GetWatermark(string name)
        {
            lock (_gate)
            {
                DateTime v;
                return Load().Watermarks.TryGetValue(name, out v) ? v : (DateTime?)null;
            }
        }

        public void SetWatermark(string name, DateTime valueUtc)
        {
            lock (_gate)
            {
                var state = Load();
                state.Watermarks[name] = valueUtc;
                Save(state);
            }
        }

        public void RecordRun(SyncRun run)
        {
            lock (_gate)
            {
                var state = Load();
                state.Runs.Add(run);
                if (state.Runs.Count > 200)
                {
                    state.Runs.RemoveRange(0, state.Runs.Count - 200);
                }

                Save(state);
            }
        }

        private FileState Load()
        {
            return File.Exists(_path) ? JsonConvert.DeserializeObject<FileState>(File.ReadAllText(_path)) : new FileState();
        }

        private void Save(FileState state)
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(_path));
            Directory.CreateDirectory(dir);
            var tmp = _path + ".tmp";
            File.WriteAllText(tmp, JsonConvert.SerializeObject(state, Formatting.Indented));
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }

            File.Move(tmp, _path);
        }

        private class FileState
        {
            public Dictionary<string, DateTime> Watermarks { get; set; } = new Dictionary<string, DateTime>();
            public List<SyncRun> Runs { get; set; } = new List<SyncRun>();
        }
    }
}

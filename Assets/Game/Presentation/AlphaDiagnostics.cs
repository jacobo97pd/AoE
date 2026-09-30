using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Emberfield.Presentation
{
    public enum AlphaDiagnosticKind
    {
        SessionStarted, SessionClosed, PreviousSessionInterrupted, MatchStarted, MatchFinished,
        TutorialStep, ErrorObserved, ExceptionObserved, AssertionObserved, NetworkInterrupted, NetworkResumed
    }
    [Serializable]
    public sealed class AlphaDiagnosticRecord
    {
        public AlphaDiagnosticKind Kind;
        public long UtcSeconds;
        public long Tick;
        public int Value;
    }
    [Serializable]
    public sealed class AlphaDiagnosticEnvelope
    {
        public int Version = 1;
        public AlphaDiagnosticRecord[] Records = Array.Empty<AlphaDiagnosticRecord>();
    }

    // Typed local counters only. Never accepts raw logs, stack traces, account tokens, names or device IDs.
    // The marker detects an unclean exit, which is not proof of a native crash. Nothing is uploaded.
    public sealed class AlphaDiagnostics : IDisposable
    {
        public const int Capacity = 128, MaximumFileBytes = 65536;
        private readonly string recordsPath, markerPath, exportPath;
        private readonly List<AlphaDiagnosticRecord> records = new List<AlphaDiagnosticRecord>(Capacity);
        private bool disposed;
        public bool Consented { get; private set; }
        public bool StorageAvailable { get; private set; } = true;
        public IReadOnlyList<AlphaDiagnosticRecord> Records => records.AsReadOnly();
        public string ExportPath => exportPath;
        public AlphaDiagnostics(string directory, bool consent)
        {
            if (string.IsNullOrEmpty(directory)) throw new ArgumentException("Diagnostics require a directory.", nameof(directory));
            recordsPath = Path.Combine(directory, "diagnostics.json");
            markerPath = Path.Combine(directory, "session-active.json");
            exportPath = Path.Combine(directory, "diagnostics-export.json");
            if (consent) SetConsent(true);
            else Clear();
        }
        public void SetConsent(bool enabled)
        {
            if (disposed || Consented == enabled) return;
            if (!enabled) { Consented = false; Clear(); return; }
            Consented = true;
            ReadRecords();
            bool interrupted = File.Exists(markerPath);
            StorageAvailable &= AlphaLocalFiles.Write(markerPath, "{\"Version\":1}");
            if (interrupted) Record(AlphaDiagnosticKind.PreviousSessionInterrupted);
            Record(AlphaDiagnosticKind.SessionStarted);
        }
        public void Record(AlphaDiagnosticKind kind, long tick = 0, int value = 0)
        {
            if (!Consented || disposed || !Enum.IsDefined(typeof(AlphaDiagnosticKind), kind)) return;
            if (records.Count == Capacity) records.RemoveAt(0);
            records.Add(new AlphaDiagnosticRecord { Kind = kind, UtcSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), Tick = Math.Max(0, tick), Value = Mathf.Clamp(value, -1000000, 1000000) });
            StorageAvailable &= Flush();
        }
        public bool Clear()
        {
            records.Clear();
            bool okay = AlphaLocalFiles.Delete(recordsPath) & AlphaLocalFiles.Delete(markerPath) & AlphaLocalFiles.Delete(exportPath);
            if (Consented && !disposed) okay &= AlphaLocalFiles.Write(markerPath, "{\"Version\":1}");
            return StorageAvailable = okay;
        }
        public bool Export()
        {
            if (!Consented || disposed) return false;
            return StorageAvailable = AlphaLocalFiles.Write(exportPath, JsonUtility.ToJson(new AlphaDiagnosticEnvelope { Records = records.ToArray() }, true));
        }
        private bool Flush() => AlphaLocalFiles.Write(recordsPath, JsonUtility.ToJson(new AlphaDiagnosticEnvelope { Records = records.ToArray() }, false));
        private void ReadRecords()
        {
            records.Clear();
            try
            {
                if (!File.Exists(recordsPath) || new FileInfo(recordsPath).Length > MaximumFileBytes) return;
                var envelope = JsonUtility.FromJson<AlphaDiagnosticEnvelope>(File.ReadAllText(recordsPath));
                if (envelope == null || envelope.Version != 1 || envelope.Records == null) return;
                for (int i = Math.Max(0, envelope.Records.Length - Capacity); i < envelope.Records.Length; i++)
                {
                    var item = envelope.Records[i];
                    if (item != null && Enum.IsDefined(typeof(AlphaDiagnosticKind), item.Kind) && item.Tick >= 0 && item.UtcSeconds >= 0 && Math.Abs((long)item.Value) <= 1000000)
                        records.Add(item);
                }
            }
            catch (Exception error) when (AlphaLocalFiles.IsStorageError(error) || error is ArgumentException) { records.Clear(); }
        }
        public void Dispose()
        {
            if (disposed) return;
            if (Consented) { Record(AlphaDiagnosticKind.SessionClosed); StorageAvailable &= AlphaLocalFiles.Delete(markerPath); }
            disposed = true;
        }
    }
}

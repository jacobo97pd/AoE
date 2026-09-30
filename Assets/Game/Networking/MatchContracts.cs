using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Emberfield.Networking
{
    public interface IMatchTransport
    {
        bool IsConnected { get; }
        Task<string> ExchangeAsync(string resource, string method, string json, CancellationToken cancellation);
    }
    public interface IMatchClock { long Tick { get; } int TickRate { get; } }
    public interface IMatchResultService { Task<NetworkResultReceipt> ReadResultAsync(); }
    [Serializable] public sealed class NetworkResultReceipt { public int WinnerPlayerId; public string Reason; public long Tick; public double DurationSeconds; }
    public interface IPlayerCommandQueue
    {
        int Count { get; }
        bool TryEnqueue(NetworkCommandEnvelope command);
        bool TryDequeue(out NetworkCommandEnvelope command);
        void Clear();
    }
    public sealed class PlayerCommandQueue : IPlayerCommandQueue
    {
        public const int Capacity = 32;
        private readonly Queue<NetworkCommandEnvelope> pending = new Queue<NetworkCommandEnvelope>();
        public int Count => pending.Count;
        public bool TryEnqueue(NetworkCommandEnvelope command)
        {
            if (command == null || pending.Count >= Capacity) return false;
            pending.Enqueue(command); return true;
        }
        public bool TryDequeue(out NetworkCommandEnvelope command)
        { command = pending.Count > 0 ? pending.Dequeue() : null; return command != null; }
        public void Clear() => pending.Clear();
    }
}

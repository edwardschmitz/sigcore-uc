using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace SigCoreCommon {
    internal sealed class SigCoreCoordinator : IDisposable {
        private readonly SigCoreClient _client;
        private readonly ConcurrentDictionary<ulong, TaskCompletionSource<JObject>> _pending =
            new ConcurrentDictionary<ulong, TaskCompletionSource<JObject>>();

        public bool IsConnected { get { return _client.IsConnected; } }

        public SigCoreCoordinator(SigCoreClient client) {
            _client = client;
        }

        public async Task<JObject> SendRequestAndWaitAsync(SigCoreMessage msg, int timeoutMs) {
            TaskCompletionSource<JObject> tcs = new TaskCompletionSource<JObject>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending[msg.MsgId] = tcs;
            try {
                await _client.SendAsync(msg).ConfigureAwait(false);
                Task delay = Task.Delay(timeoutMs);
                Task completed = await Task.WhenAny(tcs.Task, delay).ConfigureAwait(false);
                if (completed == delay) throw new TimeoutException();
                JObject result = await tcs.Task.ConfigureAwait(false);
                return result;
            } finally {
                _pending.TryRemove(msg.MsgId, out _);
            }
        }

        public void TrySetResult(JObject payload, ulong msgId) {
            TaskCompletionSource<JObject> tcs;
            if (_pending.TryRemove(msgId, out tcs))
                tcs.TrySetResult(payload);
        }

        public void Dispose() {
            _pending.Clear();
        }
    }
}

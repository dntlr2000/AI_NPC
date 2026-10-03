using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AiCharacterKit.LocalInference
{
    /// <summary>Admits a single generation with bounded FIFO waiting and independent caller cancellation.</summary>
    public sealed class LocalInferenceQueue : IDisposable
    {
        private readonly object sync = new object();
        private readonly LinkedList<TaskCompletionSource<IDisposable>> waiters = new LinkedList<TaskCompletionSource<IDisposable>>();
        private readonly int capacity;
        private readonly TimeSpan timeout;
        private bool active;
        private bool disposed;

        /// <summary>Creates the production queue or an explicitly bounded test queue.</summary>
        public LocalInferenceQueue(int capacity = LocalInferenceDefaults.MaxPendingRequests, TimeSpan? timeout = null)
        {
            this.capacity = capacity;
            this.timeout = timeout ?? TimeSpan.FromSeconds(LocalInferenceDefaults.QueueSeconds);
            if (capacity < 1 || this.timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        /// <summary>Acquires one lease in arrival order or fails without occupying an inference slot.</summary>
        public async Task<IDisposable> EnterAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LinkedListNode<TaskCompletionSource<IDisposable>> node;
            lock (sync)
            {
                if (disposed) throw LocalInferenceErrors.Unavailable();
                if (!active) { active = true; return new Lease(this); }
                if (waiters.Count >= capacity) throw LocalInferenceErrors.Busy();
                node = waiters.AddLast(new TaskCompletionSource<IDisposable>(TaskCreationOptions.RunContinuationsAsynchronously));
            }
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                deadline.CancelAfter(timeout);
                using (deadline.Token.Register(() => CancelWaiter(node, cancellationToken)))
                {
                    var lease = await node.Value.Task.ConfigureAwait(false);
                    if (deadline.IsCancellationRequested)
                    {
                        lease.Dispose();
                        cancellationToken.ThrowIfCancellationRequested();
                        throw LocalInferenceErrors.Busy();
                    }
                    return lease;
                }
            }
        }

        /// <summary>Removes only the cancelled or expired waiter, preserving the remaining FIFO order.</summary>
        private void CancelWaiter(LinkedListNode<TaskCompletionSource<IDisposable>> node, CancellationToken caller)
        {
            lock (sync)
            {
                if (node.List == null) return;
                waiters.Remove(node);
                if (caller.IsCancellationRequested) node.Value.TrySetCanceled();
                else node.Value.TrySetException(LocalInferenceErrors.Busy());
            }
        }

        /// <summary>Transfers the occupied slot directly to the oldest waiter.</summary>
        private void Release()
        {
            lock (sync)
            {
                if (waiters.Count == 0) { active = false; return; }
                var next = waiters.First.Value;
                waiters.RemoveFirst();
                next.TrySetResult(new Lease(this));
            }
        }

        /// <summary>Rejects new admissions and releases all pending callers during runner shutdown.</summary>
        public void Dispose()
        {
            lock (sync)
            {
                if (disposed) return;
                disposed = true;
                foreach (var waiter in waiters) waiter.TrySetException(LocalInferenceErrors.Unavailable());
                waiters.Clear();
            }
        }

        private sealed class Lease : IDisposable
        {
            private LocalInferenceQueue owner;
            /// <summary>Records ownership of one admitted generation.</summary>
            public Lease(LocalInferenceQueue owner) { this.owner = owner; }
            /// <summary>Releases a generation slot at most once.</summary>
            public void Dispose() { Interlocked.Exchange(ref owner, null)?.Release(); }
        }
    }
}

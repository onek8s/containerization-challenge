using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using RedLockNet;

namespace VanArsdel.BuildingBlocks.Caching
{
    public interface IDistributedLockProvider
    {
        /// <summary>Returns null when the lock could not be taken within <paramref name="wait"/>.</summary>
        Task<IAsyncDisposable> TryAcquireAsync(string resource, TimeSpan expiry, TimeSpan wait, CancellationToken cancellationToken);
    }

    /// <summary>Redlock on the Redis server(s).</summary>
    public class RedLockProvider : IDistributedLockProvider
    {
        private readonly IDistributedLockFactory _factory;
        private readonly string _prefix;

        public RedLockProvider(IDistributedLockFactory factory, string prefix)
        {
            _factory = factory;
            _prefix = prefix;
        }

        public async Task<IAsyncDisposable> TryAcquireAsync(string resource, TimeSpan expiry, TimeSpan wait, CancellationToken cancellationToken)
        {
            IRedLock redLock = wait > TimeSpan.Zero
                ? await _factory.CreateLockAsync(_prefix + resource, expiry, wait, TimeSpan.FromMilliseconds(100), cancellationToken)
                : await _factory.CreateLockAsync(_prefix + resource, expiry);
            if (redLock.IsAcquired) return new Releaser(redLock);
            redLock.Dispose();
            return null;
        }

        private sealed class Releaser : IAsyncDisposable
        {
            private readonly IRedLock _lock;
            public Releaser(IRedLock l) => _lock = l;

            public ValueTask DisposeAsync()
            {
                _lock.Dispose();
                return default;
            }
        }
    }

    /// <summary>In-process locks for a single instance (developer machines).</summary>
    public class LocalLockProvider : IDistributedLockProvider
    {
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new ConcurrentDictionary<string, SemaphoreSlim>();

        public async Task<IAsyncDisposable> TryAcquireAsync(string resource, TimeSpan expiry, TimeSpan wait, CancellationToken cancellationToken)
        {
            var gate = _locks.GetOrAdd(resource, _ => new SemaphoreSlim(1, 1));
            if (!await gate.WaitAsync(wait, cancellationToken)) return null;
            return new Releaser(gate);
        }

        private sealed class Releaser : IAsyncDisposable
        {
            private SemaphoreSlim _gate;
            public Releaser(SemaphoreSlim gate) => _gate = gate;

            public ValueTask DisposeAsync()
            {
                Interlocked.Exchange(ref _gate, null)?.Release();
                return default;
            }
        }
    }
}

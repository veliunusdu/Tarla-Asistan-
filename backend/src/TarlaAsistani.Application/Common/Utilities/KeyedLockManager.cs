using System.Collections.Concurrent;

namespace TarlaAsistani.Application.Common.Utilities;

public sealed class KeyedLockManager<TKey> where TKey : notnull
{
    private sealed class LockInfo
    {
        public readonly SemaphoreSlim Semaphore = new(1, 1);
        public int RefCount;
    }

    private readonly ConcurrentDictionary<TKey, LockInfo> _locks = new();

    public int ActiveLockCount => _locks.Count;

    public async Task<IDisposable> AcquireAsync(TKey key, CancellationToken cancellationToken = default)
    {
        LockInfo info;
        while (true)
        {
            info = _locks.GetOrAdd(key, _ => new LockInfo());
            lock (info)
            {
                if (_locks.TryGetValue(key, out var current) && ReferenceEquals(current, info))
                {
                    info.RefCount++;
                    break;
                }
            }
        }

        try
        {
            await info.Semaphore.WaitAsync(cancellationToken);
        }
        catch
        {
            ReleaseInternal(key, info, acquired: false);
            throw;
        }

        return new Releaser(this, key, info);
    }

    private void ReleaseInternal(TKey key, LockInfo info, bool acquired)
    {
        if (acquired)
        {
            info.Semaphore.Release();
        }

        lock (info)
        {
            info.RefCount--;
            if (info.RefCount == 0)
            {
                _locks.TryRemove(new KeyValuePair<TKey, LockInfo>(key, info));
            }
        }
    }

    private sealed class Releaser : IDisposable
    {
        private readonly KeyedLockManager<TKey> _manager;
        private readonly TKey _key;
        private readonly LockInfo _info;
        private bool _disposed;

        public Releaser(KeyedLockManager<TKey> manager, TKey key, LockInfo info)
        {
            _manager = manager;
            _key = key;
            _info = info;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _manager.ReleaseInternal(_key, _info, acquired: true);
            }
        }
    }
}

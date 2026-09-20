using FluentAssertions;
using TarlaAsistani.Application.Common.Utilities;

namespace TarlaAsistani.UnitTests.Common;

[Trait("Category", "Common")]
public class KeyedLockManagerTests
{
    [Fact]
    public async Task AcquireAsync_WhenDisposed_RemovesKeyFromInternalMap_LeavingZeroActiveLocks()
    {
        // Arrange
        var manager = new KeyedLockManager<Guid>();
        var key = Guid.NewGuid();

        // Act
        using (await manager.AcquireAsync(key))
        {
            manager.ActiveLockCount.Should().Be(1);
        }

        // Assert - Lock was released and key was removed from dictionary
        manager.ActiveLockCount.Should().Be(0);
    }

    [Fact]
    public async Task AcquireAsync_MultipleSequentialLocks_NeverLeavesOrphanEntries()
    {
        // Arrange
        var manager = new KeyedLockManager<Guid>();

        // Act - Simulate 100 sequential operations on different user keys
        for (int i = 0; i < 100; i++)
        {
            var key = Guid.NewGuid();
            using (await manager.AcquireAsync(key))
            {
                manager.ActiveLockCount.Should().Be(1);
            }
            manager.ActiveLockCount.Should().Be(0);
        }

        // Assert - After all operations, dictionary is completely empty (no unbounded growth)
        manager.ActiveLockCount.Should().Be(0);
    }

    [Fact]
    public async Task AcquireAsync_ConcurrentTasksOnSameKey_SerializesExecution()
    {
        // Arrange
        var manager = new KeyedLockManager<string>();
        const string key = "user-123";
        var executionLog = new List<int>();
        var activeConcurrent = 0;
        var maxConcurrent = 0;
        var sync = new object();

        // Act
        var task1 = Task.Run(async () =>
        {
            using (await manager.AcquireAsync(key))
            {
                lock (sync)
                {
                    activeConcurrent++;
                    if (activeConcurrent > maxConcurrent) maxConcurrent = activeConcurrent;
                }
                await Task.Delay(50);
                lock (sync)
                {
                    executionLog.Add(1);
                    activeConcurrent--;
                }
            }
        });

        var task2 = Task.Run(async () =>
        {
            await Task.Delay(10); // Ensure task1 begins first
            using (await manager.AcquireAsync(key))
            {
                lock (sync)
                {
                    activeConcurrent++;
                    if (activeConcurrent > maxConcurrent) maxConcurrent = activeConcurrent;
                }
                await Task.Delay(50);
                lock (sync)
                {
                    executionLog.Add(2);
                    activeConcurrent--;
                }
            }
        });

        await Task.WhenAll(task1, task2);

        // Assert
        maxConcurrent.Should().Be(1, "locks on same key must strictly serialize execution");
        executionLog.Should().Equal(1, 2);
        manager.ActiveLockCount.Should().Be(0, "after all tasks finish, dictionary must be cleaned up");
    }

    [Fact]
    public async Task AcquireAsync_ConcurrentTasksOnDifferentKeys_ExecuteConcurrently()
    {
        // Arrange
        var manager = new KeyedLockManager<string>();
        var t1Started = new TaskCompletionSource();
        var t2Started = new TaskCompletionSource();

        // Act
        var task1 = Task.Run(async () =>
        {
            using (await manager.AcquireAsync("user-A"))
            {
                t1Started.SetResult();
                await t2Started.Task; // Wait for task 2 to also acquire its lock
            }
        });

        var task2 = Task.Run(async () =>
        {
            await t1Started.Task;
            using (await manager.AcquireAsync("user-B"))
            {
                t2Started.SetResult();
                manager.ActiveLockCount.Should().Be(2, "two distinct keys must both be active concurrently");
            }
        });

        await Task.WhenAll(task1, task2);

        // Assert
        manager.ActiveLockCount.Should().Be(0, "all keys must be cleaned up after tasks finish");
    }
}

using RatBot.Discord.BackgroundWorkers;
using Shouldly;

namespace RatBot.Discord.Tests.BackgroundWorkers;

[TestFixture]
public sealed class RoleColourSyncQueueTests
{
    [Test]
    public async Task Enqueue_RejectsDuplicateWhilePendingAndInFlight()
    {
        RoleColourSyncQueue queue = new RoleColourSyncQueue(capacity: 1);

        queue.Enqueue(1, 2).ShouldBeTrue();
        queue.Enqueue(1, 2).ShouldBeFalse();

        IRoleColourSyncQueue.WorkItem item = await queue.Reader.ReadAsync();
        queue.OnWorkStarted();

        queue.Enqueue(1, 2).ShouldBeFalse();
        queue.GetStatus().ShouldBe(new IRoleColourSyncQueue.Status(0, 1, null));
    }

    [Test]
    public async Task Enqueue_AllowsRequeueAfterCompletion()
    {
        RoleColourSyncQueue queue = new RoleColourSyncQueue(capacity: 1);
        queue.Enqueue(1, 2).ShouldBeTrue();
        IRoleColourSyncQueue.WorkItem item = await queue.Reader.ReadAsync();
        queue.OnWorkStarted();
        queue.OnWorkCompleted(item);

        queue.Enqueue(1, 2).ShouldBeTrue();
        queue.GetStatus().Pending.ShouldBe(1);
    }

    [Test]
    public async Task Enqueue_WhenFull_ReturnsFalseAndRollsBackState()
    {
        RoleColourSyncQueue queue = new RoleColourSyncQueue(capacity: 1);
        queue.Enqueue(1, 1).ShouldBeTrue();

        queue.Enqueue(1, 2).ShouldBeFalse();

        queue.GetStatus().Pending.ShouldBe(1);
        await queue.Reader.ReadAsync();
        queue.Enqueue(1, 2).ShouldBeTrue();
    }

    [Test]
    public async Task EnqueueAsync_WhenFull_WaitsForCapacity()
    {
        RoleColourSyncQueue queue = new RoleColourSyncQueue(capacity: 1);
        queue.Enqueue(1, 1).ShouldBeTrue();

        Task enqueue = queue.EnqueueAsync(1, 2, CancellationToken.None).AsTask();
        enqueue.IsCompleted.ShouldBeFalse();

        await queue.Reader.ReadAsync();
        await enqueue;

        (await queue.Reader.ReadAsync()).ShouldBe(new IRoleColourSyncQueue.WorkItem(1, 2));
        queue.GetStatus().Pending.ShouldBe(2);
    }

    [Test]
    public async Task EnqueueAsync_WhenCancelled_RollsBackCountersAndDeduplication()
    {
        RoleColourSyncQueue queue = new RoleColourSyncQueue(capacity: 1);
        queue.Enqueue(1, 1).ShouldBeTrue();
        using CancellationTokenSource cancellation = new CancellationTokenSource();

        Task enqueue = queue.EnqueueAsync(1, 2, cancellation.Token).AsTask();
        enqueue.IsCompleted.ShouldBeFalse();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(enqueue);
        queue.GetStatus().Pending.ShouldBe(1);

        await queue.Reader.ReadAsync(cancellation.Token);
        (await queue.EnqueueAsync(1, 2)).ShouldBeTrue();
    }
}

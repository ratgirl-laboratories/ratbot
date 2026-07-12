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

        await queue.Reader.ReadAsync();
        queue.OnWorkStarted();

        queue.Enqueue(1, 2).ShouldBeFalse();
        queue.GetStatus().ShouldBe(new IRoleColourSyncQueue.Status(0, 1, Eta: null));
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
    public async Task Enqueue_WhenFull_WaitsForCapacity()
    {
        RoleColourSyncQueue queue = new RoleColourSyncQueue(capacity: 1);
        queue.Enqueue(1, 1).ShouldBeTrue();

        bool enqueue = queue.Enqueue(1, 2);
        enqueue.ShouldBeFalse();

        await queue.Reader.ReadAsync();

        (await queue.Reader.ReadAsync()).ShouldBe(new IRoleColourSyncQueue.WorkItem(1, 2));
        queue.GetStatus().Pending.ShouldBe(2);
    }
}

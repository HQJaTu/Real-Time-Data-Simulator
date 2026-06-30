using System.Collections.Concurrent;
using RTDSimulator.Core;
using Xunit;

namespace RTDSimulator.Core.Tests;

public class LoadGeneratorTests
{
    /// <summary>In-memory connection that records the batches it is asked to send.</summary>
    private sealed class RecordingConnection : ITargetConnection
    {
        private int _connectCount;
        public int ConnectCount => _connectCount;
        public ConcurrentQueue<int> BatchSizes { get; } = new();
        public int Batches => BatchSizes.Count;

        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _connectCount);
            return Task.CompletedTask;
        }

        public Task SendBatchAsync(IReadOnlyCollection<string> payloads, CancellationToken cancellationToken = default)
        {
            BatchSizes.Enqueue(payloads.Count);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Send_SingleSender_DispatchesConfiguredBatches()
    {
        var connection = new RecordingConnection();
        var generator = new LoadGenerator("x") { BatchesNo = 3, EventsPerBatch = 2 };

        await generator.Send(connection, CancellationToken.None);

        Assert.Equal(3, connection.Batches);
        Assert.All(connection.BatchSizes, size => Assert.Equal(2, size));
        Assert.Equal(1, connection.ConnectCount);
    }

    [Fact]
    public async Task Send_WithParallelism_RunsEverySender()
    {
        var connection = new RecordingConnection();
        var generator = new LoadGenerator("x") { BatchesNo = 2, EventsPerBatch = 5, WaitTime = TimeSpan.Zero };

        long messages = 0;
        int batchEvents = 0;
        generator.BatchSent += (_, e) =>
        {
            Interlocked.Add(ref messages, e.MessageCount);
            Interlocked.Increment(ref batchEvents);
        };

        await generator.Send(connection, parallelism: 3, CancellationToken.None);

        Assert.Equal(6, connection.Batches);   // 3 senders * 2 batches
        Assert.Equal(6, batchEvents);
        Assert.Equal(30, messages);            // 6 batches * 5 messages
        Assert.All(connection.BatchSizes, size => Assert.Equal(5, size));
        Assert.Equal(1, connection.ConnectCount); // connected once before fan-out
    }

    [Fact]
    public async Task BatchSentEventArgs_ReportsPerBatchSize()
    {
        var connection = new RecordingConnection();
        // Payload "abcd" (4 bytes) x 3 per batch => 12 bytes per batch.
        var generator = new LoadGenerator("abcd") { BatchesNo = 1, EventsPerBatch = 3 };

        BatchSentEventArgs? captured = null;
        generator.BatchSent += (_, e) => captured = e;

        await generator.Send(connection, CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(3, captured!.MessageCount);
        Assert.Equal(12, captured.SizeInBytes);
    }

    [Fact]
    public async Task Send_WhenCancelled_StopsEarly()
    {
        var connection = new RecordingConnection();
        var generator = new LoadGenerator("x") { BatchesNo = 1000, EventsPerBatch = 1 };

        using var cts = new CancellationTokenSource();
        generator.BatchSent += (_, _) => cts.Cancel(); // cancel after the first batch

        await generator.Send(connection, cts.Token);

        Assert.InRange(connection.Batches, 1, 999);
    }

    [Fact]
    public async Task Send_ParallelismBelowOne_IsTreatedAsSingleSender()
    {
        var connection = new RecordingConnection();
        var generator = new LoadGenerator("x") { BatchesNo = 4, EventsPerBatch = 1 };

        await generator.Send(connection, parallelism: 0, CancellationToken.None);

        Assert.Equal(4, connection.Batches);
    }
}

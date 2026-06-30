namespace RTDSimulator.Core;

/// <summary>
/// Drives load generation: builds batches of generated payloads and dispatches
/// them through an <see cref="ITargetConnection"/>. The connection is responsible
/// for the service-specific transport, keeping this orchestration generic.
///
/// Concurrency is owned here: <see cref="Send(ITargetConnection, int, CancellationToken)"/>
/// runs the requested number of senders as concurrent async tasks. The batch loop keeps
/// no shared mutable state, so a single instance is safe to run in parallel.
/// </summary>
public class LoadGenerator
{
    /// <summary>Number of batches each sender dispatches.</summary>
    public int BatchesNo { get; set; } = 1;

    /// <summary>Number of messages per batch.</summary>
    public int EventsPerBatch { get; set; } = 1;

    /// <summary>Delay between consecutive batches within a single sender.</summary>
    public TimeSpan WaitTime { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// The payload generator used to produce each message. Exposed so callers
    /// (e.g. a preview screen) can inspect <see cref="PayloadGenerator.Variables"/>.
    /// </summary>
    public PayloadGenerator Generator { get; }

    /// <summary>
    /// Raised after each batch is sent. May be invoked concurrently when
    /// <c>parallelism &gt; 1</c>; handlers must be thread-safe (or marshal to a UI thread).
    /// </summary>
    public event EventHandler<BatchSentEventArgs>? BatchSent;

    public LoadGenerator(string payload)
        : this(new PayloadGenerator(payload))
    {
    }

    public LoadGenerator(PayloadGenerator generator)
    {
        Generator = generator;
    }

    /// <summary>Runs a single sender.</summary>
    public Task Send(ITargetConnection connection, CancellationToken cancellationToken)
        => Send(connection, 1, cancellationToken);

    /// <summary>
    /// Runs <paramref name="parallelism"/> senders concurrently, each dispatching
    /// <see cref="BatchesNo"/> batches. The (thread-safe) connection is shared.
    /// </summary>
    public async Task Send(ITargetConnection connection, int parallelism, CancellationToken cancellationToken)
    {
        if (parallelism < 1)
            parallelism = 1;

        await connection.ConnectAsync(cancellationToken);

        var senders = Enumerable.Range(0, parallelism)
            .Select(_ => RunSenderAsync(connection, cancellationToken));
        await Task.WhenAll(senders);
    }

    private async Task RunSenderAsync(ITargetConnection connection, CancellationToken cancellationToken)
    {
        DateTime nextRun = DateTime.Now;
        for (int batch = 0; batch < BatchesNo; batch++)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            long batchSizeInBytes = 0;
            var payloads = new List<string>(EventsPerBatch);
            for (int m = 0; m < EventsPerBatch; m++)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                string payload = await Generator.GetPayload(m);
                payloads.Add(payload);
                batchSizeInBytes += payload.Length;
            }

            await SleepUntil(nextRun, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
                return;

            await connection.SendBatchAsync(payloads, cancellationToken);
            BatchSent?.Invoke(this, new BatchSentEventArgs(payloads.Count, batchSizeInBytes));

            nextRun = DateTime.Now.Add(WaitTime);
        }
    }

    private async Task SleepUntil(DateTime t, CancellationToken cancellationToken)
    {
        TimeSpan waitTime = t.Subtract(DateTime.Now);
        if (waitTime <= TimeSpan.Zero) return;
        try
        {
            await Task.Delay(waitTime, cancellationToken);
        }
        catch (Exception)
        {
            //don't throw if cancelled
        }
    }
}

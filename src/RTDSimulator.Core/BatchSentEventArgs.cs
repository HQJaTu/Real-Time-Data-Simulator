namespace RTDSimulator.Core;

/// <summary>
/// Reports a single dispatched batch. Carrying the per-batch figures on the event
/// (rather than on shared <see cref="LoadGenerator"/> fields) keeps progress
/// reporting correct when several senders run concurrently.
/// </summary>
public sealed class BatchSentEventArgs : EventArgs
{
    public BatchSentEventArgs(int messageCount, long sizeInBytes)
    {
        MessageCount = messageCount;
        SizeInBytes = sizeInBytes;
    }

    /// <summary>Number of messages in the batch.</summary>
    public int MessageCount { get; }

    /// <summary>Total payload size of the batch, in bytes.</summary>
    public long SizeInBytes { get; }
}

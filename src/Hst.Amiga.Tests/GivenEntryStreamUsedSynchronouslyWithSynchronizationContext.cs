namespace Hst.Amiga.Tests;

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static FileSystemVolumeTestHelper;
using FileMode = FileSystems.FileMode;

/// <summary>
/// Synchronous stream methods wait for async file system operations. These tests run them on a single threaded
/// synchronization context, like a UI thread, against a disk stream completing asynchronously, like a file stream,
/// which deadlocks if continuations try to resume on the blocked thread.
/// </summary>
public class GivenEntryStreamUsedSynchronouslyWithSynchronizationContext
{
    private const string FileName = "File";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    private static readonly byte[] Data = Enumerable.Range(0, 20000).Select(x => (byte)(x % 251)).ToArray();

    [Theory]
    [InlineData(Ffs)]
    [InlineData(Pfs3)]
    [InlineData(Sfs)]
    public async Task WhenReadAndDisposeSynchronouslyThenDataIsReadWithoutDeadlock(string fileSystem)
    {
        // arrange - formatted disk with file containing data
        var stream = new AsyncCompletingMemoryStream();
        await CreateFormattedDisk(fileSystem, stream);
        await using (var volume = await MountVolume(fileSystem, stream))
        {
            await volume.CreateFile(FileName, false, false);
            await using var entryStream = await volume.OpenFile(FileName, FileMode.Write, true, false);
            await entryStream.WriteAsync(Data, 0, Data.Length);
        }

        // arrange - mount volume and open file for reading
        await using var readVolume = await MountVolume(fileSystem, stream);
        var readStream = await readVolume.OpenFile(FileName, FileMode.Read, false, false);

        // act - read and dispose synchronously on synchronization context
        var buffer = new byte[Data.Length];
        var bytesRead = 0;
        RunOnSingleThreadedSynchronizationContext(() =>
        {
            int read;
            while (bytesRead < buffer.Length &&
                   (read = readStream.Read(buffer, bytesRead, buffer.Length - bytesRead)) > 0)
            {
                bytesRead += read;
            }

            readStream.Dispose();
        });

        // assert - data is read
        Assert.Equal(Data.Length, bytesRead);
        Assert.Equal(Data, buffer);
    }

    [Theory]
    [InlineData(Ffs)]
    [InlineData(Pfs3)]
    [InlineData(Sfs)]
    public async Task WhenWriteAndDisposeSynchronouslyThenDataIsWrittenWithoutDeadlock(string fileSystem)
    {
        // arrange - formatted disk with empty file opened for writing
        var stream = new AsyncCompletingMemoryStream();
        await CreateFormattedDisk(fileSystem, stream);
        await using (var volume = await MountVolume(fileSystem, stream))
        {
            await volume.CreateFile(FileName, false, false);
            var writeStream = await volume.OpenFile(FileName, FileMode.Write, true, false);

            // act - write, flush and dispose synchronously on synchronization context
            RunOnSingleThreadedSynchronizationContext(() =>
            {
                writeStream.Write(Data, 0, Data.Length);
                writeStream.Flush();
                writeStream.Dispose();
            });
        }

        // assert - file contains data
        await using var readVolume = await MountVolume(fileSystem, stream);
        await using var readStream = await readVolume.OpenFile(FileName, FileMode.Read, false, false);
        var memoryStream = new MemoryStream();
        await readStream.CopyToAsync(memoryStream);
        Assert.Equal(Data, memoryStream.ToArray());
    }

    /// <summary>
    /// Run action on a new thread with a single threaded synchronization context, which only runs posted
    /// continuations when the thread is not busy, and fail if action doesn't complete within timeout.
    /// </summary>
    private static void RunOnSingleThreadedSynchronizationContext(Action action)
    {
        Exception error = null;
        using var completed = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            var context = new SingleThreadedSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(context);
            context.Post(_ =>
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    error = e;
                }
                finally
                {
                    completed.Set();
                    context.Complete();
                }
            }, null);
            context.Run();
        })
        {
            IsBackground = true
        };
        thread.Start();

        Assert.True(completed.Wait(Timeout), "Synchronous stream operation deadlocked on synchronization context");
        if (error != null)
        {
            throw new AggregateException(error);
        }
    }

    private sealed class SingleThreadedSynchronizationContext : SynchronizationContext
    {
        private readonly BlockingCollection<(SendOrPostCallback Callback, object State)> queue = new();

        public override void Post(SendOrPostCallback d, object state) => queue.Add((d, state));

        public override void Send(SendOrPostCallback d, object state) =>
            throw new NotSupportedException("Synchronous send is not supported");

        public void Run()
        {
            foreach (var (callback, state) in queue.GetConsumingEnumerable())
            {
                callback(state);
            }
        }

        public void Complete() => queue.CompleteAdding();
    }

    /// <summary>
    /// Awaitable that always continues on a thread pool thread without capturing synchronization context.
    /// </summary>
    private sealed class ThreadPoolYield : System.Runtime.CompilerServices.INotifyCompletion
    {
        public static readonly ThreadPoolYield Instance = new();

        public ThreadPoolYield GetAwaiter() => this;

        public bool IsCompleted => false;

        public void OnCompleted(Action continuation) =>
            ThreadPool.UnsafeQueueUserWorkItem(_ => continuation(), null);

        public void GetResult()
        {
        }
    }

    /// <summary>
    /// Memory stream where async reads and writes complete on a thread pool thread, like a file stream, instead of
    /// completing synchronously.
    /// </summary>
    private sealed class AsyncCompletingMemoryStream : MemoryStream
    {
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count,
            CancellationToken cancellationToken)
        {
            await ThreadPoolYield.Instance;
            return base.Read(buffer, offset, count);
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            await ThreadPoolYield.Instance;
            return base.Read(buffer.Span);
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count,
            CancellationToken cancellationToken)
        {
            await ThreadPoolYield.Instance;
            base.Write(buffer, offset, count);
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            await ThreadPoolYield.Instance;
            base.Write(buffer.Span);
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            await ThreadPoolYield.Instance;
        }
    }
}

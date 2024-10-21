using Azure.Storage.Blobs;
using Diginsight.Analyzer.Entities;
using Diginsight.Analyzer.Repositories.Configurations;
using Diginsight.Analyzer.Repositories.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Runtime.CompilerServices;
using Timer = System.Timers.Timer;

namespace Diginsight.Analyzer.Repositories;

internal sealed partial class AnalysisInfoRepository : IAnalysisInfoRepository
{
    private readonly ILogger logger;
    private readonly IOptionsMonitor<AnalysisInfoConfig> analysisInfoConfigMonitor;
    private readonly Container cosmosContainer;
    private readonly BlobContainerClient blobContainerClient;

    public AnalysisInfoRepository(
        ILogger<AnalysisInfoRepository> logger,
        IOptionsMonitor<AnalysisInfoConfig> analysisInfoConfigMonitor
    )
    {
        this.logger = logger;
        this.analysisInfoConfigMonitor = analysisInfoConfigMonitor;
        throw new NotImplementedException();
    }

    private IAnalysisInfoConfig AnalysisInfoConfig => analysisInfoConfigMonitor.CurrentValue;

    public async Task InsertAsync(IAnalysisContext analysisContext)
    {
        await DeleteCoreAsync(analysisContext.ExecutionCoord.Id);
        await UpsertCoreAsync(analysisContext);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UpsertAsync(IAnalysisContext analysisContext)
    {
        return UpsertCoreAsync(analysisContext);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task DeleteAsync(Guid executionId)
    {
        return DeleteCoreAsync(executionId);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IDisposable? StartTimedProgressFlush(IAnalysisContext analysisContext)
    {
        return StartTimedProgressFlush(() => WriteProgressAsync(analysisContext));
    }

    public async Task<(IEnumerable<AnalysisContextSnapshot> Items, int TotalCount)> GetAnalysisSnapshotsAsync(int page, int pageSize, bool withProgress)
    {
        LogMessages.GettingAnalysisSnapshots(logger, page, pageSize);

        throw new NotImplementedException();

        //var paginatedResult = await migrationContextSnapshotRepository.GetPaginatedItemsAsync(
        //    static x => x.Status != TimeBoundStatus.Pending, page, pageSize, nameof(MigrationContextSnapshot.StartedAt), false
        //);

        //IEnumerable<MigrationContextSnapshot> snapshots = paginatedResult.Values;
        //if (withProgress)
        //{
        //    foreach (MigrationContextSnapshot snapshot in snapshots)
        //    {
        //        await FillProgressAsync(snapshot);
        //    }
        //}

        //return (snapshots, paginatedResult.TotalItems);
    }

    public async Task<(IEnumerable<AnalysisContextSnapshot> Items, int TotalCount)> GetQueuedAnalysisSnapshotsAsync(int page, int pageSize)
    {
        LogMessages.GettingQueuedAnalysisSnapshots(logger, page, pageSize);

        throw new NotImplementedException();

        //var paginatedResult = await migrationContextSnapshotRepository.GetPaginatedItemsAsync(
        //    static x => x.Status == TimeBoundStatus.Pending, page, pageSize, nameof(MigrationContextSnapshot.QueuedAt), true
        //);

        //IEnumerable<MigrationContextSnapshot> snapshots = paginatedResult.Values;

        //return (snapshots, paginatedResult.TotalItems);
    }

    public async Task<AnalysisContextSnapshot?> GetAnalysisSnapshotAsync(Guid executionId, bool withProgress)
    {
        LogMessages.GettingAnalysisSnapshot(logger, executionId);

        throw new NotImplementedException();

        //MigrationContextSnapshot? snapshot = await migrationContextSnapshotRepository
        //    .GetItemsAE(q => q.Where(x => x.InstanceId == instanceId))
        //    .FirstOrDefaultAsync();

        //if (snapshot is not null && withProgress)
        //{
        //    await FillProgressAsync(snapshot);
        //}

        //return snapshot;
    }

    public async Task<AnalysisContextSnapshot?> GetAnalysisSnapshotAsync(AnalysisCoord analysisCoord, bool withProgress)
    {
        (Guid analysisId, int attempt) = analysisCoord;

        LogMessages.GettingAnalysisSnapshot(logger, analysisId, attempt);

        throw new NotImplementedException();

        //MigrationContextSnapshot? snapshot = await migrationContextSnapshotRepository
        //    .GetItemsAE(q => q.Where(x => x.InstanceId == instanceId))
        //    .FirstOrDefaultAsync();

        //if (snapshot is not null && withProgress)
        //{
        //    await FillProgressAsync(snapshot);
        //}

        //return snapshot;
    }

    public IAsyncEnumerable<AnalysisContextSnapshot> GetAllQueuedAnalysisSnapshotsAE()
    {
        LogMessages.GettingAllQueuedAnalysisSnapshots(logger);

        throw new NotImplementedException();
        //return migrationContextSnapshotRepository
        //    .GetItemsAE(static q => q.Where(static x => x.Status == TimeBoundStatus.Pending).OrderBy(static x => x.QueuedAt));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static JsonSerializer GetSerializer()
    {
        return JsonSerializer.CreateDefault();
    }

    private async Task DeleteCoreAsync(Guid executionId)
    {
        LogMessages.DeletingExecutionContext(logger, executionId);

        //migrationContextId ??= await migrationContextDocumentRepository
        //    .GetItemsAE(q => q.Where(x => x.InstanceId == instanceId).Select(static x => x.Id))
        //    .FirstOrDefaultAsync();
        //if (migrationContextId is null)
        //{
        //    return;
        //}

        //await migrationContextDocumentRepository.DeleteItemAsync(migrationContextId, new PartitionKey(instanceId.ToString()));
    }

    private async Task UpsertCoreAsync(IAnalysisContext analysisContext)
    {
        (Guid analysisId, int attempt) = analysisContext.AnalysisCoord;
        LogMessages.UpsertingAnalysisContext(logger, analysisId, attempt);

        AnalysisContextDocument document = AnalysisContextDocument.Create(analysisContext);
        throw new NotImplementedException();
        //await migrationContextDocumentRepository.UpsertItemAsync(document);

        //try
        //{
        //    await WriteProgressAsync(analysisContext);
        //}
        //catch (IOException exception)
        //{
        //    LogMessages.ErrorWritingProgress(logger, exception);
        //}
    }

    private Task WriteProgressAsync(IAnalysisContext analysisContext)
    {
        if (analysisContext.IsNotStarted())
        {
            return Task.CompletedTask;
        }

        (Guid analysisId, int attempt) = analysisContext.AnalysisCoord;
        return WriteProgressAsync(analysisId, attempt, analysisContext.GetProgress<Progress>());
    }

    private async Task WriteProgressAsync(Guid analysisId, int attempt, Progress progress)
    {
        throw new NotImplementedException();

        //string filePath = Path.Combine(ProgressPath, $"{fileName}.json");
        //Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        //await using Stream stream = File.OpenWrite(filePath);
        //await GetSerializer().SerializeAsync(stream, progress);
    }

    private IDisposable? StartTimedProgressFlush(Func<Task> flushAsync)
    {
        int seconds = AnalysisInfoConfig.TimedProgressFlushSeconds;
        if (seconds < 30)
        {
            return null;
        }

        Timer timer = new (TimeSpan.FromSeconds(seconds).TotalMilliseconds) { AutoReset = true };
        timer.Elapsed += (_, _) =>
        {
            try
            {
                flushAsync().GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                _ = e;
            }
        };
        timer.Start();

        return timer;
    }

    private async Task FillProgressAsync(AnalysisContextSnapshot snapshot)
    {
        if (snapshot.StartedAt is null)
            return;

        snapshot.Progress = await ReadProgressAsync(snapshot.AnalysisCoord) ?? new Progress();
    }

    private async Task<Progress?> ReadProgressAsync(AnalysisCoord analysisCoord)
    {
        (Guid analysisId, int attempt) = analysisCoord;

        LogMessages.ReadingAnalysisProgress(logger, analysisId, attempt);

        throw new NotImplementedException();
        //Stream stream;
        //try
        //{
        //    stream = File.OpenRead(Path.Combine(ProgressPath, $"{filePath}.json"));
        //}
        //catch (Exception)
        //{
        //    return null;
        //}

        //await using (stream)
        //{
        //    return GetSerializer().Deserialize<T>(stream);
        //}
    }

    private static partial class LogMessages
    {
        [LoggerMessage(0, LogLevel.Trace, "Upserting analysis context for analysis {AnalysisId} attempt {Attempt}")]
        internal static partial void UpsertingAnalysisContext(ILogger logger, Guid analysisId, int attempt);

        [LoggerMessage(1, LogLevel.Trace, "Getting latest analysis snapshots (page {PageIndex} sized {PageSize})")]
        internal static partial void GettingAnalysisSnapshots(ILogger logger, int pageIndex, int pageSize);

        [LoggerMessage(2, LogLevel.Trace, "Getting analysis snapshots for analysis {ExecutionId}")]
        internal static partial void GettingAnalysisSnapshot(ILogger logger, Guid executionId);

        [LoggerMessage(3, LogLevel.Trace, "Getting analysis snapshots for analysis {AnalysisId} attempt {Attempt}")]
        internal static partial void GettingAnalysisSnapshot(ILogger logger, Guid analysisId, int attempt);

        [LoggerMessage(4, LogLevel.Warning, "I/O error writing progress")]
        internal static partial void ErrorWritingProgress(ILogger logger, Exception exception);

        [LoggerMessage(5, LogLevel.Trace, "Getting all queued analysis snapshots")]
        internal static partial void GettingAllQueuedAnalysisSnapshots(ILogger logger);

        [LoggerMessage(6, LogLevel.Trace, "Getting queued analysis snapshots (page {PageIndex} sized {PageSize})")]
        internal static partial void GettingQueuedAnalysisSnapshots(ILogger logger, int pageIndex, int pageSize);

        [LoggerMessage(7, LogLevel.Trace, "Deleting execution context for execution {ExecutionId}")]
        internal static partial void DeletingExecutionContext(ILogger logger, Guid executionId);

        [LoggerMessage(8, LogLevel.Trace, "Reading analysis progress for analysis {AnalysisId} attempt {Attempt}")]
        internal static partial void ReadingAnalysisProgress(ILogger logger, Guid analysisId, int attempt);
    }
}

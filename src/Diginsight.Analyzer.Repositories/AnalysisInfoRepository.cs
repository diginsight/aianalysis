using Diginsight.Analyzer.Entities;
using Diginsight.Analyzer.Repositories.Configurations;
using Diginsight.Analyzer.Repositories.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Runtime.CompilerServices;
using Timer = System.Timers.Timer;

namespace Diginsight.Analyzer.Repositories;

internal sealed partial class AnalysisInfoRepository : IAnalysisInfoRepository
{
    private readonly ILogger logger;
    //private readonly IRepository<AnalysisContextDocument> analysisContextDocumentRepository;
    //private readonly IRepository<AnalysisContextSnapshot> analysisContextSnapshotRepository;
    private readonly IOptionsMonitor<AnalysisInfoConfig> analysisInfoConfigMonitor;

    public AnalysisInfoRepository(
        ILogger<AnalysisInfoRepository> logger,
        //IRepository<AnalysisContextDocument> analysisContextDocumentRepository,
        //IRepository<AnalysisContextSnapshot> analysisContextSnapshotRepository,
        IOptionsMonitor<AnalysisInfoConfig> analysisInfoConfigMonitor
    )
    {
        this.logger = logger;
        //this.analysisContextDocumentRepository = analysisContextDocumentRepository;
        //this.analysisContextSnapshotRepository = analysisContextSnapshotRepository;
        this.analysisInfoConfigMonitor = analysisInfoConfigMonitor;
    }

    private IAnalysisInfoConfig AnalysisInfoConfig => analysisInfoConfigMonitor.CurrentValue;

    public async Task InsertAsync(IAnalysisContext analysisContext)
    {
        await DeleteCoreAsync(analysisContext.Coordinate, null);
        await UpsertCoreAsync(analysisContext);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UpsertAsync(IAnalysisContext analysisContext)
    {
        return UpsertCoreAsync(analysisContext);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task DeleteAsync(AnalysisCoordinate coordinate, string persistenceId)
    {
        return DeleteCoreAsync(coordinate, persistenceId);
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

        //var paginatedResult = await analysisContextSnapshotRepository.GetPaginatedItemsAsync(
        //    static x => x.Status != TimeBoundStatus.Pending, page, pageSize, nameof(AnalysisContextSnapshot.StartedAt), false
        //);

        //IEnumerable<AnalysisContextSnapshot> snapshots = paginatedResult.Values;
        //if (withProgress)
        //{
        //    foreach (AnalysisContextSnapshot snapshot in snapshots)
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

        //var paginatedResult = await analysisContextSnapshotRepository.GetPaginatedItemsAsync(
        //    static x => x.Status == TimeBoundStatus.Pending, page, pageSize, nameof(AnalysisContextSnapshot.QueuedAt), true
        //);

        //IEnumerable<AnalysisContextSnapshot> snapshots = paginatedResult.Values;

        //return (snapshots, paginatedResult.TotalItems);
    }

    public async Task<AnalysisContextSnapshot?> GetAnalysisSnapshotAsync(AnalysisCoordinate coordinate, bool withProgress)
    {
        (Guid analysisId, int attempt) = coordinate;

        LogMessages.GettingAnalysisSnapshots(logger, analysisId, attempt);

        throw new NotImplementedException();

        //AnalysisContextSnapshot? snapshot = await analysisContextSnapshotRepository
        //    .GetItemsAE(q => q.Where(x => x.AnalysisId == analysisId && x.Attempt == attempt))
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

        //return analysisContextSnapshotRepository
        //    .GetItemsAE(static q => q.Where(static x => x.Status == TimeBoundStatus.Pending).OrderBy(static x => x.QueuedAt));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static JsonSerializer GetSerializer()
    {
        return JsonSerializer.CreateDefault();
    }

    private async Task DeleteCoreAsync(AnalysisCoordinate coordinate, string? persistenceId)
    {
        (Guid analysisId, int attempt) = coordinate;

        LogMessages.DeletingAnalysisContext(logger, analysisId, attempt);

        throw new NotImplementedException();

        //persistenceId ??= await analysisContextDocumentRepository
        //    .GetItemsAE(q => q.Where(x => x.AnalysisId == analysisId && x.Attempt == attempt).Select(static x => x.Id))
        //    .FirstOrDefaultAsync();
        //if (persistenceId is null)
        //{
        //    return;
        //}

        //await analysisContextDocumentRepository.DeleteItemAsync(persistenceId, new PartitionKey(analysisId.ToString()));
    }

    private async Task UpsertCoreAsync(IAnalysisContext analysisContext)
    {
        (Guid analysisId, int attempt) = analysisContext.Coordinate;

        LogMessages.UpsertingAnalysisContext(logger, analysisId, attempt);

        throw new NotImplementedException();

        //AnalysisContextDocument document = AnalysisContextDocument.Create(analysisContext);
        //await analysisContextDocumentRepository.UpsertItemAsync(document);

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
        return analysisContext.IsNotStarted()
            ? Task.CompletedTask
            : WriteProgressAsync(analysisContext.Coordinate, analysisContext.GetProgress<AnalysisProgress>());
    }

    private async Task WriteProgressAsync(AnalysisCoordinate coordinate, object progress)
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
        if (snapshot.StartedAt is not null)
        {
            (Guid analysisId, int attempt) = snapshot.Coordinate;
            snapshot.Progress = await ReadProgressAsync<AnalysisProgress>(analysisId, attempt) ?? new AnalysisProgress();
        }
    }

    private async Task<T?> ReadProgressAsync<T>(Guid analysisId, int attempt)
        where T : class
    {
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

        [LoggerMessage(2, LogLevel.Trace, "Getting analysis snapshots for analysis {AnalysisId} attempt {Attempt}")]
        internal static partial void GettingAnalysisSnapshots(ILogger logger, Guid analysisId, int attempt);

        [LoggerMessage(3, LogLevel.Warning, "I/O error writing progress")]
        internal static partial void ErrorWritingProgress(ILogger logger, Exception exception);

        [LoggerMessage(4, LogLevel.Trace, "Getting all queued analysis snapshots")]
        internal static partial void GettingAllQueuedAnalysisSnapshots(ILogger logger);

        [LoggerMessage(5, LogLevel.Trace, "Getting queued analysis snapshots (page {PageIndex} sized {PageSize})")]
        internal static partial void GettingQueuedAnalysisSnapshots(ILogger logger, int pageIndex, int pageSize);

        [LoggerMessage(6, LogLevel.Trace, "Deleting analysis context for analysis {AnalysisId} attempt {Attempt}")]
        internal static partial void DeletingAnalysisContext(ILogger logger, Guid analysisId, int attempt);
    }
}

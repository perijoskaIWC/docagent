using System.Threading.Channels;
using DocAgent.Core;
using DocAgent.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DocAgent.Api.Runs;

/// <summary>
/// The IProgress the pipeline reports to. Report must return at once and may be called from several threads together
/// (Stevcho's "concurrent" author), while a DbContext allows one operation at a time. So Report only queues the
/// update, and a single loop with its own DbContext writes the updates to the run's row, in order.
/// </summary>
internal sealed class RunProgressWriter : IProgress<RunProgress>, IAsyncDisposable
{
    private readonly Channel<RunProgress> _updates =
        Channel.CreateUnbounded<RunProgress>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Task _writer;

    public RunProgressWriter(Guid runId, IServiceScopeFactory scopes, ILogger logger) =>
        _writer = WriteAsync(runId, scopes, logger);

    public void Report(RunProgress value)
    {
        // Completed and Failed are set by the worker, in the same save as the result. Written from here, the UI could
        // see Completed before the design exists.
        if (value.Status is not (RunStatus.Completed or RunStatus.Failed))
            _updates.Writer.TryWrite(value);
    }

    /// <summary>Waits until every queued update is written. Safe to call more than once.</summary>
    public async ValueTask DisposeAsync()
    {
        _updates.Writer.TryComplete();
        await _writer;
    }

    private async Task WriteAsync(Guid runId, IServiceScopeFactory scopes, ILogger logger)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DocAgentDbContext>();

        await foreach (var update in _updates.Reader.ReadAllAsync())
        {
            try
            {
                await db.Runs.Where(r => r.Id == runId).ExecuteUpdateAsync(set => set
                    .SetProperty(r => r.Status, update.Status)
                    .SetProperty(r => r.ProgressMessage, update.Message)
                    .SetProperty(r => r.Iteration, update.Iteration));
            }
            catch (Exception ex)
            {
                // Progress is for display; losing one update must not fail the run.
                logger.LogWarning(ex, "Run {RunId}: could not save progress '{Message}'", runId, update.Message);
            }
        }
    }
}

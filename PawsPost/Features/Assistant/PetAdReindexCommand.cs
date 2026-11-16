namespace PawsPost.Features.Assistant;

/// <summary>
/// Command-line trigger for the re-embedding step: <c>dotnet run --project PawsPost -- reindex-ads</c>.
/// It holds no rules, it only calls <see cref="IPetAdReindexService"/> and reports the counts.
/// </summary>
public static class PetAdReindexCommand
{
    public const string Name = "reindex-ads";

    /// <summary>Exit code for a run stopped with Ctrl+C (the usual shell code for SIGINT).</summary>
    public const int CancelledExitCode = 130;

    public const string CancelledMessage = "Cancelled. Run again to continue.";

    /// <returns>0 when no ad failed, 1 when at least one ad failed, 130 when cancelled.</returns>
    public static async Task<int> RunAsync(
        IServiceProvider services, TextWriter output, CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPetAdReindexService>();

        PetAdReindexResult result;
        try
        {
            result = await service.ReindexMissingAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Ctrl+C: stop with a short message instead of a stack trace.
            // The token is already cancelled, so the write must not use it.
            await output.WriteLineAsync(CancelledMessage.AsMemory(), CancellationToken.None);
            return CancelledExitCode;
        }

        var message = $"Re-indexed {result.Indexed} of {result.Missing} ads without a vector.";
        if (result.Failed > 0)
        {
            message += $" {result.Failed} failed; run again to retry.";
        }

        await output.WriteLineAsync(message.AsMemory(), cancellationToken);
        return result.Failed == 0 ? 0 : 1;
    }
}

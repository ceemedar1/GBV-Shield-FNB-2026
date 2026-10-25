using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace GBV_Shield.Services;

/// <summary>
/// Observes intentionally backgrounded tasks so failures are logged instead of
/// becoming unobserved task exceptions later in the process lifetime.
/// </summary>
public static class TaskSafety
{
    public static void Forget(this Task? task, string operation)
    {
        if (task is null) return;
        _ = ObserveAsync(task, operation);
    }

    private static async Task ObserveAsync(Task task, string operation)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is an expected lifecycle outcome.
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{operation}] Background task failed: {ex}");
        }
    }
}

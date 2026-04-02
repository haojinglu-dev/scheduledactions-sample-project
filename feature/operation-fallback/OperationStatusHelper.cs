using Azure.ResourceManager.ComputeSchedule;
using Azure.ResourceManager.ComputeSchedule.Models;
using Azure.ResourceManager.Resources;

namespace ComputeScheduleSampleProject.Feature.OperationFallback;

/// <summary>
/// Shared helper for polling operation status and interpreting fallback results.
/// </summary>
internal static class OperationStatusHelper
{
    /// <summary>
    /// Polls GetOperationStatus until the operation reaches a terminal state,
    /// then prints the result including fallback outcome if applicable.
    /// </summary>
    public static async Task PollAndInterpretAsync(
        SubscriptionResource subscription,
        string location,
        List<string> operationIds)
    {
        var statusRequest = new GetOperationStatusContent(operationIds, Guid.NewGuid().ToString());

        while (true)
        {
            GetOperationStatusResult statusResponse =
                await subscription.GetVirtualMachineOperationStatusAsync(location, statusRequest);

            foreach (var result in statusResponse.Results)
            {
                ResourceOperationDetails details = result.Operation;
                Console.WriteLine($"  VM: {result.ResourceId}");
                Console.WriteLine($"  Operation: {details.OpType}, State: {details.State}");

                if (details.State == ScheduledActionOperationState.Succeeded)
                {
                    Console.WriteLine("  ✅ Operation succeeded — no fallback needed.");
                    return;
                }

                if (details.State == ScheduledActionOperationState.Failed)
                {
                    // Primary operation error
                    if (details.ResourceOperationError is not null)
                    {
                        Console.WriteLine($"  Primary error: {details.ResourceOperationError.ErrorCode} — {details.ResourceOperationError.ErrorDetails}");
                    }

                    // Fallback outcome
                    if (details.FallbackOperationInfo is not null)
                    {
                        FallbackOperationInfo fallback = details.FallbackOperationInfo;

                        if (fallback.Status == ScheduledActionOperationState.Succeeded)
                        {
                            Console.WriteLine($"  ✅ Fallback ({fallback.LastOpType}) succeeded.");
                        }
                        else
                        {
                            Console.WriteLine($"  ❌ Fallback ({fallback.LastOpType}) also failed.");
                            if (fallback.Error is not null)
                            {
                                Console.WriteLine($"     Fallback error: {fallback.Error.ErrorCode} — {fallback.Error.ErrorDetails}");
                            }

                            Console.WriteLine("     Manual intervention may be needed.");
                        }
                    }
                    else
                    {
                        Console.WriteLine("  ❌ Operation failed with no fallback executed.");
                        Console.WriteLine("     This may indicate a non-retriable error.");
                    }

                    return;
                }
            }

            Console.WriteLine("  ⏳ Operation in progress, checking again in 30 seconds...");
            await Task.Delay(TimeSpan.FromSeconds(30));
        }
    }
}

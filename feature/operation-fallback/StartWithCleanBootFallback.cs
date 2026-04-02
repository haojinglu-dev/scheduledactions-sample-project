using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.ComputeSchedule;
using Azure.ResourceManager.ComputeSchedule.Models;
using Azure.ResourceManager.Resources;

namespace ComputeScheduleSampleProject.Feature.OperationFallback;

/// <summary>
/// Demonstrates how to submit a Start operation with a clean-boot fallback.
///
/// When a hibernated VM fails to resume after all retries, setting
/// OnFailureAction to "Start" tells the system to discard the hibernated
/// session state and perform a fresh boot — maximizing the chance of the
/// VM coming back online.
///
/// ⚠️ The fallback discards the hibernated session state. The VM will
///    boot as if it were cold-started.
/// </summary>
internal static class StartWithCleanBootFallback
{
    /// <summary>
    /// Submits a Start request with retry policy and Start (clean-boot) fallback,
    /// then polls for the operation result and interprets the fallback outcome.
    /// </summary>
    public static async Task RunAsync(string subscriptionId, string location, string vmResourceId)
    {
        TokenCredential credential = new DefaultAzureCredential();
        ArmClient client = new(credential);

        ResourceIdentifier subscriptionResourceId = SubscriptionResource.CreateResourceIdentifier(subscriptionId);
        SubscriptionResource subscription = client.GetSubscriptionResource(subscriptionResourceId);

        // 1. Build a RetryPolicy with Start (clean-boot) fallback
        var retryPolicy = new UserRequestRetryPolicy
        {
            RetryWindowInMinutes = 120,
            OnFailureAction = "Start"
        };

        // 2. Build the Start request
        var executionParameters = new ScheduledActionExecutionParameterDetail
        {
            RetryPolicy = retryPolicy
        };

        var resources = new UserRequestResources(new List<string> { vmResourceId });
        string correlationId = Guid.NewGuid().ToString();
        var startRequest = new ExecuteStartContent(executionParameters, resources, correlationId);

        // 3. Submit the Start operation
        Console.WriteLine($"Submitting Start with clean-boot fallback for: {vmResourceId}");
        StartResourceOperationResult response =
            await subscription.ExecuteVirtualMachineStartAsync(location, startRequest);

        // 4. Collect operation IDs from the response
        var operationIds = response.Results
            .Select(r => r.Operation?.OperationId)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToList();

        Console.WriteLine($"Operation submitted. IDs: {string.Join(", ", operationIds)}");

        // 5. Poll for status and interpret the result
        await PollAndInterpretResultAsync(subscription, location, operationIds!);
    }

    /// <summary>
    /// Polls operation status and interprets the fallback result.
    /// When a fallback is configured, the top-level state reflects the primary
    /// operation outcome — even if the fallback succeeds, the state is still
    /// "Failed". Check FallbackOperationInfo to determine the actual outcome.
    /// </summary>
    private static async Task PollAndInterpretResultAsync(
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
                Console.WriteLine($"  VM: {result.ResourceId}");
                Console.WriteLine($"  Operation: {result.OpType}, State: {result.State}");

                if (result.State == ScheduledActionOperationState.Succeeded)
                {
                    Console.WriteLine("  ✅ Start (resume) succeeded — no fallback needed.");
                    return;
                }

                if (result.State == ScheduledActionOperationState.Failed)
                {
                    if (result.ResourceOperationError is not null)
                    {
                        Console.WriteLine($"  Primary error: {result.ResourceOperationError.ErrorCode} — {result.ResourceOperationError.ErrorDetails}");
                    }

                    // Check fallback outcome via the typed FallbackOperationInfo property
                    if (result.FallbackOperationInfo is not null)
                    {
                        var fallback = result.FallbackOperationInfo;

                        if (fallback.Status == "Succeeded")
                        {
                            Console.WriteLine($"  ✅ Fallback ({fallback.LastOpType}) succeeded — VM was clean-booted.");
                            Console.WriteLine("     Note: Hibernated session state was discarded.");
                        }
                        else
                        {
                            Console.WriteLine($"  ❌ Fallback ({fallback.LastOpType}) also failed.");
                            if (fallback.Error is not null)
                            {
                                Console.WriteLine($"     Fallback error: {fallback.Error}");
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

using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.ComputeSchedule;
using Azure.ResourceManager.ComputeSchedule.Models;
using Azure.ResourceManager.Resources;

namespace ComputeScheduleSampleProject.Feature.OperationFallback;

/// <summary>
/// Demonstrates how to submit a Hibernate operation with a Deallocate fallback.
///
/// If the Hibernate operation fails after all retries, the system automatically
/// deallocates the VM instead — ensuring resources are released even when
/// hibernation is not possible.
/// </summary>
internal static class HibernateWithDeallocateFallback
{
    /// <summary>
    /// Submits a Hibernate request with retry policy and Deallocate fallback,
    /// then polls for the operation result and interprets the fallback outcome.
    /// </summary>
    public static async Task RunAsync(string subscriptionId, string location, string vmResourceId)
    {
        TokenCredential credential = new DefaultAzureCredential();
        ArmClient client = new(credential);

        ResourceIdentifier subscriptionResourceId = SubscriptionResource.CreateResourceIdentifier(subscriptionId);
        SubscriptionResource subscription = client.GetSubscriptionResource(subscriptionResourceId);

        // 1. Build a RetryPolicy with Deallocate fallback
        var retryPolicy = new UserRequestRetryPolicy
        {
            RetryWindowInMinutes = 60,
            OnFailureAction = "Deallocate"
        };

        // 2. Build the Hibernate request
        var executionParameters = new ScheduledActionExecutionParameterDetail
        {
            RetryPolicy = retryPolicy
        };

        var resources = new UserRequestResources(new List<string> { vmResourceId });
        string correlationId = Guid.NewGuid().ToString();
        var hibernateRequest = new ExecuteHibernateContent(executionParameters, resources, correlationId);

        // 3. Submit the Hibernate operation
        Console.WriteLine($"Submitting Hibernate with Deallocate fallback for: {vmResourceId}");
        HibernateResourceOperationResult response =
            await subscription.ExecuteVirtualMachineHibernateAsync(location, hibernateRequest);

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
    /// operation outcome. Check FallbackOperationInfo to determine if the
    /// fallback recovered the VM.
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
                    Console.WriteLine("  ✅ Hibernate succeeded — no fallback needed.");
                    return;
                }

                if (result.State == ScheduledActionOperationState.Failed)
                {
                    // Check the primary error
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
                            Console.WriteLine($"  ✅ Fallback ({fallback.LastOpType}) succeeded — VM was deallocated.");
                        }
                        else
                        {
                            Console.WriteLine($"  ❌ Fallback ({fallback.LastOpType}) also failed.");
                            if (fallback.Error is not null)
                            {
                                Console.WriteLine($"     Fallback error: {fallback.Error}");
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("  ❌ Operation failed with no fallback executed.");
                    }

                    return;
                }
            }

            Console.WriteLine("  ⏳ Operation in progress, checking again in 30 seconds...");
            await Task.Delay(TimeSpan.FromSeconds(30));
        }
    }
}

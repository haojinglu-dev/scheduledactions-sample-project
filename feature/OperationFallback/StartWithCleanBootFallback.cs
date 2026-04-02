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
        ArmClient client = new(credential, subscriptionId);

        ResourceIdentifier subscriptionResourceId = SubscriptionResource.CreateResourceIdentifier(subscriptionId);
        SubscriptionResource subscription = client.GetSubscriptionResource(subscriptionResourceId);

        // 1. Build a RetryPolicy with Start (clean-boot) fallback
        var retryPolicy = new UserRequestRetryPolicy
        {
            RetryWindowInMinutes = 30,
            OnFailureAction = "Start"
        };

        // 2. Build the Start request
        var executionParameters = new ScheduledActionExecutionParameterDetail
        {
            RetryPolicy = retryPolicy
        };

        var resources = new UserRequestResources(new List<ResourceIdentifier> { new(vmResourceId) });
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
        await OperationStatusHelper.PollAndInterpretAsync(subscription, location, operationIds!);
    }
}

using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.ComputeSchedule;
using Azure.ResourceManager.ComputeSchedule.Models;
using Azure.ResourceManager.Resources;

namespace ComputeScheduleSampleProject.Feature.OperationFallback;

/// <summary>
/// Demonstrates how to submit a Hibernate operation with a Deallocate fallback
/// but no retry window.
///
/// When retryWindowInMinutes is omitted (or set to 0), the operation is
/// attempted once. If it fails with a retriable error, the system skips
/// retries and goes directly to the fallback action.
/// </summary>
internal static class HibernateFallbackOnlyNoRetry
{
    /// <summary>
    /// Submits a Hibernate request with only onFailureAction set (no retries).
    /// If the single attempt fails, the fallback Deallocate executes immediately.
    /// </summary>
    public static async Task RunAsync(string subscriptionId, string location, string vmResourceId)
    {
        TokenCredential credential = new DefaultAzureCredential();
        ArmClient client = new(credential, subscriptionId);

        ResourceIdentifier subscriptionResourceId = SubscriptionResource.CreateResourceIdentifier(subscriptionId);
        SubscriptionResource subscription = client.GetSubscriptionResource(subscriptionResourceId);

        // No retryWindowInMinutes — only onFailureAction
        var retryPolicy = new UserRequestRetryPolicy
        {
            OnFailureAction = "Deallocate"
        };

        var executionParameters = new ScheduledActionExecutionParameterDetail
        {
            RetryPolicy = retryPolicy
        };

        var resources = new UserRequestResources(new List<ResourceIdentifier> { new(vmResourceId) });
        string correlationId = Guid.NewGuid().ToString();
        var hibernateRequest = new ExecuteHibernateContent(executionParameters, resources, correlationId);

        Console.WriteLine($"Submitting Hibernate with Deallocate fallback (no retries) for: {vmResourceId}");
        HibernateResourceOperationResult response =
            await subscription.ExecuteVirtualMachineHibernateAsync(location, hibernateRequest);

        var operationIds = response.Results
            .Select(r => r.Operation?.OperationId)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToList();

        Console.WriteLine($"Operation submitted. IDs: {string.Join(", ", operationIds)}");

        await OperationStatusHelper.PollAndInterpretAsync(subscription, location, operationIds!);
    }
}

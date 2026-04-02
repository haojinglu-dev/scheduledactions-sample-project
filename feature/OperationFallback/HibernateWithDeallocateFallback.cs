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
        ArmClient client = new(credential, subscriptionId);

        ResourceIdentifier subscriptionResourceId = SubscriptionResource.CreateResourceIdentifier(subscriptionId);
        SubscriptionResource subscription = client.GetSubscriptionResource(subscriptionResourceId);

        // 1. Build a RetryPolicy with Deallocate fallback
        var retryPolicy = new UserRequestRetryPolicy
        {
            RetryWindowInMinutes = 30,
            OnFailureAction = "Deallocate"
        };

        // 2. Build the Hibernate request
        var executionParameters = new ScheduledActionExecutionParameterDetail
        {
            RetryPolicy = retryPolicy
        };

        var resources = new UserRequestResources(new List<ResourceIdentifier> { new(vmResourceId) });
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
        await OperationStatusHelper.PollAndInterpretAsync(subscription, location, operationIds!);
    }
}

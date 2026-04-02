using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.ComputeSchedule;
using Azure.ResourceManager.ComputeSchedule.Models;
using Azure.ResourceManager.Resources;

namespace ComputeScheduleSampleProject.Feature.OperationFallback;

/// <summary>
/// Demonstrates how to submit a Create operation with a Delete fallback.
///
/// If the VM creation fails after all retries, the system automatically
/// deletes the partially-created VM to clean up resources.
/// </summary>
internal static class CreateWithDeleteFallback
{
    /// <summary>
    /// Submits a Create request with retry policy and Delete fallback,
    /// then polls for the operation result and interprets the fallback outcome.
    /// </summary>
    /// <param name="simulationPolicy">
    /// Optional simulation policy to inject simulated failures for testing.
    /// </param>
    public static async Task RunAsync(string subscriptionId, string location, ResourceProvisionPayload resourceConfig, SimulationProfilePolicy? simulationPolicy = null)
    {
        TokenCredential credential = new DefaultAzureCredential();

        ArmClientOptions options = new();
        if (simulationPolicy is not null)
        {
            options.AddPolicy(simulationPolicy, HttpPipelinePosition.PerCall);
        }

        ArmClient client = new(credential, subscriptionId, options);

        ResourceIdentifier subscriptionResourceId = SubscriptionResource.CreateResourceIdentifier(subscriptionId);
        SubscriptionResource subscription = client.GetSubscriptionResource(subscriptionResourceId);

        // 1. Build a RetryPolicy with Delete fallback
        var retryPolicy = new UserRequestRetryPolicy
        {
            RetryWindowInMinutes = 30,
            OnFailureAction = "Delete"
        };

        // 2. Build the Create request
        var executionParameters = new ScheduledActionExecutionParameterDetail
        {
            RetryPolicy = retryPolicy
        };

        string correlationId = Guid.NewGuid().ToString();
        var createRequest = new ExecuteCreateContent(resourceConfig, executionParameters)
        {
            CorrelationId = correlationId
        };

        // 3. Submit the Create operation
        Console.WriteLine($"Submitting Create with Delete fallback for prefix: {resourceConfig.ResourcePrefix}");
        CreateResourceOperationResult response =
            await subscription.ExecuteVirtualMachineCreateOperationAsync(location, createRequest);

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

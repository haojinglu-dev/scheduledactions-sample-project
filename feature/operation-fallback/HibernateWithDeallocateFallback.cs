using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.ComputeSchedule;
using Azure.ResourceManager.ComputeSchedule.Models;
using Azure.ResourceManager.Resources;
using System.ClientModel.Primitives;
using System.Text.Json;

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
    /// <param name="simulationPolicy">
    /// Optional simulation policy to inject simulated failures for testing.
    /// Use SimulationProfilePolicy.HibernateRetryFailsFallbackSucceeds() to demo fallback.
    /// </param>
    public static async Task RunAsync(string subscriptionId, string location, string vmResourceId, SimulationProfilePolicy? simulationPolicy = null)
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

            // Serialize to JSON for full access to all fields including FallbackOperationInfo
            BinaryData rawResponse = ModelReaderWriter.Write(statusResponse, ModelReaderWriterOptions.Json);
            using JsonDocument doc = JsonDocument.Parse(rawResponse);

            foreach (JsonElement result in doc.RootElement.GetProperty("results").EnumerateArray())
            {
                string resourceId = result.GetProperty("resourceId").GetString() ?? "";
                string opType = result.TryGetProperty("operation", out JsonElement op) && op.TryGetProperty("opType", out JsonElement ot) ? ot.GetString() ?? "" : "";
                string state = op.TryGetProperty("state", out JsonElement st) ? st.GetString() ?? "" : "";

                Console.WriteLine($"  VM: {resourceId}");
                Console.WriteLine($"  Operation: {opType}, State: {state}");

                if (state == "Succeeded")
                {
                    Console.WriteLine("  ✅ Hibernate succeeded — no fallback needed.");
                    return;
                }

                if (state == "Failed")
                {
                    if (op.TryGetProperty("resourceOperationError", out JsonElement error))
                    {
                        Console.WriteLine($"  Primary error: {error.GetProperty("errorCode").GetString()} — {error.GetProperty("errorDetails").GetString()}");
                    }

                    if (op.TryGetProperty("fallbackOperationInfo", out JsonElement fallback))
                    {
                        string fallbackStatus = fallback.GetProperty("status").GetString() ?? "Unknown";
                        string fallbackOp = fallback.GetProperty("lastOpType").GetString() ?? "Unknown";

                        if (fallbackStatus == "Succeeded")
                        {
                            Console.WriteLine($"  ✅ Fallback ({fallbackOp}) succeeded — VM was deallocated.");
                        }
                        else
                        {
                            Console.WriteLine($"  ❌ Fallback ({fallbackOp}) also failed.");
                            if (fallback.TryGetProperty("error", out JsonElement fallbackError))
                            {
                                Console.WriteLine($"     Fallback error: {fallbackError}");
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

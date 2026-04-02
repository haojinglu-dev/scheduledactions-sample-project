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
    /// <param name="simulationPolicy">
    /// Optional simulation policy to inject simulated failures for testing.
    /// Use SimulationProfilePolicy.StartRetryFailsFallbackSucceeds() to demo fallback.
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
                    Console.WriteLine("  ✅ Start (resume) succeeded — no fallback needed.");
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
                            Console.WriteLine($"  ✅ Fallback ({fallbackOp}) succeeded — VM was clean-booted.");
                            Console.WriteLine("     Note: Hibernated session state was discarded.");
                        }
                        else
                        {
                            Console.WriteLine($"  ❌ Fallback ({fallbackOp}) also failed.");
                            if (fallback.TryGetProperty("error", out JsonElement fallbackError))
                            {
                                Console.WriteLine($"     Fallback error: {fallbackError}");
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

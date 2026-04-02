using Azure.Core;
using Azure.Core.Pipeline;
using System.Text;
using System.Text.Json;

namespace ComputeScheduleSampleProject.Feature.OperationFallback;

/// <summary>
/// An HttpPipelinePolicy that injects the x-simulation-profile header into
/// Scheduled Actions API requests. This allows deterministic testing of retry
/// and fallback behavior without needing real platform failures.
///
/// Requirements:
///   - The target subscription must be in the SimulationTrafficSubscriptionAllowlist
///   - AllowSimulationTraffic must be enabled on the target cluster
/// </summary>
internal class SimulationProfilePolicy : HttpPipelinePolicy
{
    private readonly string _base64Profile;

    private SimulationProfilePolicy(string base64Profile)
    {
        _base64Profile = base64Profile;
    }

    public override void Process(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
    {
        message.Request.Headers.SetValue("x-simulation-profile", _base64Profile);
        ProcessNext(message, pipeline);
    }

    public override ValueTask ProcessAsync(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
    {
        message.Request.Headers.SetValue("x-simulation-profile", _base64Profile);
        return ProcessNextAsync(message, pipeline);
    }

    /// <summary>
    /// Creates a policy from a raw simulation profile object.
    /// The profile is serialized to JSON and base64-encoded.
    /// </summary>
    public static SimulationProfilePolicy Create(SimulationProfile profile)
    {
        string json = JsonSerializer.Serialize(profile, new JsonSerializerOptions
        {
            PropertyNamingPolicy = null, // PascalCase to match Bond struct field names
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
        string base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        return new SimulationProfilePolicy(base64);
    }

    // --- Pre-built scenarios ---

    /// <summary>
    /// Hibernate fails on attempt 1, retry succeeds on attempt 2.
    /// No fallback is executed.
    /// </summary>
    public static SimulationProfilePolicy HibernateRetrySucceeds() => Create(new SimulationProfile
    {
        Steps =
        [
            new() { Attempt = 1, AsyncError = new() { ErrorCode = "SimulatedAsyncHibernateFailure", ErrorMessage = "Simulated async hibernate failure - attempt 1" } },
            new() { Attempt = 2 }
        ]
    });

    /// <summary>
    /// Hibernate fails on attempts 1 and 2, fallback Deallocate succeeds on attempt 3.
    /// Demonstrates: primary Failed + fallbackOperationInfo.status = Succeeded.
    /// </summary>
    public static SimulationProfilePolicy HibernateRetryFailsFallbackSucceeds() => Create(new SimulationProfile
    {
        Steps =
        [
            new() { Attempt = 1, AsyncError = new() { ErrorCode = "SimulatedAsyncHibernateFailure", ErrorMessage = "Simulated async hibernate failure - attempt 1" } },
            new() { Attempt = 2, AsyncError = new() { ErrorCode = "SimulatedAsyncHibernateFailure", ErrorMessage = "Simulated async hibernate failure - attempt 2" } },
            new() { Attempt = 3 }
        ]
    });

    /// <summary>
    /// Hibernate fails on attempts 1 and 2, fallback Deallocate also fails on attempt 3.
    /// Demonstrates: both primary and fallback Failed.
    /// </summary>
    public static SimulationProfilePolicy HibernateRetryFailsFallbackFails() => Create(new SimulationProfile
    {
        Steps =
        [
            new() { Attempt = 1, AsyncError = new() { ErrorCode = "SimulatedAsyncHibernateFailure", ErrorMessage = "Simulated async hibernate failure - attempt 1" } },
            new() { Attempt = 2, AsyncError = new() { ErrorCode = "SimulatedAsyncHibernateFailure", ErrorMessage = "Simulated async hibernate failure - attempt 2" } },
            new() { Attempt = 3, AsyncError = new() { ErrorCode = "SimulatedAsyncDeallocateFailure", ErrorMessage = "Simulated async deallocate failure - fallback" } }
        ]
    });

    /// <summary>
    /// Start (resume) fails on attempts 1 and 2, fallback Start (clean boot) succeeds on attempt 3.
    /// Demonstrates: resume Failed + clean-boot fallback Succeeded.
    /// </summary>
    public static SimulationProfilePolicy StartRetryFailsFallbackSucceeds() => Create(new SimulationProfile
    {
        Steps =
        [
            new() { Attempt = 1, AsyncError = new() { ErrorCode = "SimulatedAsyncStartFailure", ErrorMessage = "Simulated async start failure - attempt 1" } },
            new() { Attempt = 2, AsyncError = new() { ErrorCode = "SimulatedAsyncStartFailure", ErrorMessage = "Simulated async start failure - attempt 2" } },
            new() { Attempt = 3 }
        ]
    });
}

// --- Simulation profile model ---

internal class SimulationProfile
{
    public List<SimulationStep>? Steps { get; set; }
}

internal class SimulationStep
{
    public int Attempt { get; set; }
    public SimulationError? SyncError { get; set; }
    public SimulationError? AsyncError { get; set; }
}

internal class SimulationError
{
    public string ErrorCode { get; set; } = "";
    public string ErrorMessage { get; set; } = "";
}

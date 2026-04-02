using ComputeScheduleSampleProject.Feature.OperationFallback;

namespace ComputeScheduleSampleProject
{
    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            string subscriptionId = "d3b6401d-a175-4370-95d6-8c0dac1a9fdf";
            string location = "eastus2euap";
            string vmResourceId = $"/subscriptions/{subscriptionId}/resourceGroups/hibernate-fallback-demo/providers/Microsoft.Compute/virtualMachines/hibernate-demo-vm";

            // Use simulation mode to deterministically trigger fallback
            await HibernateWithDeallocateFallback.RunAsync(
                subscriptionId,
                location,
                vmResourceId,
                simulationPolicy: SimulationProfilePolicy.HibernateRetryFailsFallbackSucceeds());
        }
    }
}
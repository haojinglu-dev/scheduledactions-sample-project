using ComputeScheduleSampleProject.Feature.OperationFallback;

namespace ComputeScheduleSampleProject
{
    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            string subscriptionId = "d3b6401d-a175-4370-95d6-8c0dac1a9fdf";
            string location = "centraluseuap";
            string vmResourceId = $"/subscriptions/{subscriptionId}/resourceGroups/hibernate-fallback-demo-cuse/providers/Microsoft.Compute/virtualMachines/hibernate-demo-vm";

            // Run without simulation — real operation
            await HibernateWithDeallocateFallback.RunAsync(
                subscriptionId,
                location,
                vmResourceId);
        }
    }
}
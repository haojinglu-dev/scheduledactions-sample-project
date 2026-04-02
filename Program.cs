using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.ComputeSchedule;
using Azure.ResourceManager.ComputeSchedule.Models;
using Azure.ResourceManager.Resources;
using System.ClientModel.Primitives;

namespace ComputeScheduleSampleProject
{
    internal static class Program
    {
        /// <summary>
        /// This project shows a sample use case for the ComputeSchedule use case
        /// </summary>
        private static void Main(string[] args)
        {
            // Testing the ExecuteStart operation
            var executeStartRequest = new ExecuteStartContent(new ExecutionParameters(), new Resources(new List<string>() { "/subscriptions/afe495ca-b99a-4e36-86c8-9e0e41697f1c/resourcegroups/Kronox_SyntheticRuns_EastAsia/providers/Microsoft.Compute/virtualMachines/nneka-computeschedule-testvm" }), Guid.NewGuid().ToString());
            var executeStartResult = TestExecuteStartAsync("eastasia", executeStartRequest, "afe495ca-b99a-4e36-86c8-9e0e41697f1c").Result;

            var executeStartProcessedData = ModelReaderWriter.Write(executeStartResult, ModelReaderWriterOptions.Json);
            Console.WriteLine(executeStartProcessedData.ToString());


            // Testing the GetOperationStatus operation
            var allOperationIds = executeStartResult.Results.Select(result => result.Operation?.OperationId).Where(operationId => !string.IsNullOrEmpty(operationId)).ToList();
            var getOpsStatusReq = new GetOperationStatusContent(allOperationIds, Guid.NewGuid().ToString());
            var getOperationStatus = TestGetOpsStatusAsync("eastasia", getOpsStatusReq, "afe495ca-b99a-4e36-86c8-9e0e41697f1c").Result;

            var getOperationStatusProcessedData = ModelReaderWriter.Write(getOperationStatus, ModelReaderWriterOptions.Json);
            Console.WriteLine(getOperationStatusProcessedData.ToString());

        }
        private static async Task<StartResourceOperationResponse> TestExecuteStartAsync(string location, ExecuteStartContent executeStartRequest, string subid)
        {
            TokenCredential cred = new DefaultAzureCredential();
            ArmClient client = new(cred);
            string subscriptionId = subid;
            ResourceIdentifier subscriptionResourceId = SubscriptionResource.CreateResourceIdentifier(subscriptionId);
            SubscriptionResource subscriptionResource = client.GetSubscriptionResource(subscriptionResourceId);
            string locationparameter = location;
            ExecuteStartContent content = executeStartRequest;
            StartResourceOperationResponse? result;
            try
            {
                result = await subscriptionResource.VirtualMachinesExecuteStartScheduledActionAsync(locationparameter, content);
                Console.WriteLine($"Succeeded: {result}");

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.InnerException?.Message);
                throw;
            }

            return result;
        }

        private static async Task<DeallocateResourceOperationResponse> TestExecuteDeallocateAsync(string location, ExecuteDeallocateContent executeDeallocateRequest, string subid)
        {
            TokenCredential cred = new DefaultAzureCredential();
            ArmClient client = new(cred);
            string subscriptionId = subid;
            ResourceIdentifier subscriptionResourceId = SubscriptionResource.CreateResourceIdentifier(subscriptionId);
            SubscriptionResource subscriptionResource = client.GetSubscriptionResource(subscriptionResourceId);
            string locationparameter = location;
            ExecuteDeallocateContent content = executeDeallocateRequest;

            DeallocateResourceOperationResponse? result;
            try
            {
                result = await subscriptionResource.VirtualMachinesExecuteDeallocateScheduledActionAsync(locationparameter, content);
                Console.WriteLine($"Succeeded: {result}");

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.InnerException?.Message);
                throw;
            }

            return result;

        }

        private static async Task<HibernateResourceOperationResponse> TestExecuteHibernateAsync(string location, ExecuteHibernateContent executeHibernateRequest, string subid)
        {
            TokenCredential cred = new DefaultAzureCredential();
            ArmClient client = new(cred);
            string subscriptionId = subid;
            ResourceIdentifier subscriptionResourceId = SubscriptionResource.CreateResourceIdentifier(subscriptionId);
            SubscriptionResource subscriptionResource = client.GetSubscriptionResource(subscriptionResourceId);
            string locationparameter = location;
            ExecuteHibernateContent content = executeHibernateRequest;
            HibernateResourceOperationResponse? result;

            try
            {
                result = await subscriptionResource.VirtualMachinesExecuteHibernateScheduledActionAsync(locationparameter, content);
                Console.WriteLine($"Succeeded: {result}");

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.InnerException?.Message);
                throw;
            }

            return result;
        }

        public static async Task<CancelOperationsResponse> TestCancelOpsAsync(string location, CancelOperationsContent cancelOpsRequest, string subid)
        {
            TokenCredential cred = new DefaultAzureCredential();

            ArmClient client = new(cred);
            string subscriptionId = subid;
            ResourceIdentifier subscriptionResourceId = SubscriptionResource.CreateResourceIdentifier(subscriptionId);
            SubscriptionResource subscriptionResource = client.GetSubscriptionResource(subscriptionResourceId);
            string locationparameter = location;
            CancelOperationsContent content = cancelOpsRequest;
            CancelOperationsResponse? result;

            try
            {
                result = await subscriptionResource.VirtualMachinesCancelOperationsScheduledActionAsync(locationparameter, content);
                Console.WriteLine($"Succeeded: {result}");

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.InnerException?.Message);
                throw;
            }

            return result;
        }

        public static async Task<GetOperationStatusResponse> TestGetOpsStatusAsync(string location, GetOperationStatusContent getOpsStatusRequest, string subid)
        {
            TokenCredential cred = new DefaultAzureCredential();

            ArmClient client = new(cred);
            string subscriptionId = subid;
            ResourceIdentifier subscriptionResourceId = SubscriptionResource.CreateResourceIdentifier(subscriptionId);
            SubscriptionResource subscriptionResource = client.GetSubscriptionResource(subscriptionResourceId);
            string locationparameter = location;
            GetOperationStatusContent content = getOpsStatusRequest;
            GetOperationStatusResponse? result;

            try
            {
                result = await subscriptionResource.VirtualMachinesGetOperationStatusScheduledActionAsync(locationparameter, content);
                Console.WriteLine($"Succeeded: {result}");

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.InnerException?.Message);
                throw;
            }

            return result;
        }
    }
}
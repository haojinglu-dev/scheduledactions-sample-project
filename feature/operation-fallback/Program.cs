using ComputeScheduleSampleProject.Feature.OperationFallback;

// ---------------------------------------------------------------
// Operation Fallback Samples
//
// Uncomment the scenario you want to run and update the parameters.
// ---------------------------------------------------------------

string subscriptionId = "your-subscription-id";
string location = "eastus";
string vmResourceId = $"/subscriptions/{subscriptionId}/resourceGroups/your-rg/providers/Microsoft.Compute/virtualMachines/your-vm";

// Scenario 1: Hibernate with Deallocate fallback
await HibernateWithDeallocateFallback.RunAsync(subscriptionId, location, vmResourceId);

// Scenario 2: Start with clean-boot fallback
// await StartWithCleanBootFallback.RunAsync(subscriptionId, location, vmResourceId);

// Scenario 3: Create with Delete fallback
// var resourceConfig = new Azure.ResourceManager.ComputeSchedule.Models.ResourceProvisionPayload("your-base-profile-arm-id");
// await CreateWithDeleteFallback.RunAsync(subscriptionId, location, resourceConfig);

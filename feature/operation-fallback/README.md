# Operation Fallback Examples

These samples demonstrate how to use the **retry policy with fallback** feature in the Azure Scheduled Actions (ComputeSchedule) SDK.

## What is a fallback?

When a VM operation (Start, Hibernate, etc.) fails after all retry attempts, a **fallback action** provides a safety net — an alternative operation that leaves the VM in a known, safe state.

## Supported fallback chains

| Operation | `onFailureAction` | Fallback behavior |
|-----------|-------------------|-------------------|
| **Start** | `"Start"` | If resume fails, performs a clean boot (hibernated state is discarded) |
| **Hibernate** | `"Deallocate"` | If hibernate fails, deallocates the VM instead |
| **Create** | `"Delete"` | If create fails, deletes the partially-created VM |

## Samples

| File | Description |
|------|-------------|
| [HibernateWithDeallocateFallback.cs](HibernateWithDeallocateFallback.cs) | Hibernate a VM with automatic Deallocate if hibernate fails |
| [StartWithCleanBootFallback.cs](StartWithCleanBootFallback.cs) | Resume a hibernated VM with automatic clean boot if resume fails |

## SDK version

These samples require `Azure.ResourceManager.ComputeSchedule` version **1.2.0-alpha.20260331.1** or later, which introduces:
- `UserRequestRetryPolicy` with `OnFailureAction` property
- `FallbackOperationInfo` on `ResourceOperationDetails` for typed fallback results

## Usage

```csharp
// Hibernate with Deallocate fallback
await HibernateWithDeallocateFallback.RunAsync(
    subscriptionId: "your-subscription-id",
    location: "eastus",
    vmResourceId: "/subscriptions/.../providers/Microsoft.Compute/virtualMachines/my-vm");

// Start with clean-boot fallback
await StartWithCleanBootFallback.RunAsync(
    subscriptionId: "your-subscription-id",
    location: "eastus",
    vmResourceId: "/subscriptions/.../providers/Microsoft.Compute/virtualMachines/my-vm");
```

## For more details

See the full [Retry and Fallback Policy documentation](https://aka.ms/scheduledactions/retry-fallback-policy) for retry window semantics, error handling, and response interpretation.

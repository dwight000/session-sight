using Xunit;

namespace SessionSight.FunctionalTests;

/// <summary>
/// Collection definition for tests that must run sequentially.
/// Extraction tests are resource-intensive and cannot run in parallel.
/// </summary>
[CollectionDefinition("Sequential", DisableParallelization = true)]
public class SequentialCollection : ICollectionFixture<object>
{
}

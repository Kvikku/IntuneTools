using Xunit;

// Disable parallel test execution to prevent flaky failures from shared mutable
// global state (Variables.totalTimeSavedInSeconds, counter fields, etc.).
[assembly: CollectionBehavior(DisableTestParallelization = true)]

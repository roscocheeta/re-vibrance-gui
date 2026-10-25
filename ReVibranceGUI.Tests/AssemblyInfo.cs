using Xunit;

// Disable parallel test execution across test collections to ensure STA thread safety for WPF tests.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

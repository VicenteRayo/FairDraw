using Xunit;

// FairDrawApiFactory hands its SQLite connection to Program.cs via a single static hook
// (FairDraw.Api.Testing.TestHooks), consulted only while building the host under the "Testing"
// environment. Running test collections in parallel would let two factories race on that static
// field, so integration tests in this assembly run sequentially — acceptable at this test count.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

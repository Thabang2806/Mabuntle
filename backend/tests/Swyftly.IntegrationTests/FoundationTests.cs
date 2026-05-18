namespace Swyftly.IntegrationTests;

public class FoundationTests
{
    [Fact]
    public void ApiAssembly_IsReachableFromIntegrationTests()
    {
        Assert.Equal("Swyftly.Api", typeof(Program).Assembly.GetName().Name);
    }
}

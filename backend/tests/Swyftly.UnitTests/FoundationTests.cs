using Swyftly.Domain;

namespace Swyftly.UnitTests;

public class FoundationTests
{
    [Fact]
    public void DomainAssemblyReference_ExposesDomainAssembly()
    {
        Assert.Equal("Swyftly.Domain", DomainAssemblyReference.Assembly.GetName().Name);
    }
}

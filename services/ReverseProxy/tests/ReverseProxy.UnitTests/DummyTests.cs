using Xunit;

namespace ReverseProxy.UnitTests;

public class DummyTests
{
    [Fact]
    public void DummyTest_AlwaysPasses()
    {
        Assert.Equal(2, 1 + 1);
    }
}

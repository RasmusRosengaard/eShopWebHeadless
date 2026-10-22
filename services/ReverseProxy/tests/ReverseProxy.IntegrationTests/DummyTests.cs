using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ReverseProxy.IntegrationTests;

[TestClass]
public class DummyTests
{
    [TestMethod]
    public void DummyTest_AlwaysPasses()
    {
        Assert.AreEqual(2, 1 + 1);
    }
}

using Unknown6656.Testing;


namespace EDS.CSharp.UnitTests;


[TestClass]
public class UnitTest1
{
    [TestMethod]
    [TestWith(0)]
    [TestWith(1)]
    [TestWith(-1)]
    [TestWith(2)]
    [TestWith(-2)]
    [TestWith(7)]
    [TestWith(-7)]
    [TestWith(10)]
    [TestWith(-10)]
    [TestWith(15)]
    [TestWith(16)]
    [TestWith(31)]
    [TestWith(32)]
    [TestWith(511)]
    [TestWith(512)]
    [TestWith(-512)]
    [TestWith(10_000)]
    [TestWith(-10_000)]
    [TestWith(-0xdead)]
    [TestWith(0xbeef)]
    [TestWith(int.MaxValue)]
    [TestWith(int.MinValue)]
    public void test_int32(int value)
    {
        byte[] bytes = Serializer.Serialize(value);
        int value2 = Serializer.Deserialize<int>(bytes);

        Assert.AreEqual(value, value2);
    }
}
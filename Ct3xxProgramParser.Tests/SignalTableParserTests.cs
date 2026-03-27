// Provides Signal Table Parser Tests for the program parser test project support code.
using System.Linq;
using Ct3xxProgramParser.Parsing;

namespace Ct3xxProgramParser.Tests;

[TestClass]
/// <summary>
/// Represents the signal table parser tests.
/// </summary>
public sealed class SignalTableParserTests
{
    [TestMethod]
    /// <summary>
    /// Determines whether the parse multi module signal table condition is met.
    /// </summary>
    public void ShouldParseMultiModuleSignalTable()
    {
        var parser = new SignalTableParser();
        var path = TestData.GetProgramFilePath(@"UIF I2C Test\Signaltable.ctsit");
        var table = parser.ParseTable(path);

        Assert.AreEqual(2, table.Modules.Count, "Expected two module blocks in UIF I2C test signal table.");

        var uif = table.Modules.First(m => m.Name == "UIF");
        Assert.AreEqual(44, uif.Assignments.Count, "Unexpected UIF channel count.");

        var sc3 = table.Modules.First(m => m.Name == "SC3");
        Assert.IsTrue(sc3.Assignments.Any(a => a.Channel == 69 && a.Name == "SPI_MISO"));
        var spiMiso = sc3.Assignments.Single(a => a.Channel == 69 && a.Name == "SPI_MISO");
        Assert.AreEqual("SC3", spiMiso.ModuleName);
        Assert.AreEqual("SC3_69_0", spiMiso.CanonicalName);
    }
}

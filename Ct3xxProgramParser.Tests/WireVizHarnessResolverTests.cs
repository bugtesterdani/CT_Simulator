using System.IO;
using System.Linq;
using Ct3xxProgramParser.Programs;
using Ct3xxSimulator.Simulation.WireViz;

namespace Ct3xxProgramParser.Tests;

[TestClass]
public sealed class WireVizHarnessResolverTests
{
    [TestMethod]
    public void ShouldResolveSignalTableEntryThroughWireVizHarness()
    {
        var parser = new Ct3xxProgramFileParser();
        var programPath = TestData.GetProgramFilePath(@"UIF I2C Test\UIF I2C Test.ctxprg");
        var fileSet = parser.Load(programPath);

        var tempFile = Path.Combine(Path.GetTempPath(), $"wireviz-{Guid.NewGuid():N}.yaml");
        try
        {
            File.WriteAllText(tempFile, """
connectors:
  CT3:
    bgcolor: WH
    pinlabels: [SC3_69_0, SC3_68_0]
  DevicePort:
    bgcolor: YE
    pinlabels: [MISO_PIN, MOSI_PIN]
  HARNESS:
    bgcolor: YE
    pinlabels: [MISO_ALT, MOSI_ALT]
connections:
  -
    - CT3: [1, 2]
    - DevicePort: [1, 2]
  -
    - CT3: [1, 2]
    - HARNESS: [1, 2]
""");

            var resolver = WireVizHarnessResolver.Create(fileSet, new[] { tempFile });

            Assert.IsTrue(resolver.TryResolve("SPI_MISO", out var resolutions));
            var resolution = resolutions.Single();
            Assert.AreEqual("CT3.1", resolution.Source.Key);
            Assert.AreEqual("SC3_69_0", resolution.Source.PinLabel);
            CollectionAssert.AreEquivalent(new[] { "DevicePort.1", "HARNESS.1" }, resolution.Targets.Select(target => target.Key).ToArray());
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}

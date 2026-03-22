using System.IO;
using System.Linq;
using Ct3xxProgramParser.Programs;

namespace Ct3xxProgramParser.Tests;

[TestClass]
public sealed class ProgramFileParserTests
{
    [TestMethod]
    public void ShouldLoadProgramAndResolveSignalTables()
    {
        var parser = new Ct3xxProgramFileParser();
        var programPath = TestData.GetProgramFilePath(@"UIF I2C Test\UIF I2C Test.ctxprg");
        var result = parser.Load(programPath);

        Assert.IsTrue(File.Exists(result.ProgramPath));
        var signalTables = result.SignalTables.ToList();
        Assert.AreEqual(1, signalTables.Count, "Program should link exactly one signal table.");

        var tableDoc = signalTables.Single();
        StringAssert.EndsWith(tableDoc.FileName, "Signaltable.ctsit");
        Assert.AreEqual("SIT$", tableDoc.TableDefinition?.Id);
        Assert.AreEqual("SC3", tableDoc.Table.Modules.Last().Name);
    }
}

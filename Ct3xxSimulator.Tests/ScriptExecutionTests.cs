// Provides Script Execution Tests for the simulator test project support code.
using System.Xml;
using Ct3xxProgramParser.Model;
using Ct3xxProgramParser.Programs;
using Ct3xxSimulator.Simulation;

namespace Ct3xxSimulator.Tests;

[TestClass]
/// <summary>
/// Represents the script execution tests.
/// </summary>
public sealed class ScriptExecutionTests
{
    [TestMethod]
    /// <summary>
    /// Executes expression evaluator should resolve com tcp demo style write file expression.
    /// </summary>
    public void ExpressionEvaluator_ShouldResolve_ComTcpDemoStyleWriteFileExpression()
    {
        var workspace = CreateTempWorkspace();
        try
        {
            var context = new SimulationContext();
            context.SetProgramContext(Path.Combine(workspace, "Program.ctxprg"));
            context.SetValue("SERIALPORT", "COM30");
            context.SetValue("REMOTEHOST", string.Empty);
            context.SetValue("SOCKETPORT", "5000");
            var evaluator = new ExpressionEvaluator(context);

            var expression = "WriteFile(PathCombine(TestProgramPath(),'Com2TCP\\startcom2tcp.cmd'), '@ECHO OFF' & Char(10) & Char(13) & 'cd /D %~dp0' & Char(10) & Char(13) & 'start com2tcp.exe ' & '\\\\.\\\\' & SERIALPORT & ' ' & REMOTEHOST & ' ' & SOCKETPORT ,'c')";
            var result = evaluator.Evaluate(expression);

            var generatedPath = Path.Combine(workspace, "Com2TCP", "startcom2tcp.cmd");
            Assert.IsTrue(File.Exists(generatedPath), "WriteFile sollte die Script-Datei erzeugen.");

            var content = File.ReadAllText(generatedPath);
            StringAssert.Contains(content, "@ECHO OFF");
            StringAssert.Contains(content, "cd /D %~dp0");
            StringAssert.Contains(content, @"start com2tcp.exe \\.\\COM30  5000");
            Assert.IsTrue(Convert.ToDouble(result) > 0d, "WriteFile sollte eine Dateilaenge > 0 zurueckgeben.");
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [TestMethod]
    /// <summary>
    /// Executes ecll should pass when exit code evaluation is disabled.
    /// </summary>
    public void Ecll_ShouldPass_WhenExitCodeEvaluationIsDisabled()
    {
        var workspace = CreateTempWorkspace();
        try
        {
            var scriptPath = Path.Combine(workspace, "ok.cmd");
            File.WriteAllText(scriptPath, "@echo off\r\nexit /b 7\r\n");

            var observer = RunSingleExecutableTest(workspace, scriptPath, waitForFinish: true, evaluateExitCode: false);

            Assert.AreEqual(1, observer.Evaluations.Count);
            Assert.AreEqual(TestOutcome.Pass, observer.Evaluations[0].Outcome);
            StringAssert.Contains(observer.Evaluations[0].Details ?? string.Empty, "ExitCode=7");
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [TestMethod]
    /// <summary>
    /// Executes ecll should fail when evaluated exit code differs from expected.
    /// </summary>
    public void Ecll_ShouldFail_WhenEvaluatedExitCodeDiffersFromExpected()
    {
        var workspace = CreateTempWorkspace();
        try
        {
            var scriptPath = Path.Combine(workspace, "fail.cmd");
            File.WriteAllText(scriptPath, "@echo off\r\nexit /b 5\r\n");

            var observer = RunSingleExecutableTest(workspace, scriptPath, waitForFinish: true, evaluateExitCode: true, expectedExitCode: 0);

            Assert.AreEqual(1, observer.Evaluations.Count);
            Assert.AreEqual(TestOutcome.Fail, observer.Evaluations[0].Outcome);
            Assert.AreEqual(5d, observer.Evaluations[0].MeasuredValue!.Value, 0.0001d);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    /// <summary>
    /// Executes RunSingleExecutableTest.
    /// </summary>
    private static SimulationObserverSpy RunSingleExecutableTest(
        string workspace,
        string scriptPath,
        bool waitForFinish,
        bool evaluateExitCode,
        int? expectedExitCode = null)
    {
        var programPath = Path.Combine(workspace, "Program.ctxprg");
        File.WriteAllText(programPath, "<placeholder />");

        var parameters = new TestParameters
        {
            Name = "Run Script",
            DrawingReference = "Run Script",
            AdditionalAttributes = BuildAttributes(
                ("ExeFileName", $"'{scriptPath}'"),
                ("WaitForFinish", waitForFinish ? "yes" : "no"),
                ("EvaluateExitCode", evaluateExitCode ? "yes" : "no"),
                ("ExpectedExitCode", (expectedExitCode ?? 0).ToString()))
        };

        var program = new Ct3xxProgram
        {
            RootItems =
            {
                new Test
                {
                    Id = "ECLL",
                    Parameters = parameters
                }
            }
        };

        var observer = new SimulationObserverSpy();
        var simulator = new Ct3xxProgramSimulator(observer: observer);
        simulator.Run(new Ct3xxProgramFileSet(programPath, program, Array.Empty<Ct3xxProgramParser.Documents.Ct3xxFileDocument>()), 1);
        return observer;
    }

    /// <summary>
    /// Executes BuildAttributes.
    /// </summary>
    private static XmlAttribute[] BuildAttributes(params (string Name, string Value)[] values)
    {
        var document = new XmlDocument();
        return values.Select(value =>
        {
            var attribute = document.CreateAttribute(value.Name);
            attribute.Value = value.Value;
            return attribute;
        }).ToArray();
    }

    /// <summary>
    /// Executes CreateTempWorkspace.
    /// </summary>
    private static string CreateTempWorkspace()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ct3xx-script-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}

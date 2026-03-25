using Ct3xxProgramParser.Programs;
using Ct3xxSimulator.Simulation;
using Ct3xxSimulator.Simulation.WireViz;
using Ct3xxSimulator.Validation;
using System.Globalization;

namespace Ct3xxSimulator.Tests;

[TestClass]
public sealed class SimulatorIntegrationTests
{
    [TestMethod]
    public void Validation_ShouldAccept_SimtestScenario()
    {
        var issues = SimulationConfigurationValidator.Validate(
            TestData.GetPath(@"simtest\ct3xx\Simulator_Test.ctxprg"),
            TestData.GetPath(@"simtest\wireplan"),
            TestData.GetPath(@"simtest\wireplan"),
            TestData.GetPath(@"simtest\device\devices\IKI_good.json"));

        Assert.AreEqual(0, issues.Count, string.Join(Environment.NewLine, issues));
    }

    [TestMethod]
    public void RuntimeTargets_ShouldPreserve_LogicalSignalNames_ForSimtest()
    {
        var parser = new Ct3xxProgramFileParser();
        var fileSet = parser.Load(TestData.GetPath(@"simtest\ct3xx\Simulator_Test.ctxprg"));

        Environment.SetEnvironmentVariable("CT3XX_WIREVIZ_ROOT", TestData.GetPath(@"simtest\wireplan"));
        Environment.SetEnvironmentVariable("CT3XX_SIMULATION_MODEL_ROOT", TestData.GetPath(@"simtest\wireplan"));

        var resolver = WireVizHarnessResolver.Create(fileSet);
        var signalState = new Dictionary<string, object?> { ["VCC_Plus"] = 24d, ["ADC_IN"] = 4d, ["UIF_OUT1"] = 24d };
        var signalTimes = new Dictionary<string, long> { ["VCC_Plus"] = 0L, ["ADC_IN"] = 0L, ["UIF_OUT1"] = 0L };

        AssertRuntimeTarget(resolver, signalState, signalTimes, "VCC_Plus");
        AssertRuntimeTarget(resolver, signalState, signalTimes, "ADC_IN");
        AssertRuntimeTarget(resolver, signalState, signalTimes, "DIG_OUT");
    }

    [TestMethod]
    public void Simtest_GoodProfile_ShouldPass_AndExposeFinalDeviceState()
    {
        using var python = PythonDeviceProcessFixture.StartProfile(@"simtest\device\devices\IKI_good.json");

        var observer = RunProgram(
            @"simtest\ct3xx\Simulator_Test.ctxprg",
            @"simtest\wireplan",
            @"simtest\wireplan",
            python.PipePath);

        CollectionAssert.AreEqual(
            new[] { "HV einschalten", "UIF OUT1 Relais einschalten", "OFF", "ON" },
            observer.Evaluations.Select(item => item.StepName).ToArray());

        CollectionAssert.AreEqual(
            new[] { TestOutcome.Pass, TestOutcome.Pass, TestOutcome.Pass, TestOutcome.Pass },
            observer.Evaluations.Select(item => item.Outcome).ToArray());

        var lastStep = observer.Evaluations[^1];
        Assert.AreEqual(3.3d, lastStep.MeasuredValue!.Value, 0.0001d);
        Assert.AreEqual(2, lastStep.Traces.Count, "Der ON-Schritt soll genau einen Eingangspfad und einen Ausgangspfad liefern.");
        StringAssert.StartsWith(lastStep.Traces[0].Title, "Ansteuerung:");
        StringAssert.Contains(lastStep.Traces[0].Title, "ADC_IN");
        StringAssert.StartsWith(lastStep.Traces[1].Title, "Messpfad:");
        StringAssert.Contains(lastStep.Traces[1].Title, "DIG_OUT");
        Assert.IsTrue(lastStep.Traces[0].Nodes.Any(node => node.Contains("RELAIS.COM1", StringComparison.OrdinalIgnoreCase)),
            $"Der ADC_IN-Ansteuerpfad muss den Relaiseingang COM1 enthalten. Tatsaechlicher Pfad: {string.Join(" -> ", lastStep.Traces[0].Nodes)}");
        Assert.IsTrue(lastStep.Traces[0].Nodes.Any(node => node.Contains("RELAIS.NO1", StringComparison.OrdinalIgnoreCase)),
            $"Der ADC_IN-Ansteuerpfad muss den Relaisausgang NO1 enthalten. Tatsaechlicher Pfad: {string.Join(" -> ", lastStep.Traces[0].Nodes)}");
        Assert.IsTrue(lastStep.Traces.All(trace => trace.Nodes.Count >= 2));
        Assert.IsTrue(lastStep.Traces.All(trace => IsTesterToDeviceTrace(trace.Nodes)),
            string.Join(Environment.NewLine, lastStep.Traces.Select(trace => $"{trace.Title}: {string.Join(" -> ", trace.Nodes)}")));

        var finalSnapshot = observer.Snapshots.Last();
        Assert.IsTrue(finalSnapshot.RelayStates.Any(item => item.Contains("RELAIS", StringComparison.OrdinalIgnoreCase)),
            string.Join(Environment.NewLine, finalSnapshot.RelayStates));
        Assert.AreEqual(4d, ParseNumeric(finalSnapshot.ExternalDeviceState.Inputs["ADC_IN"]), 0.0001d);
        Assert.AreEqual(3.3d, ParseNumeric(finalSnapshot.ExternalDeviceState.Outputs["DIG_OUT"]), 0.0001d);
    }

    [TestMethod]
    public void Simtest_BadProfile_ShouldFail_LastStep_WithRealMeasuredValue()
    {
        using var python = PythonDeviceProcessFixture.StartProfile(@"simtest\device\devices\IKI_bad.json");

        var observer = RunProgram(
            @"simtest\ct3xx\Simulator_Test.ctxprg",
            @"simtest\wireplan",
            @"simtest\wireplan",
            python.PipePath);

        CollectionAssert.AreEqual(
            new[] { TestOutcome.Pass, TestOutcome.Pass, TestOutcome.Pass, TestOutcome.Fail },
            observer.Evaluations.Select(item => item.Outcome).ToArray());

        var lastStep = observer.Evaluations[^1];
        Assert.AreEqual("ON", lastStep.StepName);
        Assert.AreEqual(1.5d, lastStep.MeasuredValue!.Value, 0.0001d);
        Assert.IsTrue(lastStep.Traces.Count > 0, "Der Testschritt sollte einen nachvollziehbaren Verbindungspfad liefern.");
    }

    [TestMethod]
    public void TransformerScenario_ShouldPass_EndToEnd()
    {
        using var python = PythonDeviceProcessFixture.StartProfile(@"simtest_transformer\device\devices\TrafoStromwandler_good.yaml");

        var observer = RunProgram(
            @"simtest_transformer\ct3xx\Transformer_Current_Example.ctxprg",
            @"simtest_transformer\wireplan",
            @"simtest_transformer\wireplan",
            python.PipePath);

        Assert.IsTrue(observer.Evaluations.Count >= 4, "Das Transformator-Beispiel sollte mehrere echte Messschritte liefern.");
        Assert.IsTrue(observer.Evaluations.All(item => item.Outcome == TestOutcome.Pass),
            string.Join(Environment.NewLine, observer.Evaluations.Select(item => $"{item.StepName}: {item.Outcome} {item.MeasuredValue}")));
    }

    private static SimulationObserverSpy RunProgram(string programRelativePath, string wireRootRelativePath, string simulationRootRelativePath, string pipePath)
    {
        var previousWire = Environment.GetEnvironmentVariable("CT3XX_WIREVIZ_ROOT");
        var previousSimulation = Environment.GetEnvironmentVariable("CT3XX_SIMULATION_MODEL_ROOT");
        var previousPipe = Environment.GetEnvironmentVariable("CT3XX_PY_DEVICE_PIPE");

        try
        {
            Environment.SetEnvironmentVariable("CT3XX_WIREVIZ_ROOT", TestData.GetPath(wireRootRelativePath));
            Environment.SetEnvironmentVariable("CT3XX_SIMULATION_MODEL_ROOT", TestData.GetPath(simulationRootRelativePath));
            Environment.SetEnvironmentVariable("CT3XX_PY_DEVICE_PIPE", pipePath);

            var parser = new Ct3xxProgramFileParser();
            var fileSet = parser.Load(TestData.GetPath(programRelativePath));
            var observer = new SimulationObserverSpy();
            var simulator = new Ct3xxProgramSimulator(observer: observer);
            simulator.Run(fileSet, 1);
            return observer;
        }
        finally
        {
            Environment.SetEnvironmentVariable("CT3XX_WIREVIZ_ROOT", previousWire);
            Environment.SetEnvironmentVariable("CT3XX_SIMULATION_MODEL_ROOT", previousSimulation);
            Environment.SetEnvironmentVariable("CT3XX_PY_DEVICE_PIPE", previousPipe);
        }
    }

    private static void AssertRuntimeTarget(
        WireVizHarnessResolver resolver,
        IReadOnlyDictionary<string, object?> signalState,
        IReadOnlyDictionary<string, long> signalTimes,
        string signalName)
    {
        Assert.IsTrue(
            resolver.TryResolveRuntimeTargets(signalName, signalState, signalTimes, 1000L, Simulation.FaultInjection.SimulationFaultSet.Empty, signalName != "DIG_OUT", out var targets),
            $"Kein Runtime-Target fuer '{signalName}' gefunden.");

        Assert.IsTrue(targets.Any(target => string.Equals(target.SignalName, signalName, StringComparison.OrdinalIgnoreCase)),
            $"Das logische Signal '{signalName}' wurde nicht bis zum DUT durchgereicht. Tatsächliche Targets: {string.Join(", ", targets.Select(item => item.SignalName))}");
    }

    private static double ParseNumeric(string value)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariant))
        {
            return invariant;
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var current))
        {
            return current;
        }

        throw new AssertFailedException($"Konnte numerischen Snapshot-Wert '{value}' nicht parsen.");
    }

    private static bool IsTesterToDeviceTrace(IReadOnlyList<string> nodes)
    {
        if (nodes.Count < 2)
        {
            return false;
        }

        return ClassifyLane(nodes[0]) >= ClassifyLane(nodes[^1]);
    }

    private static int ClassifyLane(string node)
    {
        if (string.IsNullOrWhiteSpace(node))
        {
            return 1;
        }

        var text = node.Trim();
        if (text.StartsWith("CT3.", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("Signal ", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (text.Contains("DevicePort.", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("OUTPUT", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("INPUT", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return 1;
    }
}

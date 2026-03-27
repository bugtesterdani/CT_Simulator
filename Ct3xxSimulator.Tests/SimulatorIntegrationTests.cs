// Provides Simulator Integration Tests for the simulator test project support code.
using Ct3xxProgramParser.Programs;
using Ct3xxProgramParser.Model;
using Ct3xxSimulator.Simulation;
using Ct3xxSimulator.Simulation.WireViz;
using Ct3xxSimulator.Validation;
using System.Globalization;

namespace Ct3xxSimulator.Tests;

[TestClass]
/// <summary>
/// Represents the simulator integration tests.
/// </summary>
public sealed class SimulatorIntegrationTests
{
    [TestMethod]
    /// <summary>
    /// Executes validation should accept simtest scenario.
    /// </summary>
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
    /// <summary>
    /// Executes runtime targets should preserve logical signal names for simtest.
    /// </summary>
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
    /// <summary>
    /// Executes simtest good profile should pass and expose final device state.
    /// </summary>
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
    /// <summary>
    /// Executes simtest bad profile should fail last step with real measured value.
    /// </summary>
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
    /// <summary>
    /// Executes transformer scenario should pass end to end.
    /// </summary>
    public void TransformerScenario_ShouldPass_EndToEnd()
    {
        using var python = PythonDeviceProcessFixture.StartProfile(@"simtest\device\devices\TrafoStromwandler_good.yaml");

        var observer = RunProgram(
            @"simtest_transformer\ct3xx\Transformer_Current_Example.ctxprg",
            @"simtest_transformer\wireplan",
            @"simtest_transformer\wireplan",
            python.PipePath);

        Assert.IsTrue(observer.Evaluations.Count >= 4, "Das Transformator-Beispiel sollte mehrere echte Messschritte liefern.");
        Assert.IsTrue(observer.Evaluations.All(item => item.Outcome == TestOutcome.Pass),
            string.Join(Environment.NewLine, observer.Evaluations.Select(item => $"{item.StepName}: {item.Outcome} {item.MeasuredValue}")));
    }

    [TestMethod]
    /// <summary>
    /// Executes validation should accept template sm2 scenario.
    /// </summary>
    public void Validation_ShouldAccept_TemplateSm2Scenario()
    {
        var issues = SimulationConfigurationValidator.Validate(
            TestData.GetPath(@"simtest_template_sm2\ct3xx\template_SM2.ctxprg"),
            TestData.GetPath(@"simtest_template_sm2\wireplan"),
            TestData.GetPath(@"simtest_template_sm2\wireplan"),
            TestData.GetPath(@"simtest\device\main.py"));

        Assert.AreEqual(0, issues.Count, string.Join(Environment.NewLine, issues));
    }

    [TestMethod]
    /// <summary>
    /// Executes template sm2 scenario should pass end to end.
    /// </summary>
    public void TemplateSm2Scenario_ShouldPass_EndToEnd()
    {
        using var python = PythonDeviceProcessFixture.StartModule(
            @"simtest\device\main.py",
            "template_sm2_led_analyzer");

        var observer = RunProgram(
            @"simtest_template_sm2\ct3xx\template_SM2.ctxprg",
            @"simtest_template_sm2\wireplan",
            @"simtest_template_sm2\wireplan",
            python.PipePath);

        Assert.IsTrue(observer.Evaluations.Count >= 12, "template_SM2 sollte sichtbare IOXX/ECLL/PWT$/E488/PET$-Schritte liefern.");
        var finalSnapshotText = observer.Snapshots.Count == 0
            ? "Keine Snapshots"
            : $"Inputs={string.Join(", ", observer.Snapshots[^1].ExternalDeviceState.Inputs.Select(item => $"{item.Key}={item.Value}"))}; Interfaces={string.Join(", ", observer.Snapshots[^1].ExternalDeviceState.Interfaces.Select(item => $"{item.Key}={item.Value}"))}";
        Assert.IsTrue(observer.Evaluations.All(item => item.Outcome == TestOutcome.Pass),
            string.Join(Environment.NewLine, observer.Evaluations.Select(item => $"{item.StepName}: {item.Outcome} {item.Details}")) +
            Environment.NewLine +
            finalSnapshotText +
            Environment.NewLine +
            string.Join(Environment.NewLine, observer.Messages.Where(message => message.Contains("Python device", StringComparison.OrdinalIgnoreCase) || message.Contains("E488", StringComparison.OrdinalIgnoreCase))));

        Assert.IsTrue(observer.Messages.Any(message => message.Contains("E488 Schnittstelle SEND Interface LED Analyzer", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(observer.Messages.Any(message => message.Contains("E488 Schnittstelle RECV Interface LED Analyzer: 15,1,25,40", StringComparison.OrdinalIgnoreCase)));

        var petEvaluations = observer.Evaluations
            .Where(item => item.StepName is "Helligkeit LED" or "Rot Anteil LED" or "GrÃ¼n Anteil LED" or "Blau Anteil LED")
            .ToList();

        Assert.AreEqual(8, petEvaluations.Count, "template_SM2 sollte zwei PET$-Bloecke mit je vier Auswertungen liefern.");
        Assert.IsTrue(observer.Snapshots.Any(snapshot =>
                string.Equals(snapshot.ActiveConcurrentGroup, "Parallel checking", StringComparison.OrdinalIgnoreCase) &&
                snapshot.ConcurrentBranches.Count > 0),
            "Concurrent-Snapshots fuer template_SM2 wurden nicht erzeugt.");
        Assert.IsTrue(observer.Snapshots.Any(snapshot =>
                snapshot.ConcurrentBranches.Any(branch =>
                    branch.BranchName.Contains("Programming Device", StringComparison.OrdinalIgnoreCase))),
            "Der Concurrent-Branch fuer ECLL fehlt in den Snapshot-Daten.");
        Assert.IsTrue(observer.Snapshots.Any(snapshot =>
                string.Equals(snapshot.ConcurrentEvent, "group_sync:start", StringComparison.OrdinalIgnoreCase)),
            "Der globale Snapshot-Punkt group_sync:start fehlt.");
        Assert.IsTrue(observer.Snapshots.Any(snapshot =>
                snapshot.ConcurrentEvent?.StartsWith("branch_waiting:", StringComparison.OrdinalIgnoreCase) == true),
            "Der globale Snapshot-Punkt fuer branch_waiting fehlt.");
        Assert.IsTrue(observer.Snapshots.Any(snapshot =>
                snapshot.ConcurrentEvent?.StartsWith("interface_request:", StringComparison.OrdinalIgnoreCase) == true),
            "Der globale Snapshot-Punkt fuer interface_request fehlt.");
        Assert.IsTrue(observer.Snapshots.Any(snapshot =>
                snapshot.ConcurrentEvent?.StartsWith("interface_response:", StringComparison.OrdinalIgnoreCase) == true),
            "Der globale Snapshot-Punkt fuer interface_response fehlt.");
        Assert.IsTrue(observer.Snapshots.Any(snapshot =>
                snapshot.ConcurrentEvent?.StartsWith("process_exit:", StringComparison.OrdinalIgnoreCase) == true),
            "Der globale Snapshot-Punkt fuer process_exit fehlt.");
    }

    [TestMethod]
    /// <summary>
    /// Executes template splitted am2 parser should read split child sequence.
    /// </summary>
    public void TemplateSplittedAm2Parser_ShouldRead_SplitChildSequence()
    {
        var parser = new Ct3xxProgramFileParser();
        var fileSet = parser.Load(TestData.GetPath(@"testprogramme\template_splitted_am2\template_splitted_am2.ctxprg"));

        var dutLoopItems = fileSet.Program.DutLoop?.Items;
        Assert.IsNotNull(dutLoopItems);
        Assert.AreEqual(1, dutLoopItems.Count);
        Assert.IsInstanceOfType<Test>(dutLoopItems[0]);

        var splitTest = (Test)dutLoopItems[0];
        Assert.AreEqual("2ARB", splitTest.Id);
        Assert.AreEqual(4, splitTest.Items.Count, "Der Split-Test sollte zwei Auswertungsgruppen und zwei IOXX-Schritte enthalten.");
        Assert.IsInstanceOfType<Group>(splitTest.Items[0]);
        Assert.IsInstanceOfType<Test>(splitTest.Items[1]);
        Assert.IsInstanceOfType<Group>(splitTest.Items[2]);
        Assert.IsInstanceOfType<Test>(splitTest.Items[3]);
    }

    [TestMethod]
    /// <summary>
    /// Executes template splitted am2 scenario should pass end to end.
    /// </summary>
    public void TemplateSplittedAm2Scenario_ShouldPass_EndToEnd()
    {
        using var python = PythonDeviceProcessFixture.StartModule(
            @"simtest\device\main.py",
            "template_splitted_am2_led_analyzer");

        var issues = SimulationConfigurationValidator.Validate(
            TestData.GetPath(@"testprogramme\template_splitted_am2\template_splitted_am2.ctxprg"),
            TestData.GetPath(@"simtest_template_splitted_am2\wireplan"),
            TestData.GetPath(@"simtest_template_splitted_am2\wireplan"),
            TestData.GetPath(@"simtest\device\main.py"));

        Assert.AreEqual(0, issues.Count, string.Join(Environment.NewLine, issues));

        var observer = RunProgram(
            @"testprogramme\template_splitted_am2\template_splitted_am2.ctxprg",
            @"simtest_template_splitted_am2\wireplan",
            @"simtest_template_splitted_am2\wireplan",
            python.PipePath);

        Assert.IsTrue(observer.Evaluations.Count >= 13,
            string.Join(Environment.NewLine, observer.Evaluations.Select(item => $"{item.StepName}: {item.Outcome} {item.Details}")));

        Assert.IsTrue(observer.Evaluations.Any(item => item.StepName == "Spannungsmessung"),
            "Der 2ARB-Hauptschritt fehlt.");
        Assert.IsTrue(observer.Evaluations.Count(item => item.StepName == "LED Abfrage") == 2,
            "Es sollten zwei E488-Schritte aus den Split-Untergruppen sichtbar sein.");
        Assert.IsTrue(observer.Evaluations.Count(item => item.StepName == "Relais schalten") == 2,
            "Es sollten zwei IOXX-Schritte aus dem Split-Unterablauf sichtbar sein.");

        var petEvaluations = observer.Evaluations
            .Where(item => item.StepName is "Helligkeit LED" or "Rot Anteil LED" or "Gruen Anteil LED" or "GrÃ¼n Anteil LED" or "Blau Anteil LED" or "GrÃƒÂ¼n Anteil LED")
            .ToList();
        Assert.AreEqual(8, petEvaluations.Count, "Es sollten zwei PET$-Bloecke mit je vier Auswertungen sichtbar sein.");
        Assert.IsTrue(petEvaluations.All(item => item.Outcome == TestOutcome.Pass),
            string.Join(Environment.NewLine, petEvaluations.Select(item => $"{item.StepName}: {item.Outcome} {item.MeasuredValue}")));

        Assert.IsTrue(observer.Messages.Any(message => message.Contains("E488 Schnittstelle RECV Interface LED Analyzer: 0,0,0,0", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(observer.Messages.Any(message => message.Contains("E488 Schnittstelle RECV Interface LED Analyzer: 15,1,25,40", StringComparison.OrdinalIgnoreCase)));

        var waveformEvaluation = observer.Evaluations.First(item => item.StepName == "Spannungsmessung");
        Assert.IsTrue(waveformEvaluation.Traces.Any(trace => trace.Title.Contains("Waveform Stimulus", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(waveformEvaluation.Traces.Any(trace => trace.Title.Contains("Waveform Response", StringComparison.OrdinalIgnoreCase)));

        var finalSnapshot = observer.Snapshots.Last();
        Assert.AreEqual(0d, ParseNumeric(finalSnapshot.ExternalDeviceState.Inputs["DUT_HV"]), 0.0001d);
        Assert.IsTrue(finalSnapshot.ExternalDeviceState.Inputs.ContainsKey("WAVE_IN_1"));
        Assert.IsTrue(finalSnapshot.ExternalDeviceState.Inputs.ContainsKey("WAVE_IN_2"));
        Assert.IsTrue(finalSnapshot.ExternalDeviceState.Inputs.ContainsKey("WAVE_IN_3"));
        Assert.IsTrue(finalSnapshot.ExternalDeviceState.Outputs.ContainsKey("WAVE_OUT_1"));
        Assert.IsTrue(finalSnapshot.ExternalDeviceState.Outputs.ContainsKey("WAVE_OUT_2"));
        Assert.IsTrue(finalSnapshot.ExternalDeviceState.Outputs.ContainsKey("WAVE_OUT_3"));
    }

    [TestMethod]
    /// <summary>
    /// Executes concurrent group should advance on shared simulation clock.
    /// </summary>
    public void ConcurrentGroup_ShouldAdvance_OnSharedSimulationClock()
    {
        var program = new Ct3xxProgram
        {
            RootItems =
            {
                new Group
                {
                    Id = "PGR$",
                    Name = "Concurrent Timing",
                    ExecMode = "concurrent",
                    ExecCondition = "TRUE",
                    Items =
                    {
                        new Test
                        {
                            Id = "PWT$",
                            Parameters = new TestParameters
                            {
                                Name = "Wait 2s",
                                AdditionalAttributes = CreateAttributes(("WaitTime", "2s"))
                            }
                        },
                        new Test
                        {
                            Id = "PWT$",
                            Parameters = new TestParameters
                            {
                                Name = "Wait 3s",
                                AdditionalAttributes = CreateAttributes(("WaitTime", "3s"))
                            }
                        }
                    }
                }
            }
        };

        var observer = new SimulationObserverSpy();
        var simulator = new Ct3xxProgramSimulator(observer: observer);
        simulator.Run(program, 1);

        Assert.IsTrue(observer.Snapshots.Count > 0, "Es wurden keine Snapshots erzeugt.");
        Assert.AreEqual(3000L, observer.Snapshots[^1].CurrentTimeMs,
            $"Concurrent-Waits sollten auf einer gemeinsamen Simulationsuhr laufen. Letzte Zeit: {observer.Snapshots[^1].CurrentTimeMs} ms");
        Assert.IsTrue(observer.Snapshots.Any(snapshot =>
                snapshot.ConcurrentEvent?.StartsWith("branch_waiting:", StringComparison.OrdinalIgnoreCase) == true));
        Assert.IsTrue(observer.Snapshots.Any(snapshot =>
                snapshot.ConcurrentEvent?.StartsWith("branch_resumed:", StringComparison.OrdinalIgnoreCase) == true));
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
            $"Das logische Signal '{signalName}' wurde nicht bis zum DUT durchgereicht. TatsÃ¤chliche Targets: {string.Join(", ", targets.Select(item => item.SignalName))}");
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

    private static System.Xml.XmlAttribute[] CreateAttributes(params (string Name, string Value)[] attributes)
    {
        var document = new System.Xml.XmlDocument();
        return attributes
            .Select(attribute =>
            {
                var xmlAttribute = document.CreateAttribute(attribute.Name);
                xmlAttribute.Value = attribute.Value;
                return xmlAttribute;
            })
            .ToArray();
    }
}

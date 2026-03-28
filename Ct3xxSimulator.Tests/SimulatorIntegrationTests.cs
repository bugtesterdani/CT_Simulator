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

    [TestMethod]
    /// <summary>
    /// Executes uif i2c scenario should pass end to end.
    /// </summary>
    public void UifI2cScenario_ShouldPass_EndToEnd()
    {
        using var python = PythonDeviceProcessFixture.StartProfile(@"simtest\device\devices\i2c_lm75_good.yaml");

        var issues = SimulationConfigurationValidator.Validate(
            TestData.GetPath(@"testprogramme\UIF I2C Test\UIF I2C Test.ctxprg"),
            TestData.GetPath(@"simtest_uif_i2c\wireplan"),
            TestData.GetPath(@"simtest_uif_i2c\wireplan"),
            TestData.GetPath(@"simtest\device\devices\i2c_lm75_good.yaml"));

        Assert.AreEqual(0, issues.Count, string.Join(Environment.NewLine, issues));

        var observer = RunProgram(
            @"testprogramme\UIF I2C Test\UIF I2C Test.ctxprg",
            @"simtest_uif_i2c\wireplan",
            @"simtest_uif_i2c\wireplan",
            python.PipePath);

        var i2cSteps = observer.Evaluations
            .Where(item => item.StepName.StartsWith("UIF I2C", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.AreEqual(3, i2cSteps.Count, string.Join(Environment.NewLine, observer.Evaluations.Select(item => $"{item.StepName}: {item.Outcome} {item.Details}")));
        Assert.IsTrue(i2cSteps.All(item => item.Outcome == TestOutcome.Pass));
        Assert.IsTrue(i2cSteps.All(item => item.Details?.Contains("0x93", StringComparison.OrdinalIgnoreCase) == true));
    }

    [TestMethod]
    /// <summary>
    /// Executes uif i2c scenario should fail when the slave returns a mismatching readback byte.
    /// </summary>
    public void UifI2cScenario_ShouldFail_WhenSlaveReadbackDoesNotMatch()
    {
        using var python = PythonDeviceProcessFixture.StartProfile(@"simtest\device\devices\i2c_lm75_fail.yaml");

        var observer = RunProgram(
            @"testprogramme\UIF I2C Test\UIF I2C Test.ctxprg",
            @"simtest_uif_i2c\wireplan",
            @"simtest_uif_i2c\wireplan",
            python.PipePath);

        Assert.IsTrue(observer.Evaluations.Any(item => item.Outcome == TestOutcome.Fail && item.StepName.Contains("I2C", StringComparison.OrdinalIgnoreCase)),
            string.Join(Environment.NewLine, observer.Evaluations.Select(item => $"{item.StepName}: {item.Outcome} {item.Details}")));
    }

    [TestMethod]
    /// <summary>
    /// Executes uif i2c scenario should error when no i2c slave acknowledges the bus.
    /// </summary>
    public void UifI2cScenario_ShouldError_WhenNoSlaveAcknowledges()
    {
        using var python = PythonDeviceProcessFixture.StartProfile(@"simtest\device\devices\i2c_lm75_error.yaml");

        var observer = RunProgram(
            @"testprogramme\UIF I2C Test\UIF I2C Test.ctxprg",
            @"simtest_uif_i2c\wireplan",
            @"simtest_uif_i2c\wireplan",
            python.PipePath);

        Assert.IsTrue(observer.Evaluations.Any(item => item.Outcome == TestOutcome.Error && item.StepName.Contains("I2C", StringComparison.OrdinalIgnoreCase)),
            string.Join(Environment.NewLine, observer.Evaluations.Select(item => $"{item.StepName}: {item.Outcome} {item.Details}")));
    }

    [TestMethod]
    /// <summary>
    /// Executes ea3 i2c scenario should pass end to end.
    /// </summary>
    public void Ea3I2cScenario_ShouldPass_EndToEnd()
    {
        using var python = PythonDeviceProcessFixture.StartProfile(@"simtest\device\devices\i2c_lm75_good.yaml");

        var issues = SimulationConfigurationValidator.Validate(
            TestData.GetPath(@"testprogramme\EA3 I2C Test\EA3 I2C Test.ctxprg"),
            TestData.GetPath(@"simtest_ea3_i2c\wireplan"),
            TestData.GetPath(@"simtest_ea3_i2c\wireplan"),
            TestData.GetPath(@"simtest\device\devices\i2c_lm75_good.yaml"));

        Assert.AreEqual(0, issues.Count, string.Join(Environment.NewLine, issues));

        var observer = RunProgram(
            @"testprogramme\EA3 I2C Test\EA3 I2C Test.ctxprg",
            @"simtest_ea3_i2c\wireplan",
            @"simtest_ea3_i2c\wireplan",
            python.PipePath);

        var i2cSteps = observer.Evaluations
            .Where(item => item.StepName.Contains("EA3 I2C", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.AreEqual(3, i2cSteps.Count);
        Assert.IsTrue(i2cSteps.All(item => item.Outcome == TestOutcome.Pass),
            string.Join(Environment.NewLine, observer.Evaluations.Select(item => $"{item.StepName}: {item.Outcome} {item.Details}")));
        Assert.IsTrue(observer.Evaluations.Any(item => item.StepName == "EA3 Init" && item.Outcome == TestOutcome.Pass));
    }

    [TestMethod]
    /// <summary>
    /// Executes ea3-r i2c scenario should pass end to end.
    /// </summary>
    public void Ea3rI2cScenario_ShouldPass_EndToEnd()
    {
        using var python = PythonDeviceProcessFixture.StartProfile(@"simtest\device\devices\i2c_lm75_good.yaml");

        var issues = SimulationConfigurationValidator.Validate(
            TestData.GetPath(@"testprogramme\EA3-R I2C Test\EA3-R I2C Test.ctxprg"),
            TestData.GetPath(@"simtest_ea3r_i2c\wireplan"),
            TestData.GetPath(@"simtest_ea3r_i2c\wireplan"),
            TestData.GetPath(@"simtest\device\devices\i2c_lm75_good.yaml"));

        Assert.AreEqual(0, issues.Count, string.Join(Environment.NewLine, issues));

        var observer = RunProgram(
            @"testprogramme\EA3-R I2C Test\EA3-R I2C Test.ctxprg",
            @"simtest_ea3r_i2c\wireplan",
            @"simtest_ea3r_i2c\wireplan",
            python.PipePath);

        var i2cSteps = observer.Evaluations
            .Where(item => item.StepName.Contains("EA3-R I2C", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.AreEqual(3, i2cSteps.Count);
        Assert.IsTrue(i2cSteps.All(item => item.Outcome == TestOutcome.Pass),
            string.Join(Environment.NewLine, observer.Evaluations.Select(item => $"{item.StepName}: {item.Outcome} {item.Details}")));
        Assert.IsTrue(observer.Evaluations.Any(item => item.StepName == "EA3-R Init" && item.Outcome == TestOutcome.Pass));
    }

    [TestMethod]
    /// <summary>
    /// Executes spi cat25128 scenario should pass end to end.
    /// </summary>
    public void SpiCat25128Scenario_ShouldPass_EndToEnd()
    {
        using var python = PythonDeviceProcessFixture.StartProfile(@"simtest\device\devices\spi_cat25128_good.yaml");

        var issues = SimulationConfigurationValidator.Validate(
            TestData.GetPath(@"testprogramme\SPI CAT25128 EEPROM\SPI CAT25128 EEPROM.ctxprg"),
            TestData.GetPath(@"simtest_spi_cat25128\wireplan"),
            TestData.GetPath(@"simtest_spi_cat25128\wireplan"),
            TestData.GetPath(@"simtest\device\devices\spi_cat25128_good.yaml"));

        Assert.AreEqual(0, issues.Count, string.Join(Environment.NewLine, issues));

        var observer = RunProgram(
            @"testprogramme\SPI CAT25128 EEPROM\SPI CAT25128 EEPROM.ctxprg",
            @"simtest_spi_cat25128\wireplan",
            @"simtest_spi_cat25128\wireplan",
            python.PipePath);

        Assert.AreEqual(1, observer.Evaluations.Count, string.Join(Environment.NewLine, observer.Evaluations.Select(item => $"{item.StepName}: {item.Outcome} {item.Details}")));
        var evaluation = observer.Evaluations[0];
        Assert.AreEqual(TestOutcome.Pass, evaluation.Outcome);
        Assert.AreEqual("SPI IO ON Semiconductor CAT25128 EEPROM", evaluation.StepName);
        StringAssert.Contains(evaluation.Details ?? string.Empty, "RX=FF02");
        StringAssert.Contains(evaluation.Details ?? string.Empty, "RX=FFFFFFAB");
        Assert.IsTrue(evaluation.Traces.Any(trace => trace.Title.Contains("SPI SPI_interface1", StringComparison.OrdinalIgnoreCase)));
        CollectionAssert.IsSubsetOf(
            new[] { "SPI SPI_interface1 CS", "SPI SPI_interface1 CLK", "SPI SPI_interface1 MOSI", "SPI SPI_interface1 MISO" },
            evaluation.CurvePoints.Select(point => point.Label).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    [TestMethod]
    /// <summary>
    /// Executes spi cat25128 python module should pass end to end.
    /// </summary>
    public void SpiCat25128PythonModule_ShouldPass_EndToEnd()
    {
        using var python = PythonDeviceProcessFixture.StartModule(
            @"simtest\device\main.py",
            "spi_cat25128_device");

        var observer = RunProgram(
            @"testprogramme\SPI CAT25128 EEPROM\SPI CAT25128 EEPROM.ctxprg",
            @"simtest_spi_cat25128\wireplan",
            @"simtest_spi_cat25128\wireplan",
            python.PipePath);

        Assert.AreEqual(1, observer.Evaluations.Count);
        Assert.AreEqual(TestOutcome.Pass, observer.Evaluations[0].Outcome);
    }

    [TestMethod]
    /// <summary>
    /// Executes spi cat25128 scenario should fail when readback does not match the expected status byte.
    /// </summary>
    public void SpiCat25128Scenario_ShouldFail_WhenReadbackDoesNotMatch()
    {
        using var python = PythonDeviceProcessFixture.StartProfile(@"simtest\device\devices\spi_cat25128_fail.yaml");

        var observer = RunProgram(
            @"testprogramme\SPI CAT25128 EEPROM\SPI CAT25128 EEPROM.ctxprg",
            @"simtest_spi_cat25128\wireplan",
            @"simtest_spi_cat25128\wireplan",
            python.PipePath);

        Assert.IsTrue(observer.Evaluations.Any(item => item.Outcome == TestOutcome.Fail),
            string.Join(Environment.NewLine, observer.Evaluations.Select(item => $"{item.StepName}: {item.Outcome} {item.Details}")));
    }

    [TestMethod]
    /// <summary>
    /// Executes spi cat25128 scenario should error when the configured timing does not match.
    /// </summary>
    public void SpiCat25128Scenario_ShouldError_WhenTimingDoesNotMatch()
    {
        using var python = PythonDeviceProcessFixture.StartProfile(@"simtest\device\devices\spi_cat25128_error.yaml");

        var observer = RunProgram(
            @"testprogramme\SPI CAT25128 EEPROM\SPI CAT25128 EEPROM.ctxprg",
            @"simtest_spi_cat25128\wireplan",
            @"simtest_spi_cat25128\wireplan",
            python.PipePath);

        Assert.IsTrue(observer.Evaluations.Any(item => item.Outcome == TestOutcome.Error),
            string.Join(Environment.NewLine, observer.Evaluations.Select(item => $"{item.StepName}: {item.Outcome} {item.Details}")));
    }

    [TestMethod]
    /// <summary>
    /// Executes dm30 spi eeprom scenario should pass end to end.
    /// </summary>
    public void Spi93c46bScenario_ShouldPass_EndToEnd()
    {
        using var python = PythonDeviceProcessFixture.StartProfile(@"simtest\device\devices\spi_93c46b_good.yaml");

        var issues = SimulationConfigurationValidator.Validate(
            TestData.GetPath(@"testprogramme\SPI-EEPROM\SPI-EEPROM-93C46B\SPI-EEPROM-93C46B.ctxprg"),
            TestData.GetPath(@"simtest_spi_93c46b\wireplan"),
            TestData.GetPath(@"simtest_spi_93c46b\wireplan"),
            TestData.GetPath(@"simtest\device\devices\spi_93c46b_good.yaml"));

        Assert.AreEqual(0, issues.Count, string.Join(Environment.NewLine, issues));

        var observer = RunProgram(
            @"testprogramme\SPI-EEPROM\SPI-EEPROM-93C46B\SPI-EEPROM-93C46B.ctxprg",
            @"simtest_spi_93c46b\wireplan",
            @"simtest_spi_93c46b\wireplan",
            python.PipePath);

        Assert.IsTrue(observer.Evaluations.Any(item => item.StepName == "DM300 Write EEPROM" && item.Outcome == TestOutcome.Pass),
            string.Join(Environment.NewLine, observer.Evaluations.Select(item => $"{item.StepName}: {item.Outcome} {item.Details}")));
        var readStep = observer.Evaluations.FirstOrDefault(item => item.StepName == "DM300 Read Complete EEPROM");
        Assert.IsNotNull(readStep, "DM300 Read Complete EEPROM fehlt.");
        Assert.AreEqual(TestOutcome.Pass, readStep.Outcome);
        StringAssert.Contains(readStep.Details ?? string.Empty, "57472D5445535421");
        CollectionAssert.IsSubsetOf(
            new[] { "SPI DM30 CS", "SPI DM30 CLK", "SPI DM30 MISO" },
            readStep.CurvePoints.Select(point => point.Label).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    [TestMethod]
    /// <summary>
    /// Executes validation should accept smud boundary scan scenario.
    /// </summary>
    public void Validation_ShouldAccept_SmudBoundaryScanScenario()
    {
        var issues = SimulationConfigurationValidator.Validate(
            TestData.GetPath(@"testprogramme\CT3xx Testadapter2 - Boundary Scan Test 1\CT3xx Testadapter2 - Boundary Scan Test 1.ctxprg"),
            TestData.GetPath(@"simtest_smud_boundary_scan\wireplan"),
            TestData.GetPath(@"simtest_smud_boundary_scan\wireplan"),
            TestData.GetPath(@"simtest\device\devices\smud_boundary_scan_good.yaml"));

        Assert.AreEqual(0, issues.Count, string.Join(Environment.NewLine, issues));
    }

    [TestMethod]
    /// <summary>
    /// Executes validation should require a device model.
    /// </summary>
    public void Validation_ShouldRequire_DeviceModel()
    {
        var issues = SimulationConfigurationValidator.Validate(
            TestData.GetPath(@"simtest_ctct_contact\ct3xx\CTCT_Contact_Test.ctxprg"),
            TestData.GetPath(@"simtest_ctct_contact\wireplan"),
            TestData.GetPath(@"simtest_ctct_contact\wireplan"),
            string.Empty);

        CollectionAssert.Contains(issues.ToArray(), "Das Geraetemodell wurde nicht gefunden.");
    }

    [TestMethod]
    /// <summary>
    /// Executes smud boundary scan yaml profile should pass for all three boundary scan reference programs.
    /// </summary>
    public void SmudBoundaryScanYaml_ShouldPass_ForAllReferencePrograms()
    {
        using (var python = PythonDeviceProcessFixture.StartProfile(@"simtest\device\devices\smud_boundary_scan_good.yaml"))
        {
            AssertSmudPass(
                RunProgram(
                    @"testprogramme\CT3xx Testadapter2 - Boundary Scan Test 1\CT3xx Testadapter2 - Boundary Scan Test 1.ctxprg",
                    @"simtest_smud_boundary_scan\wireplan",
                    @"simtest_smud_boundary_scan\wireplan",
                    python.PipePath),
                "SM2 DUT Power Supply Test",
                "SM2 DUT Power Supply Test");
        }

        using (var python = PythonDeviceProcessFixture.StartProfile(@"simtest\device\devices\smud_boundary_scan_good.yaml"))
        {
            AssertSmudPass(
                RunProgram(
                    @"testprogramme\CT3xx Testadapter2 - Boundary Scan Test 2\CT3xx Testadapter2 - Boundary Scan Test 2.ctxprg",
                    @"simtest_smud_boundary_scan\wireplan",
                    @"simtest_smud_boundary_scan\wireplan",
                    python.PipePath),
                "SM2 DUT Power Supply Test",
                "SM2 DUT Power Supply Test");
        }

        using (var python = PythonDeviceProcessFixture.StartProfile(@"simtest\device\devices\smud_boundary_scan_good.yaml"))
        {
            AssertSmudPass(
                RunProgram(
                    @"testprogramme\CT3xx Testadapter2 - Boundary Scan Test 3\CT3xx Testadapter2 - Boundary Scan Test 3.ctxprg",
                    @"simtest_smud_boundary_scan\wireplan",
                    @"simtest_smud_boundary_scan\wireplan",
                    python.PipePath),
                "Power on");
        }
    }

    [TestMethod]
    /// <summary>
    /// Executes smud boundary scan yaml profile should fail when the measured current is below the configured minimum.
    /// </summary>
    public void SmudBoundaryScanYaml_ShouldFail_WhenCurrentIsTooLow()
    {
        using var python = PythonDeviceProcessFixture.StartProfile(@"simtest\device\devices\smud_boundary_scan_fail.yaml");

        var observer = RunProgram(
            @"testprogramme\CT3xx Testadapter2 - Boundary Scan Test 1\CT3xx Testadapter2 - Boundary Scan Test 1.ctxprg",
            @"simtest_smud_boundary_scan\wireplan",
            @"simtest_smud_boundary_scan\wireplan",
            python.PipePath);

        Assert.IsTrue(observer.Evaluations.Any(item =>
                item.StepName == "SM2 DUT Power Supply Test" &&
                item.Outcome == TestOutcome.Fail),
            string.Join(Environment.NewLine, observer.Evaluations.Select(item => $"{item.StepName}: {item.Outcome} {item.Details}")));
    }

    [TestMethod]
    /// <summary>
    /// Executes smud boundary scan yaml profile should error when the fuse current is exceeded.
    /// </summary>
    public void SmudBoundaryScanYaml_ShouldError_WhenFuseTrips()
    {
        using var python = PythonDeviceProcessFixture.StartProfile(@"simtest\device\devices\smud_boundary_scan_error.yaml");

        var observer = RunProgram(
            @"testprogramme\CT3xx Testadapter2 - Boundary Scan Test 1\CT3xx Testadapter2 - Boundary Scan Test 1.ctxprg",
            @"simtest_smud_boundary_scan\wireplan",
            @"simtest_smud_boundary_scan\wireplan",
            python.PipePath);

        Assert.IsTrue(observer.Evaluations.Any(item =>
                item.StepName == "SM2 DUT Power Supply Test" &&
                item.Outcome == TestOutcome.Error &&
                (item.Details?.Contains("Sicherung", StringComparison.OrdinalIgnoreCase) ?? false)),
            string.Join(Environment.NewLine, observer.Evaluations.Select(item => $"{item.StepName}: {item.Outcome} {item.Details}")));
    }

    [TestMethod]
    /// <summary>
    /// Executes smud boundary scan python module should pass end to end.
    /// </summary>
    public void SmudBoundaryScanPythonModule_ShouldPass_EndToEnd()
    {
        using var python = PythonDeviceProcessFixture.StartModule(
            @"simtest\device\main.py",
            "smud_boundary_scan_adapter");

        var observer = RunProgram(
            @"testprogramme\CT3xx Testadapter2 - Boundary Scan Test 1\CT3xx Testadapter2 - Boundary Scan Test 1.ctxprg",
            @"simtest_smud_boundary_scan\wireplan",
            @"simtest_smud_boundary_scan\wireplan",
            python.PipePath);

        AssertSmudPass(observer, "SM2 DUT Power Supply Test", "SM2 DUT Power Supply Test");
    }

    [TestMethod]
    /// <summary>
    /// Executes ctct scenario should pass with active relay path and resistance evaluation.
    /// </summary>
    public void CtctScenario_ShouldPass_WithActiveRelayPath_AndResistanceEvaluation()
    {
        var parser = new Ct3xxProgramFileParser();
        var fileSet = parser.Load(TestData.GetPath(@"simtest_ctct_contact\ct3xx\CTCT_Contact_Test.ctxprg"));
        var previousWire = Environment.GetEnvironmentVariable("CT3XX_WIREVIZ_ROOT");
        var previousSimulation = Environment.GetEnvironmentVariable("CT3XX_SIMULATION_MODEL_ROOT");
        try
        {
            Environment.SetEnvironmentVariable("CT3XX_WIREVIZ_ROOT", TestData.GetPath(@"simtest_ctct_contact_open\wireplan"));
            Environment.SetEnvironmentVariable("CT3XX_SIMULATION_MODEL_ROOT", TestData.GetPath(@"simtest_ctct_contact_open\wireplan"));
            var resolver = WireVizHarnessResolver.Create(fileSet);
            var directMeasurement = resolver.MeasureResistance(
                "TP1",
                "TP2",
                new Dictionary<string, object?> { ["RELAY_CTRL"] = 24d },
                new Dictionary<string, long> { ["RELAY_CTRL"] = 0L },
                1000L,
                Simulation.FaultInjection.SimulationFaultSet.Empty);
            Assert.IsTrue(directMeasurement.PathFound,
                directMeasurement.FailureReason ?? "Kein aktiver CTCT-Pfad gefunden.");
            Assert.AreEqual(100d, directMeasurement.ResistanceOhms!.Value, 0.0001d);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CT3XX_WIREVIZ_ROOT", previousWire);
            Environment.SetEnvironmentVariable("CT3XX_SIMULATION_MODEL_ROOT", previousSimulation);
        }

        var observer = RunProgram(
            @"simtest_ctct_contact\ct3xx\CTCT_Contact_Test.ctxprg",
            @"simtest_ctct_contact\wireplan",
            @"simtest_ctct_contact\wireplan",
            string.Empty);

        var ctctEvaluations = observer.Evaluations
            .Where(item => item.StepName is "TP1" or "TP2" or "TP3")
            .ToList();
        Assert.AreEqual(3, ctctEvaluations.Count,
            string.Join(Environment.NewLine, observer.Evaluations.Select(item => $"{item.StepName}: {item.Outcome} {item.Details}")));
        Assert.IsTrue(ctctEvaluations.All(item => item.Outcome == TestOutcome.Pass),
            string.Join(Environment.NewLine, ctctEvaluations.Select(item => $"{item.StepName}: {item.Outcome} {item.Details}")));

        var tp1 = ctctEvaluations.Single(item => item.StepName == "TP1");
        Assert.AreEqual(100d, tp1.MeasuredValue!.Value, 0.0001d);
        Assert.IsTrue(tp1.Traces.Any(trace => trace.Title.Contains("TP1 -> TP2", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(tp1.Traces.SelectMany(trace => trace.Nodes).Any(node => node.Contains("REL1.COM1", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(tp1.Traces.SelectMany(trace => trace.Nodes).Any(node => node.Contains("REL1.NO1", StringComparison.OrdinalIgnoreCase)));

        var tp3 = ctctEvaluations.Single(item => item.StepName == "TP3");
        Assert.AreEqual(TestOutcome.Pass, tp3.Outcome);
        Assert.IsNull(tp3.MeasuredValue);
        StringAssert.Contains(tp3.Details!, "open");
    }

    [TestMethod]
    /// <summary>
    /// Executes ctct scenario should fail when the expected closed relay path is open.
    /// </summary>
    public void CtctScenario_ShouldFail_WhenExpectedClosedPath_IsOpen()
    {
        var observer = RunProgram(
            @"simtest_ctct_contact\ct3xx\CTCT_Contact_Test_Fail.ctxprg",
            @"simtest_ctct_contact_open\wireplan",
            @"simtest_ctct_contact_open\wireplan",
            string.Empty);

        Assert.IsTrue(observer.Evaluations.Any(item =>
                item.StepName == "TP1" &&
                item.Outcome == TestOutcome.Fail &&
                item.Details!.Contains("offene Leitung", StringComparison.OrdinalIgnoreCase)),
            string.Join(Environment.NewLine, observer.Evaluations.Select(item => $"{item.StepName}: {item.Outcome} {item.Details}")));
    }

    [TestMethod]
    /// <summary>
    /// Executes ctct scenario should error when a configured test point cannot be resolved.
    /// </summary>
    public void CtctScenario_ShouldError_WhenTestPoint_IsMissingInWiring()
    {
        var observer = RunProgram(
            @"simtest_ctct_contact\ct3xx\CTCT_Contact_Test_Error.ctxprg",
            @"simtest_ctct_contact\wireplan",
            @"simtest_ctct_contact\wireplan",
            string.Empty);

        Assert.IsTrue(observer.Evaluations.Any(item =>
                item.Outcome == TestOutcome.Error &&
                item.Details!.Contains("nicht aufgeloest", StringComparison.OrdinalIgnoreCase)),
            string.Join(Environment.NewLine, observer.Evaluations.Select(item => $"{item.StepName}: {item.Outcome} {item.Details}")));
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

    private static void AssertSmudPass(SimulationObserverSpy observer, params string[] expectedStepNames)
    {
        var smudEvaluations = observer.Evaluations
            .Where(item => expectedStepNames.Contains(item.StepName, StringComparer.OrdinalIgnoreCase))
            .ToList();
        var diagnostics = string.Join(Environment.NewLine, observer.Evaluations.Select(item => $"{item.StepName}: {item.Outcome} {item.Details}")) +
            Environment.NewLine +
            string.Join(Environment.NewLine, observer.Messages);

        Assert.AreEqual(expectedStepNames.Length, smudEvaluations.Count,
            diagnostics);
        Assert.IsTrue(smudEvaluations.All(item => item.Outcome == TestOutcome.Pass),
            diagnostics);
        Assert.IsTrue(smudEvaluations.All(item => item.Traces.Any(trace => trace.Title.Contains("SMUD", StringComparison.OrdinalIgnoreCase))),
            diagnostics +
            Environment.NewLine +
            string.Join(Environment.NewLine, smudEvaluations.SelectMany(item => item.Traces.Select(trace => $"{item.StepName}: {trace.Title} => {string.Join(" -> ", trace.Nodes)}"))));
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

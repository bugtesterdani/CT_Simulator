// Provides Wire Viz Element Behavior Tests for the simulator test project support code.
using Ct3xxProgramParser.Programs;
using Ct3xxSimulationModelParser.Parsing;
using Ct3xxSimulator.Simulation.FaultInjection;
using Ct3xxSimulator.Simulation.WireViz;

namespace Ct3xxSimulator.Tests;

[TestClass]
/// <summary>
/// Represents the wire viz element behavior tests.
/// </summary>
public sealed class WireVizElementBehaviorTests
{
    [TestMethod]
    /// <summary>
    /// Executes relay and resistor should resolve path trace and relay state.
    /// </summary>
    public void RelayAndResistor_ShouldResolve_PathTraceAndRelayState()
    {
        using var scenario = CreateScenario(
            """
            connectors:
              CT3:
                bgcolor: WH
                pins: [1, 2]
                pinlabels: [SIG, CTRL]

              RELAY:
                bgcolor: YE
                pins: [COM, NO]
                pinlabels: [COM, NO]

              R1:
                bgcolor: YE
                pins: [A, B]
                pinlabels: [A, B]

              DevicePort:
                bgcolor: YE
                pins: [INPUT]
                pinlabels: [INPUT]

            connections:
              -
                - CT3: [1]
                - RELAY: [COM]
              -
                - RELAY: [NO]
                - R1: [A]
              -
                - R1: [B]
                - DevicePort: [INPUT]
            """,
            """
            elements:
              - id: RELAY
                type: relay
                coil:
                  signal: CTRL
                  threshold_v: 5.0
                delay_ms: 20
                contacts:
                  - a: RELAY.COM
                    b: RELAY.NO
                    mode: normally_open

              - id: R1
                type: resistor
                a: R1.A
                b: R1.B
                ohms: 220
            """,
            "SIG",
            "CTRL");

        var openState = ScenarioState(("CTRL", 0d));
        AssertNoDeviceTarget(scenario, "SIG", openState.Values, openState.Times, forWrite: true);

        var closedState = ScenarioState(("CTRL", 24d));
        var target = AssertSingleDeviceTarget(scenario, "SIG", closedState.Values, closedState.Times, forWrite: true);
        Assert.AreEqual("SIG", target.SignalName);
        Assert.AreEqual("DevicePort.INPUT", target.Endpoint?.Key);

        var traces = scenario.Trace("SIG", closedState.Values, closedState.Times);
        Assert.IsTrue(traces.Any(trace => trace.Nodes.Any(node => node.Contains("RELAY.COM", StringComparison.OrdinalIgnoreCase))));
        Assert.IsTrue(traces.Any(trace => trace.Nodes.Any(node => node.Contains("RELAY.NO", StringComparison.OrdinalIgnoreCase))));
        Assert.IsTrue(traces.Any(trace => trace.Nodes.Any(node => node.Contains("R1.A", StringComparison.OrdinalIgnoreCase))));
        Assert.IsTrue(traces.Any(trace => trace.Nodes.Any(node => node.Contains("DevicePort.INPUT", StringComparison.OrdinalIgnoreCase))));

        var relayStates = scenario.Resolver.DescribeRelayStates(closedState.Values, closedState.Times, 100L, SimulationFaultSet.Empty);
        Assert.IsTrue(relayStates.Any(item => item.Contains("RELAY: geschlossen", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    /// <summary>
    /// Executes switch and fuse should respect control and faults.
    /// </summary>
    public void SwitchAndFuse_ShouldRespect_ControlAndFaults()
    {
        using var scenario = CreateScenario(
            """
            connectors:
              CT3:
                bgcolor: WH
                pins: [1, 2]
                pinlabels: [SIG, CTRL]

              SWITCH:
                bgcolor: YE
                pins: [A, B]
                pinlabels: [A, B]

              F1:
                bgcolor: YE
                pins: [A, B]
                pinlabels: [A, B]

              DevicePort:
                bgcolor: YE
                pins: [INPUT]
                pinlabels: [INPUT]

            connections:
              -
                - CT3: [1]
                - SWITCH: [A]
              -
                - SWITCH: [B]
                - F1: [A]
              -
                - F1: [B]
                - DevicePort: [INPUT]
            """,
            """
            elements:
              - id: SWITCH
                type: switch
                a: SWITCH.A
                b: SWITCH.B
                control_signal: CTRL
                threshold_v: 5.0

              - id: F1
                type: fuse
                a: F1.A
                b: F1.B
            """,
            "SIG",
            "CTRL");

        var openState = ScenarioState(("CTRL", 0d));
        AssertNoDeviceTarget(scenario, "SIG", openState.Values, openState.Times, forWrite: true);

        var closedState = ScenarioState(("CTRL", 24d));
        var target = AssertSingleDeviceTarget(scenario, "SIG", closedState.Values, closedState.Times, forWrite: true);
        Assert.AreEqual("DevicePort.INPUT", target.Endpoint?.Key);

        var blownFuse = new SimulationFaultSet(
            new[]
            {
                new SimulationFaultDefinition
                {
                    Id = "blown-f1",
                    Type = "blow_fuse",
                    ElementId = "F1",
                    Enabled = true
                }
            },
            "inline");

        AssertNoDeviceTarget(scenario, "SIG", closedState.Values, closedState.Times, forWrite: true, blownFuse);
        var states = scenario.Resolver.DescribeElementStates(closedState.Values, closedState.Times, 100L, blownFuse);
        Assert.IsTrue(states.Any(item => item.Contains("F1", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(states.Any(item => item.Contains("SWITCH", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    /// <summary>
    /// Executes diode load and voltage divider should respect direction and scaling.
    /// </summary>
    public void DiodeLoadAndVoltageDivider_ShouldRespect_DirectionAndScaling()
    {
        using var dividerScenario = CreateScenario(
            """
            connectors:
              CT3:
                bgcolor: WH
                pins: [1]
                pinlabels: [SIG]

              D1:
                bgcolor: YE
                pins: [A, K]
                pinlabels: [A, K]

              LOAD:
                bgcolor: YE
                pins: [A, B]
                pinlabels: [A, B]

              DIV:
                bgcolor: YE
                pins: [IN, OUT]
                pinlabels: [IN, OUT]

              DevicePort:
                bgcolor: YE
                pins: [ANA]
                pinlabels: [ANA]

            connections:
              -
                - CT3: [1]
                - D1: [A]
              -
                - D1: [K]
                - LOAD: [A]
              -
                - LOAD: [B]
                - DIV: [IN]
              -
                - DIV: [OUT]
                - DevicePort: [ANA]
            """,
            """
            elements:
              - id: D1
                type: diode
                anode: D1.A
                cathode: D1.K

              - id: LOAD
                type: load
                a: LOAD.A
                b: LOAD.B
                ohms: 100

              - id: DIV
                type: voltage_divider
                input: DIV.IN
                output: DIV.OUT
                ratio: 0.5
            """,
            "SIG");

        var state = ScenarioState();
        var dividerTarget = AssertSingleDeviceTarget(dividerScenario, "SIG", state.Values, state.Times, forWrite: true);
        Assert.AreEqual("ANA", dividerTarget.SignalName);
        Assert.AreEqual(0.5d, dividerTarget.SourceToTargetScale, 0.0001d);

        using var reverseDiodeScenario = CreateScenario(
            """
            connectors:
              CT3:
                bgcolor: WH
                pins: [1]
                pinlabels: [REV]

              D1:
                bgcolor: YE
                pins: [A, K]
                pinlabels: [A, K]

              DevicePort:
                bgcolor: YE
                pins: [ANA]
                pinlabels: [ANA]

            connections:
              -
                - CT3: [1]
                - D1: [K]
              -
                - D1: [A]
                - DevicePort: [ANA]
            """,
            """
            elements:
              - id: D1
                type: diode
                anode: D1.A
                cathode: D1.K
            """,
            "REV");

        AssertNoDeviceTarget(reverseDiodeScenario, "REV", state.Values, state.Times, forWrite: true);
    }

    [TestMethod]
    /// <summary>
    /// Executes sensor opto and transistor should expose expected behavior.
    /// </summary>
    public void SensorOptoAndTransistor_ShouldExposeExpectedBehavior()
    {
        using var sensorScenario = CreateScenario(
            """
            connectors:
              CT3:
                bgcolor: WH
                pins: [1]
                pinlabels: [SENSE]

              S1:
                bgcolor: YE
                pins: [OUT]
                pinlabels: [OUT]

            connections:
              -
                - CT3: [1]
                - S1: [OUT]
            """,
            """
            elements:
              - id: S1
                type: sensor
                output: S1.OUT
                output_signal: TEMP_SIGNAL
            """,
            "SENSE");

        var emptyState = ScenarioState();
        var sensorTargets = sensorScenario.ResolveTargets("SENSE", emptyState.Values, emptyState.Times, forWrite: false);
        Assert.IsTrue(sensorTargets.Any(item => item.Synthetic && item.SignalName == "TEMP_SIGNAL"));

        using var optoScenario = CreateScenario(
            """
            connectors:
              CT3:
                bgcolor: WH
                pins: [1, 2]
                pinlabels: [SIG, CTRL]

              OPTO:
                bgcolor: YE
                pins: [A, B]
                pinlabels: [A, B]

              DevicePort:
                bgcolor: YE
                pins: [INPUT]
                pinlabels: [INPUT]

            connections:
              -
                - CT3: [1]
                - OPTO: [A]
              -
                - OPTO: [B]
                - DevicePort: [INPUT]
            """,
            """
            elements:
              - id: OPTO
                type: opto
                led_signal: CTRL
                threshold_v: 5.0
                output_a: OPTO.A
                output_b: OPTO.B
            """,
            "SIG",
            "CTRL");

        AssertNoDeviceTarget(optoScenario, "SIG", ScenarioState(("CTRL", 0d)).Values, ScenarioState(("CTRL", 0d)).Times, forWrite: true);
        AssertSingleDeviceTarget(optoScenario, "SIG", ScenarioState(("CTRL", 24d)).Values, ScenarioState(("CTRL", 24d)).Times, forWrite: true);

        using var transistorScenario = CreateScenario(
            """
            connectors:
              CT3:
                bgcolor: WH
                pins: [1, 2]
                pinlabels: [SIG, CTRL]

              Q1:
                bgcolor: YE
                pins: [C, E]
                pinlabels: [C, E]

              DevicePort:
                bgcolor: YE
                pins: [INPUT]
                pinlabels: [INPUT]

            connections:
              -
                - CT3: [1]
                - Q1: [C]
              -
                - Q1: [E]
                - DevicePort: [INPUT]
            """,
            """
            elements:
              - id: Q1
                type: transistor
                collector: Q1.C
                emitter: Q1.E
                gate_signal: CTRL
                threshold_v: 5.0
                transistor_type: npn
            """,
            "SIG",
            "CTRL");

        AssertNoDeviceTarget(transistorScenario, "SIG", ScenarioState(("CTRL", 0d)).Values, ScenarioState(("CTRL", 0d)).Times, forWrite: true);
        AssertSingleDeviceTarget(transistorScenario, "SIG", ScenarioState(("CTRL", 24d)).Values, ScenarioState(("CTRL", 24d)).Times, forWrite: true);
    }

    [TestMethod]
    /// <summary>
    /// Executes transformer and current transformer should expose scaling and synthetic readback.
    /// </summary>
    public void TransformerAndCurrentTransformer_ShouldExposeScalingAndSyntheticReadback()
    {
        using var scenario = CreateScenario(
            """
            connectors:
              CT3:
                bgcolor: WH
                pins: [1, 2]
                pinlabels: [PRI_IN, CT_SEC]

              T1:
                bgcolor: YE
                pins: [P1, P2, S1, S2]
                pinlabels: [P1, P2, S1, S2]

              CT1:
                bgcolor: YE
                pins: [S1, S2]
                pinlabels: [S1, S2]

              DevicePort:
                bgcolor: YE
                pins: [VT_INPUT]
                pinlabels: [VT_INPUT]

            connections:
              -
                - CT3: [1]
                - T1: [P1]
              -
                - T1: [S1]
                - DevicePort: [VT_INPUT]
              -
                - CT3: [2]
                - CT1: [S1]
            """,
            """
            elements:
              - id: T1
                type: transformer
                primary_a: T1.P1
                primary_b: T1.P2
                secondary_a: T1.S1
                secondary_b: T1.S2
                ratio: 10.0

              - id: CT1
                type: current_transformer
                primary_signal: LOAD_CURRENT
                secondary_a: CT1.S1
                secondary_b: CT1.S2
                ratio: 2000
            """,
            "PRI_IN",
            "CT_SEC");

        var state = ScenarioState();
        var transformerTarget = AssertSingleDeviceTarget(scenario, "PRI_IN", state.Values, state.Times, forWrite: true);
        Assert.AreEqual("VT_INPUT", transformerTarget.SignalName);
        Assert.AreEqual(0.1d, transformerTarget.SourceToTargetScale, 0.0001d);

        var ctTargets = scenario.ResolveTargets("CT_SEC", state.Values, state.Times, forWrite: false);
        Assert.IsTrue(ctTargets.Any(item => item.Synthetic && item.SignalName == "LOAD_CURRENT" && Math.Abs(item.SourceToTargetScale - 2000d) < 0.0001d));
    }

    [TestMethod]
    /// <summary>
    /// Executes tester output configuration should map high low to declared supply and open.
    /// </summary>
    public void TesterOutputConfiguration_ShouldMap_HighLow_ToDeclaredSupplyAndOpen()
    {
        using var scenario = CreateScenario(
            """
            connectors:
              UIF:
                bgcolor: GN
                pins: [OUT1, 24V]
                pinlabels: [UIF_OUT1, UIF_P_24V]
            """,
            """
            elements:
              - id: UIF_SUPPLY
                type: tester_supply
                signal: UIF_P_24V
                voltage: 24.0

              - id: UIF_OUT1_CFG
                type: tester_output
                signal: UIF_OUT1
                high_mode: supply
                high_supply: UIF_P_24V
                low_mode: open
            """,
            "UIF_OUT1");

        var state = ScenarioState();
        Assert.IsTrue(scenario.Resolver.TryResolveTesterOutputValue("UIF_OUT1", "H", state.Values, out var highValue, out _));
        Assert.AreEqual(24d, Convert.ToDouble(highValue, System.Globalization.CultureInfo.InvariantCulture), 0.0001d);

        Assert.IsTrue(scenario.Resolver.TryResolveTesterOutputValue("UIF_OUT1", "L", state.Values, out var lowValue, out _));
        Assert.IsNull(lowValue);
    }

    /// <summary>
    /// Initializes a new instance of static.
    /// </summary>
    private static (Dictionary<string, object?> Values, Dictionary<string, long> Times) ScenarioState(params (string Signal, object? Value)[] values)
    {
        var signalValues = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var signalTimes = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var (signal, value) in values)
        {
            signalValues[signal] = value;
            signalTimes[signal] = 0L;
        }

        return (signalValues, signalTimes);
    }

    /// <summary>
    /// Executes AssertSingleDeviceTarget.
    /// </summary>
    private static WireVizRuntimeTarget AssertSingleDeviceTarget(
        ResolverScenario scenario,
        string signalName,
        IReadOnlyDictionary<string, object?> signalState,
        IReadOnlyDictionary<string, long> signalTimes,
        bool forWrite,
        SimulationFaultSet? faults = null)
    {
        var target = scenario.ResolveTargets(signalName, signalState, signalTimes, forWrite, faults)
            .FirstOrDefault(item => item.Endpoint?.Key?.Contains("DevicePort", StringComparison.OrdinalIgnoreCase) == true);
        Assert.IsNotNull(target, $"Kein Device-Target fuer '{signalName}' gefunden. Tatsaechliche Targets: {string.Join(", ", scenario.ResolveTargets(signalName, signalState, signalTimes, forWrite, faults).Select(item => item.Endpoint?.Key ?? item.SignalName))}");
        return target!;
    }

    /// <summary>
    /// Executes AssertNoDeviceTarget.
    /// </summary>
    private static void AssertNoDeviceTarget(
        ResolverScenario scenario,
        string signalName,
        IReadOnlyDictionary<string, object?> signalState,
        IReadOnlyDictionary<string, long> signalTimes,
        bool forWrite,
        SimulationFaultSet? faults = null)
    {
        var targets = scenario.ResolveTargets(signalName, signalState, signalTimes, forWrite, faults);
        Assert.IsFalse(targets.Any(item => item.Endpoint?.Key?.Contains("DevicePort", StringComparison.OrdinalIgnoreCase) == true),
            $"Unerwarteter Device-Target fuer '{signalName}': {string.Join(", ", targets.Select(item => item.Endpoint?.Key ?? item.SignalName))}");
    }

    /// <summary>
    /// Executes CreateScenario.
    /// </summary>
    private static ResolverScenario CreateScenario(string wirevizYaml, string simulationYaml, params string[] signalNames) =>
        ResolverScenario.Create(wirevizYaml, simulationYaml, signalNames);

    private sealed class ResolverScenario : IDisposable
    {
        /// <summary>
        /// Initializes a new instance of ResolverScenario.
        /// </summary>
        private ResolverScenario(string directory, WireVizHarnessResolver resolver)
        {
            DirectoryPath = directory;
            Resolver = resolver;
        }

        /// <summary>
        /// Gets the directory path.
        /// </summary>
        public string DirectoryPath { get; }
        /// <summary>
        /// Gets the resolver.
        /// </summary>
        public WireVizHarnessResolver Resolver { get; }

        /// <summary>
        /// Executes create.
        /// </summary>
        public static ResolverScenario Create(string wirevizYaml, string simulationYaml, IReadOnlyList<string> signalNames)
        {
            var directory = Path.Combine(Path.GetTempPath(), "ct3xx-wireviz-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);

            File.Copy(TestData.GetPath(@"simtest\ct3xx\Simulator_Test.ctxprg"), Path.Combine(directory, "Simulator_Test.ctxprg"));
            File.WriteAllText(Path.Combine(directory, "CAD.ctbrd"), string.Empty);
            File.WriteAllText(Path.Combine(directory, "Interfacetable.ctifc"), string.Empty);
            File.WriteAllText(Path.Combine(directory, "Signaltable.ctsit"), BuildSignalTable(signalNames));

            var wirevizPath = Path.Combine(directory, "wireviz.yaml");
            var simulationPath = Path.Combine(directory, "simulation.yaml");
            File.WriteAllText(wirevizPath, wirevizYaml);
            File.WriteAllText(simulationPath, simulationYaml);

            var parser = new Ct3xxProgramFileParser();
            var fileSet = parser.Load(Path.Combine(directory, "Simulator_Test.ctxprg"));
            var simulationDocument = new SimulationModelParser().ParseFile(simulationPath);
            var resolver = WireVizHarnessResolver.Create(fileSet, new[] { wirevizPath }, simulationDocument);
            return new ResolverScenario(directory, resolver);
        }

        /// <summary>
        /// Resolves the targets.
        /// </summary>
        public IReadOnlyList<WireVizRuntimeTarget> ResolveTargets(
            string signalName,
            IReadOnlyDictionary<string, object?> signalState,
            IReadOnlyDictionary<string, long> signalTimes,
            bool forWrite,
            SimulationFaultSet? faults = null)
        {
            Assert.IsTrue(Resolver.TryResolveRuntimeTargets(signalName, signalState, signalTimes, 100L, faults ?? SimulationFaultSet.Empty, forWrite, out var targets));
            return targets;
        }

        /// <summary>
        /// Executes trace.
        /// </summary>
        public IReadOnlyList<WireVizSignalTrace> Trace(
            string signalName,
            IReadOnlyDictionary<string, object?> signalState,
            IReadOnlyDictionary<string, long> signalTimes)
        {
            Assert.IsTrue(Resolver.TryTrace(signalName, signalState, signalTimes, 100L, SimulationFaultSet.Empty, out var traces));
            return traces;
        }

        /// <summary>
        /// Executes dispose.
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (Directory.Exists(DirectoryPath))
                {
                    Directory.Delete(DirectoryPath, recursive: true);
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// Executes BuildSignalTable.
        /// </summary>
        private static string BuildSignalTable(IReadOnlyList<string> signalNames)
        {
            var lines = new List<string>
            {
                ";------------------------------------------------",
                "; 1. SC3   channels 1 to 144",
                ";------------------------------------------------",
                ":MODULE 'SC3', \"Mode 144x4\"",
                string.Empty
            };

            for (var index = 0; index < signalNames.Count; index++)
            {
                lines.Add($"{index + 1,6}   \"{signalNames[index]}\"    0\t\"\"");
            }

            return string.Join(Environment.NewLine, lines) + Environment.NewLine;
        }
    }
}

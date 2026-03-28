using System.Text.Json.Nodes;
using System.Threading;
using Ct3xxSimulator.Simulation.Devices;

namespace Ct3xxSimulator.Tests;

[TestClass]
/// <summary>
/// Verifies direct I2C slave runtime behavior exposed by declarative device profiles.
/// </summary>
public sealed class I2cDeviceRuntimeTests
{
    [TestMethod]
    /// <summary>
    /// Verifies that declarative I2C profiles keep register writes for the duration of one test run.
    /// </summary>
    public void DeclarativeI2cProfile_ShouldPersist_RegisterWrites_WithinOneTestRun()
    {
        using var python = PythonDeviceProcessFixture.StartProfile(@"simtest\device\devices\i2c_lm75_good.yaml");
        using var client = new PythonDeviceSimulatorClient(python.PipePath);

        var hello = client.Hello(0, CancellationToken.None);
        Assert.IsTrue(hello.Ok, hello.ErrorMessage);

        AssertI2cResponse(
            client.SendInterface(
                "UIF1_FRONT_CONNECTOR",
                new
                {
                    tester_role = "master",
                    external_device_role = "slave",
                    start_condition = true,
                    stop_condition = false,
                    ack_mode = "READ",
                    to_send = 0x92,
                    supply_voltage = 5.0,
                },
                0,
                CancellationToken.None),
            expectedAcknowledged: true);

        AssertI2cResponse(
            client.SendInterface(
                "UIF1_FRONT_CONNECTOR",
                new
                {
                    tester_role = "master",
                    external_device_role = "slave",
                    start_condition = false,
                    stop_condition = false,
                    ack_mode = "READ",
                    to_send = 0x01,
                    supply_voltage = 5.0,
                },
                1,
                CancellationToken.None),
            expectedAcknowledged: true);

        AssertI2cResponse(
            client.SendInterface(
                "UIF1_FRONT_CONNECTOR",
                new
                {
                    tester_role = "master",
                    external_device_role = "slave",
                    start_condition = false,
                    stop_condition = true,
                    ack_mode = "READ",
                    to_send = 0x5A,
                    supply_voltage = 5.0,
                },
                2,
                CancellationToken.None),
            expectedAcknowledged: true);

        AssertI2cResponse(
            client.SendInterface(
                "UIF1_FRONT_CONNECTOR",
                new
                {
                    tester_role = "master",
                    external_device_role = "slave",
                    start_condition = true,
                    stop_condition = false,
                    ack_mode = "READ",
                    to_send = 0x92,
                    supply_voltage = 5.0,
                },
                3,
                CancellationToken.None),
            expectedAcknowledged: true);

        AssertI2cResponse(
            client.SendInterface(
                "UIF1_FRONT_CONNECTOR",
                new
                {
                    tester_role = "master",
                    external_device_role = "slave",
                    start_condition = false,
                    stop_condition = false,
                    ack_mode = "READ",
                    to_send = 0x01,
                    supply_voltage = 5.0,
                },
                4,
                CancellationToken.None),
            expectedAcknowledged: true);

        AssertI2cResponse(
            client.SendInterface(
                "UIF1_FRONT_CONNECTOR",
                new
                {
                    tester_role = "master",
                    external_device_role = "slave",
                    start_condition = true,
                    stop_condition = false,
                    ack_mode = "READ",
                    to_send = 0x93,
                    supply_voltage = 5.0,
                },
                5,
                CancellationToken.None),
            expectedAcknowledged: true);

        var readback = AssertI2cResponse(
            client.SendInterface(
                "UIF1_FRONT_CONNECTOR",
                new
                {
                    tester_role = "master",
                    external_device_role = "slave",
                    start_condition = false,
                    stop_condition = true,
                    ack_mode = "NO ACK",
                    transfer_phase = "master_read",
                    to_send = 0x00,
                    supply_voltage = 5.0,
                },
                6,
                CancellationToken.None),
            expectedAcknowledged: true);

        Assert.AreEqual(0x5A, readback["actual_byte"]?.GetValue<int>() ?? -1);

        var stateResponse = client.ReadState(6, CancellationToken.None);
        Assert.IsTrue(stateResponse.Ok, stateResponse.ErrorMessage);
        var interfaces = stateResponse.Result?["interfaces"]?.AsObject();
        var registerPreview = interfaces?["UIF1_FRONT_CONNECTOR"]?["i2c"]?["devices"]?["LM75"]?["register_preview"]?.AsObject();
        Assert.IsNotNull(registerPreview);
        Assert.AreEqual("0x5A", registerPreview["0x01"]?.GetValue<string>());
    }

    private static JsonObject AssertI2cResponse(ExternalDeviceResponse response, bool expectedAcknowledged)
    {
        Assert.IsTrue(response.Ok, response.ErrorMessage);
        var result = response.Result as JsonObject;
        Assert.IsNotNull(result, "I2C response payload fehlt.");
        var protocolResponse = result["response"] as JsonObject;
        Assert.IsNotNull(protocolResponse, result.ToJsonString());
        Assert.AreEqual(expectedAcknowledged, protocolResponse["acknowledged"]?.GetValue<bool>() ?? false, result.ToJsonString());
        return protocolResponse;
    }
}

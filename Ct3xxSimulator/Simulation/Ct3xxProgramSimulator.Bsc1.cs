using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using System.Xml;
using Ct3xxProgramParser.Model;

namespace Ct3xxSimulator.Simulation;

/// <summary>
/// Executes CT3xx BSC1 (Boundary Scan) tests against the external DUT simulation.
/// </summary>
public partial class Ct3xxProgramSimulator
{
    /// <summary>
    /// Executes RunBoundaryScanTest.
    /// </summary>
    private TestOutcome RunBoundaryScanTest(Test test)
    {
        var parameters = test.Parameters;
        if (parameters == null)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "BSC1 ohne Parameter.");
            return TestOutcome.Error;
        }

        if (_externalDeviceSession == null)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "BSC1 ohne aktive Geraetesimulation.");
            return TestOutcome.Error;
        }

        var chain = TryParseBsc1Chain(parameters, out var chainError);
        if (chain == null)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: chainError ?? "BSC1 ohne gueltige Chain.");
            return TestOutcome.Error;
        }

        var partialCount = ParseOptionalInt(GetParameterAttribute(parameters, "PartialTests"));
        var splitEnabled = string.Equals(test.Split, "yes", StringComparison.OrdinalIgnoreCase);
        var testName = parameters.Name ?? test.Name ?? test.Id ?? "BSC1";
        var drawingReference = parameters.DrawingReference ?? string.Empty;

        var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["protocol"] = "bsc1",
            ["operation"] = "boundary_scan",
            ["tester_role"] = "master",
            ["external_device_role"] = "dut",
            ["test_name"] = testName,
            ["drawing_reference"] = drawingReference,
            ["split"] = splitEnabled,
            ["partial_tests"] = partialCount,
            ["chain"] = BuildBsc1Payload(chain),
        };

        if (!_externalDeviceSession.TrySendInterface("BSC1", payload, _cancellationToken, out var responsePayload, out var error, _simulatedTimeMs))
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: $"BSC1 Kommunikationsfehler: {error ?? "keine Antwort"}");
            return TestOutcome.Error;
        }

        RefreshExternalDeviceState();
        var response = responsePayload as JsonObject;
        if (response == null)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "BSC1: Ungueltige Antwort.");
            return TestOutcome.Error;
        }

        if (IsErrorResponse(response, out var responseError))
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: responseError ?? "BSC1 Fehler.");
            return TestOutcome.Error;
        }

        var overall = ParseOutcome(response["outcome"]?.GetValue<string?>());
        var details = response["details"]?.GetValue<string?>();
        var partials = ParsePartialResults(response);

        if (splitEnabled && partialCount.HasValue && partialCount.Value > 0 && partials.Count == 0)
        {
            partials = BuildDefaultPartialResults(partialCount.Value, overall, details);
        }

        if (splitEnabled && partials.Count > 0)
        {
            var aggregated = TestOutcome.Pass;
            foreach (var partial in partials)
            {
                aggregated = CombineOutcomes(aggregated, partial.Outcome);
                var name = partial.Name ?? $"Boundary-Scan Teiltest {partial.Index}";
                PublishStepEvaluation(test, partial.Outcome, details: partial.Details, stepNameOverride: name);
            }

            return aggregated;
        }

        var chainSummary = BuildChainSummary(chain);
        var summary = string.IsNullOrWhiteSpace(details)
            ? chainSummary
            : $"{details} | {chainSummary}";
        PublishStepEvaluation(test, overall, details: summary);
        return overall;
    }

    /// <summary>
    /// Executes TryParseBsc1Chain.
    /// </summary>
    private static Bsc1Chain? TryParseBsc1Chain(TestParameters parameters, out string? error)
    {
        error = null;
        if (parameters.ExtraElements == null || parameters.ExtraElements.Length == 0)
        {
            error = "BSC1 ohne Chain-Daten.";
            return null;
        }

        var chainElement = parameters.ExtraElements
            .FirstOrDefault(element => element.Name.Equals("Chain", StringComparison.OrdinalIgnoreCase));
        if (chainElement == null)
        {
            error = "BSC1 ohne Chain-Element.";
            return null;
        }

        var chain = new Bsc1Chain
        {
            Id = chainElement.GetAttribute("Id"),
            Revision = chainElement.GetAttribute("Rev"),
            IcCount = ParseOptionalInt(chainElement.GetAttribute("Ics"))
        };

        foreach (var icElement in chainElement.ChildNodes.OfType<XmlElement>()
                     .Where(element => element.Name.Equals("Ic", StringComparison.OrdinalIgnoreCase)))
        {
            var ic = new Bsc1Ic
            {
                Id = icElement.GetAttribute("Id"),
                Revision = icElement.GetAttribute("Rev"),
                Name = icElement.GetAttribute("Name"),
                Type = icElement.GetAttribute("Type"),
                DevId = icElement.GetAttribute("DevId"),
                InstrSize = ParseOptionalInt(icElement.GetAttribute("InstrSize")),
                Bypass = icElement.GetAttribute("Bypass"),
                Extest = icElement.GetAttribute("Extest"),
                Idcode = icElement.GetAttribute("Idcode"),
                Sample = icElement.GetAttribute("Sample"),
                Cells = ParseOptionalInt(icElement.GetAttribute("Cells"))
            };

            foreach (var cellElement in icElement.ChildNodes.OfType<XmlElement>()
                         .Where(element => element.Name.Equals("Cell", StringComparison.OrdinalIgnoreCase)))
            {
                ic.CellList.Add(new Bsc1Cell
                {
                    Id = cellElement.GetAttribute("Id"),
                    Revision = cellElement.GetAttribute("Rev"),
                    Number = ParseOptionalInt(cellElement.GetAttribute("No")),
                    Port = cellElement.GetAttribute("Port"),
                    Pin = cellElement.GetAttribute("Pin"),
                    Type = cellElement.GetAttribute("Type"),
                    Kind = cellElement.GetAttribute("Kind"),
                    ControlBy = ParseOptionalInt(cellElement.GetAttribute("CtrlBy")),
                    ControlValue = cellElement.GetAttribute("CtrlVal"),
                    ControlResult = cellElement.GetAttribute("CtrlRes"),
                    Safe = cellElement.GetAttribute("Safe")
                });
            }

            chain.Ics.Add(ic);
        }

        if (chain.Ics.Count == 0)
        {
            error = "BSC1 ohne IC-Definitionen.";
            return null;
        }

        return chain;
    }

    /// <summary>
    /// Executes BuildBsc1Payload.
    /// </summary>
    private static Dictionary<string, object?> BuildBsc1Payload(Bsc1Chain chain)
    {
        var icPayloads = new List<Dictionary<string, object?>>();
        foreach (var ic in chain.Ics)
        {
            var cellPayloads = new List<Dictionary<string, object?>>();
            foreach (var cell in ic.CellList)
            {
                cellPayloads.Add(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["id"] = cell.Id,
                    ["rev"] = cell.Revision,
                    ["no"] = cell.Number,
                    ["port"] = cell.Port,
                    ["pin"] = cell.Pin,
                    ["type"] = cell.Type,
                    ["kind"] = cell.Kind,
                    ["ctrl_by"] = cell.ControlBy,
                    ["ctrl_val"] = cell.ControlValue,
                    ["ctrl_res"] = cell.ControlResult,
                    ["safe"] = cell.Safe
                });
            }

            icPayloads.Add(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = ic.Id,
                ["rev"] = ic.Revision,
                ["name"] = ic.Name,
                ["type"] = ic.Type,
                ["dev_id"] = ic.DevId,
                ["instr_size"] = ic.InstrSize,
                ["bypass"] = ic.Bypass,
                ["extest"] = ic.Extest,
                ["idcode"] = ic.Idcode,
                ["sample"] = ic.Sample,
                ["cells"] = ic.Cells,
                ["cell_list"] = cellPayloads
            });
        }

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = chain.Id,
            ["rev"] = chain.Revision,
            ["ics"] = chain.IcCount,
            ["ic_list"] = icPayloads
        };
    }

    /// <summary>
    /// Executes ParsePartialResults.
    /// </summary>
    private static List<Bsc1PartialResult> ParsePartialResults(JsonObject response)
    {
        var partials = new List<Bsc1PartialResult>();
        if (response["partials"] is not JsonArray array)
        {
            return partials;
        }

        foreach (var entry in array.OfType<JsonObject>())
        {
            var index = entry["index"]?.GetValue<int?>() ?? entry["partial"]?.GetValue<int?>() ?? 0;
            var name = entry["name"]?.GetValue<string?>() ?? entry["label"]?.GetValue<string?>();
            var outcome = ParseOutcome(entry["outcome"]?.GetValue<string?>() ?? entry["status"]?.GetValue<string?>());
            var details = entry["details"]?.GetValue<string?>();
            partials.Add(new Bsc1PartialResult(index <= 0 ? partials.Count + 1 : index, name, outcome, details));
        }

        return partials;
    }

    /// <summary>
    /// Executes BuildDefaultPartialResults.
    /// </summary>
    private static List<Bsc1PartialResult> BuildDefaultPartialResults(int count, TestOutcome outcome, string? details)
    {
        var results = new List<Bsc1PartialResult>();
        for (var index = 1; index <= count; index++)
        {
            results.Add(new Bsc1PartialResult(index, null, outcome, details));
        }

        return results;
    }

    /// <summary>
    /// Executes BuildChainSummary.
    /// </summary>
    private static string BuildChainSummary(Bsc1Chain chain)
    {
        var icCount = chain.Ics.Count;
        var cellCount = chain.Ics.Sum(ic => ic.CellList.Count);
        var expectedIcs = chain.IcCount.HasValue ? chain.IcCount.Value.ToString(CultureInfo.InvariantCulture) : "?";
        return $"Chain: ICs={icCount}/{expectedIcs}, Cells={cellCount}";
    }

    /// <summary>
    /// Executes IsErrorResponse.
    /// </summary>
    private static bool IsErrorResponse(JsonObject response, out string? details)
    {
        details = null;
        var status = response["status"]?.GetValue<string?>();
        if (string.Equals(status, "error", StringComparison.OrdinalIgnoreCase))
        {
            details = response["details"]?.GetValue<string?>();
            if (string.IsNullOrWhiteSpace(details))
            {
                details = "BSC1 Fehler.";
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Executes ParseOutcome.
    /// </summary>
    private static TestOutcome ParseOutcome(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return TestOutcome.Pass;
        }

        var normalized = raw.Trim().ToLowerInvariant();
        return normalized switch
        {
            "pass" => TestOutcome.Pass,
            "ok" => TestOutcome.Pass,
            "fail" => TestOutcome.Fail,
            "error" => TestOutcome.Error,
            _ => TestOutcome.Error
        };
    }

    /// <summary>
    /// Executes ParseOptionalInt.
    /// </summary>
    private static int? ParseOptionalInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim().Trim('\'', '"');
        return int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    /// <summary>
    /// Represents one parsed Boundary Scan chain.
    /// </summary>
    private sealed class Bsc1Chain
    {
        public string? Id { get; set; }
        public string? Revision { get; set; }
        public int? IcCount { get; set; }
        public List<Bsc1Ic> Ics { get; } = new();
    }

    /// <summary>
    /// Represents one IC inside a Boundary Scan chain.
    /// </summary>
    private sealed class Bsc1Ic
    {
        public string? Id { get; set; }
        public string? Revision { get; set; }
        public string? Name { get; set; }
        public string? Type { get; set; }
        public string? DevId { get; set; }
        public int? InstrSize { get; set; }
        public string? Bypass { get; set; }
        public string? Extest { get; set; }
        public string? Idcode { get; set; }
        public string? Sample { get; set; }
        public int? Cells { get; set; }
        public List<Bsc1Cell> CellList { get; } = new();
    }

    /// <summary>
    /// Represents one boundary scan cell definition.
    /// </summary>
    private sealed class Bsc1Cell
    {
        public string? Id { get; set; }
        public string? Revision { get; set; }
        public int? Number { get; set; }
        public string? Port { get; set; }
        public string? Pin { get; set; }
        public string? Type { get; set; }
        public string? Kind { get; set; }
        public int? ControlBy { get; set; }
        public string? ControlValue { get; set; }
        public string? ControlResult { get; set; }
        public string? Safe { get; set; }
    }

    /// <summary>
    /// Represents one partial boundary scan result.
    /// </summary>
    private sealed record Bsc1PartialResult(int Index, string? Name, TestOutcome Outcome, string? Details);
}

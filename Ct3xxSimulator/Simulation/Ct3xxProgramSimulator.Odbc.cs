using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Odbc;
using System.Globalization;
using System.Linq;
using Ct3xxProgramParser.Model;
using Ct3xxSimulationModelParser.Model;

namespace Ct3xxSimulator.Simulation;

/// <summary>
/// Executes CT3xx ODBC tests against a real or mocked ODBC backend.
/// </summary>
public partial class Ct3xxProgramSimulator
{
    /// <summary>
    /// Executes RunOdbcTest.
    /// </summary>
    private TestOutcome RunOdbcTest(Test test)
    {
        var parameters = test.Parameters;
        if (parameters == null)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "ODBC ohne Parameter.");
            return TestOutcome.Error;
        }

        if (!TryResolveOdbcInputs(parameters, out var inputs, out var inputError))
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: inputError ?? "ODBC Parameter ungueltig.");
            return TestOutcome.Error;
        }

        var config = ResolveOdbcConfiguration();
        if (inputs.CommandTimeoutSeconds.HasValue)
        {
            config.TimeoutSeconds = inputs.CommandTimeoutSeconds.Value;
        }
        if (config.Mode == OdbcMode.Mock)
        {
            var mockResult = string.IsNullOrWhiteSpace(config.MockResult)
                ? "ODBC Mock: OK"
                : config.MockResult!;
            if (!string.IsNullOrWhiteSpace(inputs.ResultVariable))
            {
                _context.SetValue(VariableAddress.From(inputs.ResultVariable), mockResult);
            }

            PublishStepEvaluation(test, TestOutcome.Pass, details: BuildOdbcDetails(inputs, mockResult, "mock", null));
            return TestOutcome.Pass;
        }

        var connectionString = BuildOdbcConnectionString(inputs.DriverName, inputs.DatabaseLocation, inputs.ConnectionAdditionalParams);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "ODBC ohne Connection-String.");
            return TestOutcome.Error;
        }

        try
        {
            using var connection = new OdbcConnection(connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = inputs.SqlStatement ?? string.Empty;
            if (config.TimeoutSeconds.HasValue)
            {
                command.CommandTimeout = config.TimeoutSeconds.Value;
            }

            var sqlKind = DetermineSqlKind(inputs.SqlStatement);
            OdbcExecutionResult executionResult = sqlKind == OdbcSqlKind.Query
                ? ExecuteOdbcQuery(command)
                : ExecuteOdbcNonQuery(command);

            if (!string.IsNullOrWhiteSpace(inputs.ResultVariable))
            {
                _context.SetValue(VariableAddress.From(inputs.ResultVariable), executionResult.ResultText);
            }

            PublishStepEvaluation(test, TestOutcome.Pass, details: BuildOdbcDetails(inputs, executionResult.ResultText, "real", executionResult.RowsAffected));
            return TestOutcome.Pass;
        }
        catch (OdbcException ex)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: $"ODBC Fehler: {ex.Message}");
            return TestOutcome.Error;
        }
        catch (InvalidOperationException ex)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: $"ODBC Fehler: {ex.Message}");
            return TestOutcome.Error;
        }
    }

    private bool TryResolveOdbcInputs(TestParameters parameters, out OdbcInputs inputs, out string? error)
    {
        error = null;
        inputs = new OdbcInputs();

        if (!TryResolveParameter(parameters, "ODBCdriverName", out var driverName, out error))
        {
            return false;
        }

        if (!TryResolveParameter(parameters, "SQLstatement", out var sqlStatement, out error))
        {
            return false;
        }

        if (!TryResolveParameter(parameters, "DataBaseQ", out var databaseLocation, out error))
        {
            return false;
        }

        _ = TryResolveParameter(parameters, "DBconnectionAdditionalParams", out var additionalParams, out _);
        var resultVariable = NormalizeQuotedText(GetParameterAttribute(parameters, "Variable"));
        var additionalAttributes = ResolveAdditionalAttributes(parameters);
        var commandTimeoutSeconds = ParseCommandTimeout(additionalAttributes);

        inputs = new OdbcInputs
        {
            DriverName = driverName,
            SqlStatement = sqlStatement,
            DatabaseLocation = databaseLocation,
            ConnectionAdditionalParams = additionalParams,
            ResultVariable = resultVariable,
            AdditionalAttributes = additionalAttributes,
            CommandTimeoutSeconds = commandTimeoutSeconds
        };

        if (string.IsNullOrWhiteSpace(inputs.SqlStatement))
        {
            error = "ODBC ohne SQLstatement.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(inputs.DatabaseLocation))
        {
            error = "ODBC ohne DataBaseQ.";
            return false;
        }

        return true;
    }

    private Dictionary<string, string> ResolveAdditionalAttributes(TestParameters parameters)
    {
        var resolved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (parameters.AdditionalAttributes == null)
        {
            return resolved;
        }

        foreach (var attribute in parameters.AdditionalAttributes)
        {
            if (string.IsNullOrWhiteSpace(attribute.Name))
            {
                continue;
            }

            var raw = attribute.Value ?? string.Empty;
            try
            {
                resolved[attribute.Name] = _evaluator.ResolveText(raw);
            }
            catch (UndefinedVariableException)
            {
                resolved[attribute.Name] = raw;
            }
        }

        return resolved;
    }

    private static int? ParseCommandTimeout(IReadOnlyDictionary<string, string> attributes)
    {
        if (TryReadInt(attributes, "TimeoutSeconds", out var seconds))
        {
            return seconds;
        }

        if (TryReadInt(attributes, "Timeout", out var timeout))
        {
            return timeout;
        }

        if (TryReadInt(attributes, "QueryTimeout", out var queryTimeout))
        {
            return queryTimeout;
        }

        if (TryReadInt(attributes, "TimeoutMs", out var timeoutMs))
        {
            return Math.Max(1, timeoutMs / 1000);
        }

        return null;
    }

    private static bool TryReadInt(IReadOnlyDictionary<string, string> attributes, string key, out int value)
    {
        value = 0;
        if (!attributes.TryGetValue(key, out var raw))
        {
            return false;
        }

        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private bool TryResolveParameter(TestParameters parameters, string attributeName, out string? resolved, out string? error)
    {
        error = null;
        resolved = null;
        var raw = GetParameterAttribute(parameters, attributeName);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        try
        {
            resolved = _evaluator.ResolveText(raw);
            return true;
        }
        catch (UndefinedVariableException ex)
        {
            error = $"{attributeName} verweist auf unbekannte Variable '{ex.Message}'.";
            return false;
        }
    }

    private OdbcConfiguration ResolveOdbcConfiguration()
    {
        var config = new OdbcConfiguration();
        if (_wireVizResolver == null)
        {
            return config;
        }

        foreach (var element in _wireVizResolver.SimulationElements.OfType<UnknownElementDefinition>())
        {
            if (!string.Equals(element.Type, "testsystem", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (element.Metadata.TryGetValue("odbc_mode", out var modeText))
            {
                config.Mode = ParseOdbcMode(modeText);
            }

            if (element.Metadata.TryGetValue("odbc_mock_result", out var mockResult))
            {
                config.MockResult = string.IsNullOrWhiteSpace(mockResult) ? null : mockResult;
            }

            if (element.Metadata.TryGetValue("odbc_timeout_seconds", out var timeoutSeconds) &&
                int.TryParse(timeoutSeconds, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedSeconds))
            {
                config.TimeoutSeconds = parsedSeconds;
            }
            else if (element.Metadata.TryGetValue("odbc_timeout_ms", out var timeoutMs) &&
                     int.TryParse(timeoutMs, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedMs))
            {
                config.TimeoutSeconds = Math.Max(1, parsedMs / 1000);
            }
        }

        return config;
    }

    private static OdbcMode ParseOdbcMode(string? raw)
    {
        var trimmed = raw?.Trim().ToLowerInvariant();
        return trimmed == "mock" ? OdbcMode.Mock : OdbcMode.Real;
    }

    private static string BuildOdbcConnectionString(string? driverName, string? databaseLocation, string? additionalParams)
    {
        var hasDbLocation = !string.IsNullOrWhiteSpace(databaseLocation);
        var hasAdditional = !string.IsNullOrWhiteSpace(additionalParams);
        var hasDriver = !string.IsNullOrWhiteSpace(driverName);

        var cleanedDb = NormalizeQuotedText(databaseLocation);
        var cleanedAdditional = NormalizeQuotedText(additionalParams);
        var cleanedDriver = NormalizeQuotedText(driverName);

        if (!string.IsNullOrWhiteSpace(cleanedDb) && LooksLikeConnectionString(cleanedDb))
        {
            return JoinConnectionString(cleanedDb, cleanedAdditional);
        }

        var segments = new List<string>();
        if (hasDriver && !string.IsNullOrWhiteSpace(cleanedDriver))
        {
            segments.Add($"Driver={{{cleanedDriver}}}");
        }

        if (hasDbLocation && !string.IsNullOrWhiteSpace(cleanedDb))
        {
            segments.Add($"Dbq={cleanedDb}");
        }

        if (hasAdditional && !string.IsNullOrWhiteSpace(cleanedAdditional))
        {
            segments.Add(cleanedAdditional);
        }

        return string.Join(";", segments.Where(segment => !string.IsNullOrWhiteSpace(segment)));
    }

    private static string JoinConnectionString(string baseConnection, string? additional)
    {
        if (string.IsNullOrWhiteSpace(additional))
        {
            return baseConnection;
        }

        var trimmed = baseConnection.Trim().TrimEnd(';');
        return $"{trimmed};{additional}";
    }

    private static bool LooksLikeConnectionString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Contains('=') && value.Contains(';');
    }

    private static OdbcSqlKind DetermineSqlKind(string? statement)
    {
        if (string.IsNullOrWhiteSpace(statement))
        {
            return OdbcSqlKind.NonQuery;
        }

        var trimmed = statement.TrimStart();
        return trimmed.StartsWith("select", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("with", StringComparison.OrdinalIgnoreCase)
            ? OdbcSqlKind.Query
            : OdbcSqlKind.NonQuery;
    }

    private static OdbcExecutionResult ExecuteOdbcNonQuery(OdbcCommand command)
    {
        var affected = command.ExecuteNonQuery();
        return new OdbcExecutionResult($"RowsAffected={affected}", affected);
    }

    private static OdbcExecutionResult ExecuteOdbcQuery(OdbcCommand command)
    {
        using var reader = command.ExecuteReader();
        var rows = new List<string>();
        var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();

        while (reader.Read())
        {
            var parts = new List<string>();
            for (var index = 0; index < reader.FieldCount; index++)
            {
                var value = reader.IsDBNull(index) ? "NULL" : reader.GetValue(index)?.ToString() ?? string.Empty;
                parts.Add($"{columns[index]}={value}");
            }

            rows.Add(string.Join(", ", parts));
        }

        var resultText = $"Rows={rows.Count}" + (rows.Count > 0 ? $" | {string.Join(" | ", rows)}" : string.Empty);
        return new OdbcExecutionResult(resultText, rows.Count);
    }

    private static string BuildOdbcDetails(OdbcInputs inputs, string resultText, string mode, int? rowsAffected)
    {
        var details = new List<string>
        {
            $"Mode={mode}",
            $"Driver={inputs.DriverName}",
            $"Db={inputs.DatabaseLocation}",
            $"Sql={inputs.SqlStatement}"
        };

        if (!string.IsNullOrWhiteSpace(inputs.ConnectionAdditionalParams))
        {
            details.Add($"Params={inputs.ConnectionAdditionalParams}");
        }

        if (rowsAffected.HasValue)
        {
            details.Add($"Rows={rowsAffected.Value}");
        }

        if (inputs.AdditionalAttributes.Count > 0)
        {
            var extra = string.Join(", ", inputs.AdditionalAttributes.Select(item => $"{item.Key}={item.Value}"));
            details.Add($"AllParams={extra}");
        }

        if (!string.IsNullOrWhiteSpace(resultText))
        {
            details.Add($"Result={resultText}");
        }

        return string.Join(" | ", details);
    }

    private sealed class OdbcInputs
    {
        public string? DriverName { get; set; }
        public string? SqlStatement { get; set; }
        public string? DatabaseLocation { get; set; }
        public string? ConnectionAdditionalParams { get; set; }
        public string? ResultVariable { get; set; }
        public Dictionary<string, string> AdditionalAttributes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public int? CommandTimeoutSeconds { get; set; }
    }

    private sealed class OdbcConfiguration
    {
        public OdbcMode Mode { get; set; } = OdbcMode.Real;
        public string? MockResult { get; set; }
        public int? TimeoutSeconds { get; set; }
    }

    private sealed record OdbcExecutionResult(string ResultText, int? RowsAffected);

    private enum OdbcSqlKind
    {
        Query,
        NonQuery
    }

    private enum OdbcMode
    {
        Real,
        Mock
    }
}

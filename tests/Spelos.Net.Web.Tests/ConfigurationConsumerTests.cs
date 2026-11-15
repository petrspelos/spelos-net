using System.Diagnostics;
using System.Text.Json;
using Spelos.Net.Web.Tools;

namespace Spelos.Net.Web.Tests;

public sealed class ConfigurationConsumerTests
{
    private static readonly string[] LiteralValues = ["simple", "", "${MISSING:-oops} $HOME $$ $(throw 'bad')", "'\"#=", " \\path\\file ", "Žluťoučký 🐈", "one\ntwo\r\nthree", "trailing\\", "quote\"and\\", "slash\\'quote", "\\\\", "`literal`\tend", "\u2018smart\u2019 quotes", "slash\\\"quote", "\\${MISSING}\\", "\u0001\u0007\u000b\u001b"];

    [Fact]
    public async Task Compose_reconstructs_literal_values_without_interpolation()
    {
        var input = LiteralValues.Select((value, index) => (value, index)).ToDictionary(item => "Value" + item.index, item => item.value);
        var result = AppSettingsConverter.Convert(JsonSerializer.Serialize(input));
        Assert.Empty(result.Errors);
        var directory = Directory.CreateTempSubdirectory("spelos-compose-");
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory.FullName, "values.env"), result.Output);
            await File.WriteAllTextAsync(Path.Combine(directory.FullName, "compose.yaml"), "services:\n  app:\n    image: scratch\n    env_file: values.env\n");
            var executable = Environment.GetEnvironmentVariable("SPELOS_COMPOSE_PATH");
            var args = executable is null ? new[] { "compose", "-f", "compose.yaml", "config", "--format", "json" } : ["-f", "compose.yaml", "config", "--format", "json"];
            var output = await Run(executable ?? "docker", args, directory.FullName);
            using var model = JsonDocument.Parse(output);
            // Compose config escapes dollars in its serialized model so it can be loaded again without interpolation.
            var environment = model.RootElement.GetProperty("services").GetProperty("app").GetProperty("environment");
            foreach (var entry in input) Assert.Equal(entry.Value.Replace("$", "$$", StringComparison.Ordinal), environment.GetProperty(entry.Key).GetString());
        }
        finally { directory.Delete(true); }
    }

    [Fact]
    public async Task PowerShell_preserves_literals_and_targets_both_scopes_without_touching_real_user_settings()
    {
        var input = LiteralValues.Where(value => value.Length > 0).Select((value, index) => (value, index)).ToDictionary(item => "SpelosTest" + item.index, item => item.value);
        var result = AppSettingsConverter.Convert(JsonSerializer.Serialize(input), format: ConfigurationOutputFormat.PowerShell);
        Assert.Empty(result.Errors);
        var directory = Directory.CreateTempSubdirectory("spelos-powershell-");
        try
        {
            // Substitute only the OS boundary. All generated quoting and commands execute
            // in a child PowerShell process; User writes are captured instead of persisted.
            var script = """
                [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()
                class EnvironmentSink {
                    static [System.Collections.Generic.List[object]] $Calls = @()
                    static [void] SetEnvironmentVariable([string] $name, [string] $value, [string] $scope) {
                        [EnvironmentSink]::Calls.Add(@{Name=$name; Value=$value; Scope=$scope})
                        if ($scope -eq 'Process') { [System.Environment]::SetEnvironmentVariable($name, $value, 'Process') }
                    }
                }
                $env:SpelosUnrelated = 'untouched'
                $env:SpelosTest0 = 'old'
                """ + "\n" + result.Output!.Replace("[System.Environment]", "[EnvironmentSink]", StringComparison.Ordinal)
                    .Replace("if (-not $IsWindows)", "if ($false)", StringComparison.Ordinal) + "\n" + """
                @{Calls=[EnvironmentSink]::Calls; Process=(Get-ChildItem Env:SpelosTest* | ForEach-Object { @{Name=$_.Name; Value=$_.Value} }); Unrelated=$env:SpelosUnrelated} | ConvertTo-Json -Depth 5 -Compress
                """;
            var file = Path.Combine(directory.FullName, "test.ps1");
            await File.WriteAllTextAsync(file, script);
            using var document = JsonDocument.Parse(await Run("pwsh", ["-NoProfile", "-NonInteractive", "-File", file], directory.FullName));
            var root = document.RootElement;
            Assert.Equal("untouched", root.GetProperty("Unrelated").GetString());
            var calls = root.GetProperty("Calls").EnumerateArray().ToArray();
            Assert.Equal(input.Count * 2, calls.Length);
            foreach (var entry in input)
            {
                foreach (var scope in new[] { "User", "Process" })
                    Assert.Equal(entry.Value, Assert.Single(calls, call => call.GetProperty("Name").GetString() == entry.Key && call.GetProperty("Scope").GetString() == scope).GetProperty("Value").GetString());
                Assert.Contains(root.GetProperty("Process").EnumerateArray(), item => item.GetProperty("Name").GetString() == entry.Key && item.GetProperty("Value").GetString() == entry.Value);
            }
        }
        finally { directory.Delete(true); }
    }

    private static async Task<string> Run(string executable, string[] args, string directory)
    {
        var start = new ProcessStartInfo(executable) { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8, StandardErrorEncoding = System.Text.Encoding.UTF8 };
        foreach (var argument in args) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await process.WaitForExitAsync(timeout.Token);
        Assert.True(process.ExitCode == 0, await error);
        return await output;
    }
}

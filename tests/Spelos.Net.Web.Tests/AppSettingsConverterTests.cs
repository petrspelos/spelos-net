using Microsoft.Extensions.Configuration;
using Spelos.Net.Web.Tools;

namespace Spelos.Net.Web.Tests;

public sealed class AppSettingsConverterTests
{
    [Fact]
    public void Converts_appsettings_paths_arrays_and_scalar_text_with_exact_prefix()
    {
        var result = AppSettingsConverter.Convert("""
            { // appsettings accepts comments
              "Options": { "Names": ["Ada", "Lin"], "Enabled": true, "Count": 1.20e2, },
            }
            """, "APP_", ConfigurationOutputFormat.Compose);
        Assert.Empty(result.Errors);
        Assert.Equal("APP_Options__Names__0='Ada'\nAPP_Options__Names__1='Lin'\nAPP_Options__Enabled='true'\nAPP_Options__Count='1.20e2'\n", result.Output);
    }
    [Theory]
    [InlineData("{\"x\":null}", "$[\"x\"]")]
    [InlineData("{\"x\":{}}", "$[\"x\"]")]
    [InlineData("{\"x\":[]}", "$[\"x\"]")]
    [InlineData("{\"x__y\":1}", "$[\"x__y\"]")]
    [InlineData("{\"x:y\":1}", "$[\"x:y\"]")]
    [InlineData("{\"\":1}", "$[\"\"]")]
    [InlineData("{\"x\":1,\"X\":2}", "$[\"X\"]")]
    [InlineData("{\"SQLCONNSTR_main\":\"secret\"}", "$[\"SQLCONNSTR_main\"]")]
    [InlineData("{\"x\":\"\\uD800\"}", "$[\"x\"]")]
    [InlineData("{\"POSTGRESQLCONNSTR_main\":\"secret\"}", "$[\"POSTGRESQLCONNSTR_main\"]")]
    [InlineData("[]", "$")]
    [InlineData("{", "$")]
    public void Rejects_unsupported_configuration_without_partial_output(string json, string path)
    {
        var result = AppSettingsConverter.Convert(json);
        Assert.Null(result.Output);
        Assert.Contains(result.Errors, error => error.Path == path);
    }    [Fact]
    public void Empty_string_is_preserved_for_compose_but_blocked_for_persistent_windows_values()
    {
        Assert.Equal("Value=''\n", AppSettingsConverter.Convert("{\"Value\":\"\"}").Output);
        var result = AppSettingsConverter.Convert("{\"Value\":\"\"}", format: ConfigurationOutputFormat.PowerShell);
        Assert.Null(result.Output);
        Assert.Contains(result.Errors, error => error.Path == "$[\"Value\"]" && error.Message.Contains("delete"));
    }

    [Theory]
    [InlineData("{\"A_\":{\"B\":1}}", "_")]
    [InlineData("{\"_A\":1}", "APP_")]
    [InlineData("{\"A\":1}", " bad ")]
    [InlineData("{\"A\":\"\\u0000\"}", "")]
    [InlineData("{\"A\":1,\"a\":{\"B\":2}}", "")]
    public void Rejects_names_values_and_prefixes_that_change_mapping(string json, string prefix)
    {
        var result = AppSettingsConverter.Convert(json, prefix);
        Assert.Null(result.Output);
        Assert.NotEmpty(result.Errors);
    }    [Fact]
    public void Generated_variables_bind_options_and_merge_individual_array_keys()
    {
        var prefix = "Spelos_" + Guid.NewGuid().ToString("N") + "_";
        var result = AppSettingsConverter.Convert("""{"Options":{"Names":["Ada"],"Enabled":true,"Count":42}}""", prefix);
        Assert.Empty(result.Errors);
        var names = new List<string>();
        try
        {
            foreach (var line in result.Output!.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split('=', 2);
                names.Add(parts[0]);
                Environment.SetEnvironmentVariable(parts[0], parts[1][1..^1]);
            }
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Options:Names:0"] = "old", ["Options:Names:1"] = "retained" })
                .AddEnvironmentVariables(prefix).Build();
            var options = configuration.GetSection("Options").Get<ExampleOptions>()!;
            Assert.Equal(["Ada", "retained"], options.Names);
            Assert.True(options.Enabled);
            Assert.Equal(42, options.Count);
            Assert.Null(configuration[prefix + "Options:Count"]);
        }
        finally { foreach (var name in names) Environment.SetEnvironmentVariable(name, null); }
    }

    public sealed class ExampleOptions
    {
        public string[] Names { get; set; } = [];
        public bool Enabled { get; set; }
        public int Count { get; set; }
    }}

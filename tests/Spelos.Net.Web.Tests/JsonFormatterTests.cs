using Spelos.Net.Web.Tools;

namespace Spelos.Net.Web.Tests;

public sealed class JsonFormatterTests
{
    [Fact]
    public void Formats_with_two_space_indentation_by_default()
    {
        var result = JsonFormatter.Format("""{"name":"Ada","tags":["a","b"],"nested":{"on":true}}""");

        Assert.Null(result.Error);
        Assert.Equal("{\n  \"name\": \"Ada\",\n  \"tags\": [\n    \"a\",\n    \"b\"\n  ],\n  \"nested\": {\n    \"on\": true\n  }\n}", result.Output);
    }

    [Theory]
    [InlineData(JsonOutputFormat.Indented, JsonIndentStyle.Spaces, 4, "{\n    \"a\": [\n        1\n    ]\n}")]
    [InlineData(JsonOutputFormat.Indented, JsonIndentStyle.Spaces, 1, "{\n \"a\": [\n  1\n ]\n}")]
    [InlineData(JsonOutputFormat.Indented, JsonIndentStyle.Tabs, 4, "{\n\t\"a\": [\n\t\t1\n\t]\n}")]
    [InlineData(JsonOutputFormat.Minified, JsonIndentStyle.Spaces, 2, "{\"a\":[1]}")]
    public void Applies_indent_style_size_and_minified_output(JsonOutputFormat format, JsonIndentStyle style, int size, string expected)
    {
        var result = JsonFormatter.Format("{ \"a\" : [ 1 ] }", new(format, style, size));

        Assert.Equal(expected, result.Output);
    }

    [Fact]
    public void Keeps_empty_containers_inline_and_expands_non_empty_ones()
    {
        var result = JsonFormatter.Format("[ { }, [ ], [ { } ] ]");

        Assert.Equal("[\n  {},\n  [],\n  [\n    {}\n  ]\n]", result.Output);
    }

    [Fact]
    public void Preserves_number_literals_exactly()
    {
        var result = JsonFormatter.Format("[1.0, 1e10, -0.50E-3, 123456789012345678901234567890]", new(JsonOutputFormat.Minified));

        Assert.Equal("[1.0,1e10,-0.50E-3,123456789012345678901234567890]", result.Output);
    }

    [Fact]
    public void Preserves_strings_and_escapes_verbatim()
    {
        const string input = """{"café":"café ✓ <b>&amp;</b>","esc":"\"\\\/\b\f\n\r\t✓"}""";

        var result = JsonFormatter.Format(input, new(JsonOutputFormat.Minified));

        Assert.Equal(input, result.Output);
    }

    [Fact]
    public void Preserves_key_order_and_duplicate_keys()
    {
        var result = JsonFormatter.Format("""{"z":1,"a":2,"z":3}""", new(JsonOutputFormat.Minified));

        Assert.Equal("""{"z":1,"a":2,"z":3}""", result.Output);
    }

    [Fact]
    public void Rejects_unpaired_surrogates_instead_of_silently_replacing_them()
    {
        var result = JsonFormatter.Format("[\"a" + (char)0xD800 + "\"]");

        Assert.Equal("", result.Output);
        Assert.Equal(new JsonFormatError(1, 4, "Invalid character U+D800. The text contains a broken Unicode surrogate pair."), result.Error);
    }

    [Fact]
    public void Formats_deeply_nested_input_beyond_the_default_reader_depth()
    {
        var input = new string('[', 1000) + new string(']', 1000);

        var result = JsonFormatter.Format(input, new(JsonOutputFormat.Minified));

        Assert.Equal(input, result.Output);
    }

    [Fact]
    public void Accepts_and_removes_comments_trailing_commas_and_a_byte_order_mark()
    {
        var result = JsonFormatter.Format("\uFEFF// leading\n{ /* inline */ \"a\": [1, 2,], // after\n\"b\": 3, }\n");

        Assert.Null(result.Error);
        Assert.Equal("{\n  \"a\": [\n    1,\n    2\n  ],\n  \"b\": 3\n}", result.Output);
    }

    [Fact]
    public void Uses_lf_line_endings_without_a_trailing_newline()
    {
        var result = JsonFormatter.Format("{\r\n\"a\": 1\r\n}\r\n");

        Assert.Equal("{\n  \"a\": 1\n}", result.Output);
    }

    [Theory]
    [InlineData("{'a': 1}", 1, 2, "Property names must be in double quotes.")]
    [InlineData("{\n  a: 1\n}", 2, 3, "Property names must be in double quotes.")]
    [InlineData("['a']", 1, 2, "Strings must be in double quotes.")]
    public void Rejects_single_quotes_and_unquoted_keys_without_partial_output(string input, int line, int column, string message)
    {
        var result = JsonFormatter.Format(input);

        Assert.Equal("", result.Output);
        Assert.Equal(new JsonFormatError(line, column, message), result.Error);
    }

    [Fact]
    public void Reports_error_columns_in_characters_after_non_ascii_text()
    {
        var result = JsonFormatter.Format("{\n  \"café ✓ 😀\": 1 x\n}");

        // Columns count Unicode characters (code points): the emoji is one column, not two UTF-16 units or four UTF-8 bytes.
        Assert.Equal(new JsonFormatError(2, 17, "Expected ',' or '}' after a value, but found 'x'."), result.Error);
    }

    [Theory]
    [InlineData("{\"a\" 1}", 1, 6, "Expected ':' after the property name, but found '1'.")]
    [InlineData("{\"a\":1 \"b\":2}", 1, 8, "Expected ',' or '}' after a value, but found '\"'.")]
    [InlineData("[1,,2]", 1, 4, "Expected a value, but found ','.")]
    [InlineData("{\"a\":}", 1, 6, "Expected a value, but found '}'.")]
    [InlineData("[1}", 1, 3, "Found '}', but the open array must be closed with ']'.")]
    [InlineData("{\"a\":1}}", 1, 8, "Unexpected '}' after the end of the JSON value. Only one top-level value is allowed.")]
    [InlineData("{\n  \"a\": [1,\n    2", 3, 6, "Unexpected end of input. The array opened at line 2, column 8 is not closed.")]
    [InlineData("{\"a\":\"abc", 1, 10, "A string is not closed. Add the closing double quote.")]
    [InlineData("\"a\nb\"", 1, 3, "Strings cannot contain line breaks. Use \\n instead.")]
    [InlineData("\"\\q\"", 1, 3, "Invalid escape sequence \\q in a string.")]
    [InlineData("01", 1, 2, "Numbers cannot have leading zeros.")]
    [InlineData("[1.]", 1, 4, "Invalid number '1.'.")]
    [InlineData("[tru]", 1, 5, "Invalid literal 'tru'. Did you mean 'true'?")]
    [InlineData("// only a comment", 1, 18, "No JSON value found. Comments alone are not JSON.")]
    [InlineData("[1] /* open", 1, 5, "A comment is not closed. Add */ to end it.")]
    [InlineData("[True]", 1, 2, "JSON literals are lowercase. Use 'true'.")]
    [InlineData("{\"a\": NULL}", 1, 7, "JSON literals are lowercase. Use 'null'.")]
    public void Explains_invalid_json_with_line_column_and_a_friendly_message(string input, int line, int column, string message)
    {
        var result = JsonFormatter.Format(input);

        Assert.Equal("", result.Output);
        Assert.Equal(new JsonFormatError(line, column, message), result.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  \r\n\t ")]
    public void Empty_or_whitespace_input_gives_an_empty_result_without_an_error(string input)
    {
        var result = JsonFormatter.Format(input);

        Assert.Equal("", result.Output);
        Assert.Null(result.Error);
    }
}

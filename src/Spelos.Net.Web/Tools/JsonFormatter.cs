using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Spelos.Net.Web.Tools;

public enum JsonOutputFormat { Indented, Minified }
public enum JsonIndentStyle { Spaces, Tabs }
public sealed record JsonFormatOptions(JsonOutputFormat Format = JsonOutputFormat.Indented, JsonIndentStyle IndentStyle = JsonIndentStyle.Spaces, int IndentSize = JsonFormatOptions.DefaultIndentSize)
{
    public const int MinIndentSize = 1, MaxIndentSize = 8, DefaultIndentSize = 2;
}
public sealed record JsonFormatError(int Line, int Column, string Message);
public sealed record JsonFormatResult(string Output, JsonFormatError? Error);

/// <summary>
/// Reformats JSON by changing whitespace only: every token is copied from its raw source text, so numbers,
/// string escapes, key order and duplicate keys survive exactly as written.
/// </summary>
public static class JsonFormatter
{
    private const char ByteOrderMark = (char)0xFEFF;

    public static JsonFormatResult Format(string input, JsonFormatOptions? options = null)
    {
        options ??= new();
        var text = input.Length > 0 && input[0] == ByteOrderMark ? input[1..] : input;
        if (string.IsNullOrWhiteSpace(text)) return new("", null);
        // UTF-8 encoding would silently turn an unpaired surrogate into U+FFFD and change a value.
        if (UnpairedSurrogate(text) is { } surrogate)
            return new("", ErrorDiagnosis.At(text, surrogate, $"Invalid character U+{(int)text[surrogate]:X4}. The text contains a broken Unicode surrogate pair."));
        var bytes = Encoding.UTF8.GetBytes(text);
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true, MaxDepth = int.MaxValue });
        var writer = new Writer(options);
        try
        {
            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.StartObject: writer.Open('{'); break;
                    case JsonTokenType.StartArray: writer.Open('['); break;
                    case JsonTokenType.EndObject: writer.Close('}'); break;
                    case JsonTokenType.EndArray: writer.Close(']'); break;
                    case JsonTokenType.PropertyName: writer.Property(Raw(reader)); break;
                    case JsonTokenType.String: writer.Value('"' + Raw(reader) + '"'); break;
                    default: writer.Value(Raw(reader)); break;
                }
            }
        }
        catch (JsonException exception)
        {
            return new("", ErrorDiagnosis.Diagnose(text, CharOffset(bytes, exception)));
        }
        return new(writer.ToString(), null);
    }

    private static int? UnpairedSurrogate(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1])) i++;
            else if (char.IsSurrogate(text[i])) return i;
        }
        return null;
    }

    private static string Raw(Utf8JsonReader reader) => Encoding.UTF8.GetString(reader.ValueSpan);

    // The reader reports a zero-based line and a UTF-8 byte position within it; convert to a UTF-16 offset into the text.
    private static int CharOffset(byte[] bytes, JsonException exception)
    {
        var lineStart = 0;
        for (var line = 0L; line < (exception.LineNumber ?? 0) && lineStart < bytes.Length; line++)
        {
            var next = Array.IndexOf(bytes, (byte)'\n', lineStart);
            if (next < 0) { lineStart = bytes.Length; break; }
            lineStart = next + 1;
        }
        var byteOffset = (int)Math.Min(bytes.Length, lineStart + (exception.BytePositionInLine ?? 0));
        return Encoding.UTF8.GetCharCount(bytes, 0, byteOffset);
    }

    private sealed class Writer(JsonFormatOptions options)
    {
        private readonly StringBuilder _output = new();
        private readonly Stack<bool> _hasItems = new();
        private readonly bool _minified = options.Format == JsonOutputFormat.Minified;
        private readonly int _indentSize = Math.Clamp(options.IndentSize, JsonFormatOptions.MinIndentSize, JsonFormatOptions.MaxIndentSize);
        private bool _afterProperty;

        public void Open(char bracket) { BeforeValue(); _output.Append(bracket); _hasItems.Push(false); }

        public void Close(char bracket)
        {
            if (_hasItems.Pop()) NewLine();
            _output.Append(bracket);
        }

        public void Property(string name)
        {
            BeforeValue();
            _output.Append('"').Append(name).Append(_minified ? "\":" : "\": ");
            _afterProperty = true;
        }

        public void Value(string raw) { BeforeValue(); _output.Append(raw); }

        private void BeforeValue()
        {
            if (_afterProperty) { _afterProperty = false; return; }
            if (_hasItems.Count == 0) return;
            if (_hasItems.Pop()) _output.Append(',');
            _hasItems.Push(true);
            NewLine();
        }

        private void NewLine()
        {
            if (_minified) return;
            _output.Append('\n');
            if (options.IndentStyle == JsonIndentStyle.Tabs) _output.Append('\t', _hasItems.Count);
            else _output.Append(' ', _hasItems.Count * _indentSize);
        }

        public override string ToString() => _output.ToString();
    }

    /// <summary>
    /// Explains a reader failure from the input itself. Published WebAssembly builds replace System.Text.Json's
    /// English exception messages with resource keys, so the messages cannot be rewritten from exception text.
    /// </summary>
    private static class ErrorDiagnosis
    {
        private static readonly Regex NumberPattern = new(@"\A-?(0|[1-9][0-9]*)(\.[0-9]+)?([eE][+-]?[0-9]+)?\z");
        private static readonly Regex LeadingZeroPattern = new(@"\A-?0[0-9]");

        public static JsonFormatError Diagnose(string text, int offset) => At(text, offset, Message(text, offset, Scan(text, offset)));

        public static JsonFormatError At(string text, int offset, string message)
        {
            var (line, column) = Position(text, offset);
            return new(line, column, message);
        }

        private static string Message(string text, int offset, ScanState scan)
        {
            var atEnd = offset >= text.Length;
            if (scan.InString)
            {
                if (atEnd) return "A string is not closed. Add the closing double quote.";
                if (scan.UnicodeDigitsRemaining > 0) return "Invalid \\u escape. Use four hexadecimal digits, for example \\u00e9.";
                if (scan.Escaped) return $"Invalid escape sequence \\{DescribeCharacter(text, offset, quoted: false)} in a string.";
                if (text[offset] is '\n' or '\r') return "Strings cannot contain line breaks. Use \\n instead.";
                if (text[offset] < ' ') return "Control characters in strings must be escaped.";
                return "Invalid string.";
            }

            if (scan.WordStart is { } wordStart)
            {
                var word = Word(text, wordStart);
                var isNumber = word[0] is '-' or (>= '0' and <= '9');
                var valid = isNumber ? NumberPattern.IsMatch(word) : word is "true" or "false" or "null";
                if (!valid && isNumber) return LeadingZeroPattern.IsMatch(word) ? "Numbers cannot have leading zeros." : $"Invalid number '{word}'.";
                if (!valid)
                {
                    var literal = word[0] switch { 't' => "true", 'f' => "false", _ => "null" };
                    return $"Invalid literal '{word}'. Did you mean '{literal}'?";
                }
            }

            if (atEnd)
            {
                if (scan.InBlockComment) return "A comment is not closed. Add */ to end it.";
                if (scan.Open.Count > 0)
                {
                    var (bracket, at) = scan.Open.Peek();
                    var (openLine, openColumn) = Position(text, at);
                    return $"Unexpected end of input. The {(bracket == '{' ? "object" : "array")} opened at line {openLine}, column {openColumn} is not closed.";
                }
                return scan.LastSignificant is null ? "No JSON value found. Comments alone are not JSON." : "Unexpected end of input.";
            }

            var found = DescribeCharacter(text, offset, quoted: true);
            var c = text[offset];
            if (c == '/') return offset + 1 < text.Length && text[offset + 1] == '*' ? "A comment is not closed. Add */ to end it." : "Invalid comment. Use // or /* */.";

            var top = scan.Open.Count > 0 ? scan.Open.Peek().Bracket : (char?)null;
            var last = scan.LastSignificant;
            var expectsName = top == '{' && last is '{' or ',';
            var expectsColon = top == '{' && last == '"' && scan.LastStringWasName;
            var expectsValue = last is null or ':' || top == '[' && last is '[' or ',';
            var quotedLike = c == '\'' || char.IsLetter(c) || c is '_' or '$';

            if (expectsName) return quotedLike ? "Property names must be in double quotes." : $"Expected a property name in double quotes, but found {found}.";
            if (expectsColon) return $"Expected ':' after the property name, but found {found}.";
            var lowercased = Word(text, offset).ToLowerInvariant();
            if (expectsValue && lowercased is "true" or "false" or "null") return $"JSON literals are lowercase. Use '{lowercased}'.";
            if (expectsValue) return quotedLike ? "Strings must be in double quotes." : $"Expected a value, but found {found}.";
            if (top is null) return $"Unexpected {found} after the end of the JSON value. Only one top-level value is allowed.";
            var close = top == '{' ? '}' : ']';
            if (c is '}' or ']' && c != close) return $"Found {found}, but the open {(top == '{' ? "object" : "array")} must be closed with '{close}'.";
            return $"Expected ',' or '{close}' after a value, but found {found}.";
        }

        private static string DescribeCharacter(string text, int offset, bool quoted)
        {
            var c = text[offset];
            if (char.IsHighSurrogate(c) && offset + 1 < text.Length) return quoted ? $"'{text.Substring(offset, 2)}'" : text.Substring(offset, 2);
            if (char.IsControl(c) || char.IsWhiteSpace(c)) return $"U+{(int)c:X4}";
            return quoted ? $"'{c}'" : c.ToString();
        }

        private static string Word(string text, int start)
        {
            var end = start;
            while (end < text.Length && IsWordChar(text[end])) end++;
            return text[start..end];
        }

        private static bool IsWordChar(char c) => char.IsAsciiLetterOrDigit(c) || c is '+' or '-' or '.';

        private static (int Line, int Column) Position(string text, int offset)
        {
            var lineStart = offset == 0 ? 0 : text.LastIndexOf('\n', offset - 1) + 1;
            var line = 1;
            for (var i = 0; i < lineStart; i++) if (text[i] == '\n') line++;
            var column = 1;
            for (var i = lineStart; i < Math.Min(offset, text.Length); i++) if (!char.IsLowSurrogate(text[i])) column++;
            return (line, column);
        }

        // Replays the lexical structure before the error. Everything before the reported position was accepted
        // by the reader, so brackets, strings and comments seen here are reliable.
        private static ScanState Scan(string text, int offset)
        {
            var state = new ScanState();
            char? beforeString = null;
            var blockStart = -1;
            for (var i = 0; i < Math.Min(offset, text.Length); i++)
            {
                var c = text[i];
                if (state.InString)
                {
                    if (state.UnicodeDigitsRemaining > 0) state.UnicodeDigitsRemaining--;
                    else if (state.Escaped) { state.Escaped = false; if (c == 'u') state.UnicodeDigitsRemaining = 4; }
                    else if (c == '\\') state.Escaped = true;
                    else if (c == '"')
                    {
                        state.InString = false;
                        state.LastStringWasName = state.Open.Count > 0 && state.Open.Peek().Bracket == '{' && beforeString is '{' or ',';
                        state.LastSignificant = '"';
                    }
                    continue;
                }
                if (state.InLineComment) { if (c == '\n') state.InLineComment = false; continue; }
                if (state.InBlockComment) { if (c == '/' && text[i - 1] == '*' && i - 1 > blockStart) state.InBlockComment = false; continue; }
                if (c == '/' && i + 1 < text.Length && text[i + 1] is '/' or '*')
                {
                    state.WordStart = null;
                    if (text[i + 1] == '/') state.InLineComment = true; else { state.InBlockComment = true; blockStart = i + 1; }
                    i++;
                    continue;
                }
                if (IsWordChar(c)) { state.WordStart ??= i; state.LastSignificant = c; continue; }
                state.WordStart = null;
                if (char.IsWhiteSpace(c)) continue;
                if (c == '"') { state.InString = true; beforeString = state.LastSignificant; continue; }
                if (c is '{' or '[') state.Open.Push((c, i));
                else if (c is '}' or ']' && state.Open.Count > 0) state.Open.Pop();
                state.LastSignificant = c;
            }
            return state;
        }

        private sealed class ScanState
        {
            public Stack<(char Bracket, int Offset)> Open { get; } = new();
            public char? LastSignificant { get; set; }
            public bool LastStringWasName { get; set; }
            public bool InString { get; set; }
            public bool Escaped { get; set; }
            public int UnicodeDigitsRemaining { get; set; }
            public bool InLineComment { get; set; }
            public bool InBlockComment { get; set; }
            public int? WordStart { get; set; }
        }
    }
}

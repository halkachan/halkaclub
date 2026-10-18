using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Halka.Poc2D3D.Save
{
    // A small strict JSON reader for save files: objects (Dictionary), arrays (List), strings,
    // integers (long, exact), other numbers (double), true/false/null. Data only: nothing in a
    // file is ever run. Depth and length are limited; any syntax error returns false.
    public static class HarukaSaveJson
    {
        private const int MaxDepth = 32;

        public static bool TryParse(string text, out object value, out string error)
        {
            value = null;
            error = null;
            if (text == null) { error = "no text"; return false; }
            var reader = new Reader(text);
            try
            {
                reader.SkipSpace();
                value = reader.ReadValue(0);
                reader.SkipSpace();
                if (!reader.AtEnd) throw new FormatException("text after the value");
                return true;
            }
            catch (FormatException exception)
            {
                error = exception.Message + " at " + reader.Position.ToString(CultureInfo.InvariantCulture);
                value = null;
                return false;
            }
        }

        public static string Quote(string text)
        {
            var builder = new StringBuilder(text.Length + 2);
            builder.Append('"');
            foreach (var c in text)
            {
                switch (c)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (c < 0x20) builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else builder.Append(c);
                        break;
                }
            }
            builder.Append('"');
            return builder.ToString();
        }

        private sealed class Reader
        {
            private readonly string text;
            private int at;

            public Reader(string source) { text = source; }
            public int Position => at;
            public bool AtEnd => at >= text.Length;

            public void SkipSpace()
            {
                while (at < text.Length && (text[at] == ' ' || text[at] == '\t' || text[at] == '\n' || text[at] == '\r')) at++;
                // A byte order mark at the start of a file is allowed.
                if (at == 0 && text.Length > 0 && text[0] == '﻿') { at = 1; SkipSpace(); }
            }

            public object ReadValue(int depth)
            {
                if (depth > MaxDepth) throw new FormatException("too deep");
                if (AtEnd) throw new FormatException("unexpected end");
                var c = text[at];
                if (c == '{') return ReadObject(depth);
                if (c == '[') return ReadArray(depth);
                if (c == '"') return ReadString();
                if (c == 't') { Expect("true"); return true; }
                if (c == 'f') { Expect("false"); return false; }
                if (c == 'n') { Expect("null"); return null; }
                if (c == '-' || (c >= '0' && c <= '9')) return ReadNumber();
                throw new FormatException("unexpected character");
            }

            private void Expect(string word)
            {
                if (string.CompareOrdinal(text, at, word, 0, word.Length) != 0) throw new FormatException("bad literal");
                at += word.Length;
            }

            private Dictionary<string, object> ReadObject(int depth)
            {
                var result = new Dictionary<string, object>(StringComparer.Ordinal);
                at++;
                SkipSpace();
                if (!AtEnd && text[at] == '}') { at++; return result; }
                while (true)
                {
                    SkipSpace();
                    if (AtEnd || text[at] != '"') throw new FormatException("object key expected");
                    var key = ReadString();
                    SkipSpace();
                    if (AtEnd || text[at] != ':') throw new FormatException("':' expected");
                    at++;
                    SkipSpace();
                    if (result.ContainsKey(key)) throw new FormatException("duplicate key " + key);
                    result[key] = ReadValue(depth + 1);
                    SkipSpace();
                    if (AtEnd) throw new FormatException("unexpected end");
                    if (text[at] == ',') { at++; continue; }
                    if (text[at] == '}') { at++; return result; }
                    throw new FormatException("',' or '}' expected");
                }
            }

            private List<object> ReadArray(int depth)
            {
                var result = new List<object>();
                at++;
                SkipSpace();
                if (!AtEnd && text[at] == ']') { at++; return result; }
                while (true)
                {
                    SkipSpace();
                    result.Add(ReadValue(depth + 1));
                    SkipSpace();
                    if (AtEnd) throw new FormatException("unexpected end");
                    if (text[at] == ',') { at++; continue; }
                    if (text[at] == ']') { at++; return result; }
                    throw new FormatException("',' or ']' expected");
                }
            }

            private string ReadString()
            {
                at++;
                var builder = new StringBuilder();
                while (true)
                {
                    if (AtEnd) throw new FormatException("unterminated string");
                    var c = text[at++];
                    if (c == '"') return builder.ToString();
                    if (c < 0x20) throw new FormatException("control character in string");
                    if (c != '\\') { builder.Append(c); continue; }
                    if (AtEnd) throw new FormatException("bad escape");
                    var e = text[at++];
                    switch (e)
                    {
                        case '"': builder.Append('"'); break;
                        case '\\': builder.Append('\\'); break;
                        case '/': builder.Append('/'); break;
                        case 'b': builder.Append('\b'); break;
                        case 'f': builder.Append('\f'); break;
                        case 'n': builder.Append('\n'); break;
                        case 'r': builder.Append('\r'); break;
                        case 't': builder.Append('\t'); break;
                        case 'u':
                            if (at + 4 > text.Length ||
                                !int.TryParse(text.Substring(at, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
                                throw new FormatException("bad \\u escape");
                            builder.Append((char)code);
                            at += 4;
                            break;
                        default: throw new FormatException("bad escape");
                    }
                }
            }

            private object ReadNumber()
            {
                var start = at;
                if (text[at] == '-') at++;
                while (!AtEnd && char.IsDigit(text[at])) at++;
                var integer = true;
                if (!AtEnd && text[at] == '.') { integer = false; at++; while (!AtEnd && char.IsDigit(text[at])) at++; }
                if (!AtEnd && (text[at] == 'e' || text[at] == 'E'))
                {
                    integer = false;
                    at++;
                    if (!AtEnd && (text[at] == '+' || text[at] == '-')) at++;
                    while (!AtEnd && char.IsDigit(text[at])) at++;
                }
                var token = text.Substring(start, at - start);
                if (token == "-" || token.Length == 0) throw new FormatException("bad number");
                if (integer && long.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var whole))
                    return whole;
                if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var real)) return real;
                throw new FormatException("bad number");
            }
        }
    }
}

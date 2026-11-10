using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace NOVR.VrUi.Controllers;

// A small reader for SteamVR's own description files, which are JSON with the
// occasional comment or trailing comma. Objects become dictionaries, arrays
// lists, numbers doubles. Depth and size are bounded: the input is a file on
// the user's disk, not something this mod wrote.
internal static class MiniJson
{
    private const int MaxDepth = 32, MaxLength = 4 * 1024 * 1024;

    internal static object? Parse(string text)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text));
        if (text.Length > MaxLength)
            throw new FormatException("JSON text is too large.");
        var index = 0;
        var value = Value(text, ref index, 0);
        Skip(text, ref index);
        if (index != text.Length)
            throw new FormatException("Unexpected text after the JSON value.");
        return value;
    }

    private static void Skip(string s, ref int i)
    {
        while (i < s.Length)
        {
            var c = s[i];
            if (char.IsWhiteSpace(c) || c == '﻿')
                i++;
            else if (c == '/' && i + 1 < s.Length && s[i + 1] == '/')
            {
                while (i < s.Length && s[i] != '\n')
                    i++;
            }
            else if (c == '/' && i + 1 < s.Length && s[i + 1] == '*')
            {
                var end = s.IndexOf("*/", i + 2, StringComparison.Ordinal);
                if (end < 0)
                    throw new FormatException("Unterminated comment.");
                i = end + 2;
            }
            else
                break;
        }
    }

    private static object? Value(string s, ref int i, int depth)
    {
        if (depth > MaxDepth)
            throw new FormatException("JSON is nested too deeply.");
        Skip(s, ref i);
        if (i >= s.Length)
            throw new FormatException("Unexpected end of JSON.");
        var c = s[i];
        if (c == '{')
        {
            i++;
            var map = new Dictionary<string, object?>(StringComparer.Ordinal);
            while (true)
            {
                Skip(s, ref i);
                if (i < s.Length && s[i] == '}')
                {
                    i++;
                    return map;
                }
                var key = Text(s, ref i);
                Skip(s, ref i);
                if (i >= s.Length || s[i] != ':')
                    throw new FormatException("Expected ':' after an object key.");
                i++;
                map[key] = Value(s, ref i, depth + 1);
                Skip(s, ref i);
                if (i < s.Length && s[i] == ',')
                    i++;
                else if (i >= s.Length || s[i] != '}')
                    throw new FormatException("Expected ',' or '}' in an object.");
            }
        }
        if (c == '[')
        {
            i++;
            var list = new List<object?>();
            while (true)
            {
                Skip(s, ref i);
                if (i < s.Length && s[i] == ']')
                {
                    i++;
                    return list;
                }
                list.Add(Value(s, ref i, depth + 1));
                Skip(s, ref i);
                if (i < s.Length && s[i] == ',')
                    i++;
                else if (i >= s.Length || s[i] != ']')
                    throw new FormatException("Expected ',' or ']' in an array.");
            }
        }
        if (c == '"')
            return Text(s, ref i);
        var start = i;
        while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '-' || s[i] == '+' || s[i] == '.'))
            i++;
        var token = s.Substring(start, i - start);
        if (token == "true")
            return true;
        if (token == "false")
            return false;
        if (token == "null")
            return null;
        if (token.Length > 0 && double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) &&
            !double.IsNaN(number) && !double.IsInfinity(number))
            return number;
        throw new FormatException("Unexpected JSON token.");
    }

    private static string Text(string s, ref int i)
    {
        if (i >= s.Length || s[i] != '"')
            throw new FormatException("Expected a string.");
        i++;
        var builder = new StringBuilder();
        while (i < s.Length)
        {
            var c = s[i++];
            if (c == '"')
                return builder.ToString();
            if (c != '\\')
            {
                builder.Append(c);
                continue;
            }
            if (i >= s.Length)
                break;
            var e = s[i++];
            switch (e)
            {
                case 'n': builder.Append('\n'); break;
                case 't': builder.Append('\t'); break;
                case 'r': builder.Append('\r'); break;
                case 'b': builder.Append('\b'); break;
                case 'f': builder.Append('\f'); break;
                case 'u':
                    if (i + 4 > s.Length || !ushort.TryParse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
                        throw new FormatException("Bad unicode escape.");
                    builder.Append((char)code);
                    i += 4;
                    break;
                default: builder.Append(e); break;
            }
        }
        throw new FormatException("Unterminated string.");
    }
}

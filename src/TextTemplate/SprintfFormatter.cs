using System;
using System.Text;
using System.Globalization;

namespace TextTemplate;

public static class SprintfFormatter
{
    private class FormatSpecifier
    {
        public string Flags = string.Empty;
        public int? Width;
        public int? Precision;
        public char Specifier;
    }

    public static string Format(string format, params object[] args)
    {
        if (format == null) throw new ArgumentNullException(nameof(format));
        args ??= Array.Empty<object>();
        var result = new StringBuilder();
        int argIndex = 0;
        for (int i = 0; i < format.Length; i++)
        {
            char c = format[i];
            if (c != '%')
            {
                result.Append(c);
                continue;
            }
            if (i + 1 < format.Length && format[i + 1] == '%')
            {
                result.Append('%');
                i++;
                continue;
            }

            var spec = ParseSpecifier(format, ref i);
            if (spec.Specifier == '%')
            {
                result.Append('%');
                continue;
            }

            if (argIndex >= args.Length)
                throw new ArgumentException("Missing argument for format specifier");
            object arg = args[argIndex++];
            result.Append(FormatArgument(spec, arg));
        }
        return result.ToString();
    }

    private static FormatSpecifier ParseSpecifier(string format, ref int index)
    {
        int i = index + 1; // skip '%'
        var spec = new FormatSpecifier();
        // flags
        while (i < format.Length && "-+ #0".IndexOf(format[i]) >= 0)
        {
            spec.Flags += format[i];
            i++;
        }
        // width
        string widthStr = string.Empty;
        while (i < format.Length && char.IsDigit(format[i]))
        {
            widthStr += format[i];
            i++;
        }
        if (widthStr.Length > 0)
            spec.Width = int.Parse(widthStr);
        // precision
        if (i < format.Length && format[i] == '.')
        {
            i++;
            string precStr = string.Empty;
            while (i < format.Length && char.IsDigit(format[i]))
            {
                precStr += format[i];
                i++;
            }
            spec.Precision = precStr.Length > 0 ? int.Parse(precStr) : 0;
        }
        // length modifiers - ignore single l or h
        if (i < format.Length && (format[i] == 'l' || format[i] == 'h'))
        {
            i++;
        }
        if (i >= format.Length)
            throw new FormatException("Invalid format string");
        spec.Specifier = format[i];
        index = i;
        return spec;
    }

    private static string FormatArgument(FormatSpecifier spec, object? arg)
    {
        bool left = spec.Flags.Contains('-');
        bool plus = spec.Flags.Contains('+');
        bool space = spec.Flags.Contains(' ') && !plus;
        bool zero = spec.Flags.Contains('0') && !left;
        bool alt = spec.Flags.Contains('#');
        string prefix = string.Empty;
        string digits;
        switch (spec.Specifier)
        {
            case 'd':
            case 'i':
                long val;
                try
                {
                    val = Convert.ToInt64(arg ?? 0, CultureInfo.InvariantCulture);
                }
                catch (Exception ex)
                {
                    throw new ArgumentException("Invalid integer argument", ex);
                }
                bool neg = val < 0;
                ulong absVal = (ulong)(neg ? -val : val);
                digits = absVal.ToString(spec.Precision.HasValue ? $"D{spec.Precision}" : "D", CultureInfo.InvariantCulture);
                if (neg)
                    prefix = "-";
                else if (plus)
                    prefix = "+";
                else if (space)
                    prefix = " ";
                return ApplyWidth(prefix + digits, spec.Width, left, zero && !spec.Precision.HasValue, prefix.Length);
            case 'u':
                ulong uval;
                try
                {
                    uval = Convert.ToUInt64(arg ?? 0, CultureInfo.InvariantCulture);
                }
                catch (Exception ex)
                {
                    throw new ArgumentException("Invalid integer argument", ex);
                }
                digits = uval.ToString(spec.Precision.HasValue ? $"D{spec.Precision}" : "D", CultureInfo.InvariantCulture);
                if (plus)
                    prefix = "+";
                else if (space)
                    prefix = " ";
                return ApplyWidth(prefix + digits, spec.Width, left, zero && !spec.Precision.HasValue, prefix.Length);
            case 'o':
                ulong oval;
                try
                {
                    oval = Convert.ToUInt64(arg ?? 0, CultureInfo.InvariantCulture);
                }
                catch (Exception ex)
                {
                    throw new ArgumentException("Invalid integer argument", ex);
                }
                digits = Convert.ToString((long)oval, 8);
                if (spec.Precision.HasValue) digits = digits.PadLeft(spec.Precision.Value, '0');
                if (alt && oval != 0) prefix = "0";
                if (plus) prefix = "+" + prefix; else if (space) prefix = " " + prefix;
                return ApplyWidth(prefix + digits, spec.Width, left, zero && !spec.Precision.HasValue, prefix.Length);
            case 'x':
            case 'X':
                ulong xval;
                try
                {
                    xval = Convert.ToUInt64(arg ?? 0, CultureInfo.InvariantCulture);
                }
                catch (Exception ex)
                {
                    throw new ArgumentException("Invalid integer argument", ex);
                }
                digits = xval.ToString(spec.Specifier == 'x' ? "x" : "X", CultureInfo.InvariantCulture);
                if (spec.Precision.HasValue) digits = digits.PadLeft(spec.Precision.Value, '0');
                if (alt && xval != 0) prefix = spec.Specifier == 'x' ? "0x" : "0X";
                if (plus) prefix = "+" + prefix; else if (space) prefix = " " + prefix;
                return ApplyWidth(prefix + digits, spec.Width, left, zero && !spec.Precision.HasValue, prefix.Length);
            case 'f':
            case 'F':
            case 'e':
            case 'E':
            case 'g':
            case 'G':
                double d;
                try
                {
                    d = Convert.ToDouble(arg ?? 0, CultureInfo.InvariantCulture);
                }
                catch (Exception ex)
                {
                    throw new ArgumentException("Invalid floating point argument", ex);
                }
                int prec = spec.Precision ?? 6;
                string fmt = spec.Specifier switch
                {
                    'f' or 'F' => "F" + prec,
                    'e' => "e" + prec,
                    'E' => "E" + prec,
                    'g' or 'G' => "G" + prec,
                    _ => ""
                };
                digits = d.ToString(fmt, CultureInfo.InvariantCulture);
                if (spec.Specifier == 'e' || spec.Specifier == 'E')
                {
                    int idx = digits.IndexOf(spec.Specifier);
                    if (idx >= 0 && idx + 2 < digits.Length)
                    {
                        char signChar = digits[idx + 1];
                        string expDigits = digits[(idx + 2)..];
                        if (int.TryParse(expDigits, out int expVal))
                        {
                            expDigits = expVal.ToString("00", CultureInfo.InvariantCulture);
                            digits = digits.Substring(0, idx) + spec.Specifier + signChar + expDigits;
                        }
                    }
                }
                if (!digits.StartsWith("-") && (plus || space))
                    prefix = plus ? "+" : " ";
                return ApplyWidth(prefix + digits.TrimStart('+'), spec.Width, left, zero && !spec.Precision.HasValue, prefix.Length);
            case 'c':
                char ch = arg switch
                {
                    char c => c,
                    int n => (char)n,
                    _ => Convert.ToChar(arg ?? '\0')
                };
                digits = ch.ToString();
                return ApplyWidth(digits, spec.Width, left, zero, 0);
            case 's':
                string str = arg?.ToString() ?? string.Empty;
                if (spec.Precision.HasValue && spec.Precision.Value < str.Length)
                    str = str.Substring(0, spec.Precision.Value);
                return ApplyWidth(str, spec.Width, left, zero, 0);
            case 'p':
                if (arg is IntPtr ptr)
                {
                    digits = ptr.ToInt64().ToString("x", CultureInfo.InvariantCulture);
                    prefix = "0x";
                }
                else
                {
                    long addr;
                    try
                    {
                        addr = Convert.ToInt64(arg ?? 0, CultureInfo.InvariantCulture);
                    }
                    catch (Exception ex)
                    {
                        throw new ArgumentException("Invalid pointer argument", ex);
                    }
                    digits = addr.ToString("x", CultureInfo.InvariantCulture);
                    prefix = "0x";
                }
                return ApplyWidth(prefix + digits, spec.Width, left, zero, prefix.Length);
            default:
                throw new FormatException($"Unknown format specifier '%{spec.Specifier}'");
        }
    }

    private static string ApplyWidth(string value, int? width, bool left, bool zeroPad, int prefixLength)
    {
        if (!width.HasValue || value.Length >= width.Value)
            return value;
        int padLength = width.Value - value.Length;
        char padChar = zeroPad ? '0' : ' ';
        if (zeroPad && !left && prefixLength > 0)
        {
            // pad after prefix
            string prefix = value.Substring(0, prefixLength);
            string rest = value.Substring(prefixLength);
            rest = rest.PadLeft(width.Value - prefixLength, padChar);
            return prefix + rest;
        }
        return left ? value.PadRight(width.Value, padChar) : value.PadLeft(width.Value, padChar);
    }
}


using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using Antlr4.Runtime.Misc;

namespace TextTemplate;

/// <summary>
/// Template engine that parses Go text/template syntax with ANTLR,
/// lowers it into a lightweight AST, and executes it against dictionaries or .NET objects.
/// </summary>
internal static class TemplateEngine
{
    private static readonly ConcurrentDictionary<string, Delegate> RegisteredFunctions = new();
    private static readonly ConcurrentDictionary<Type, TypeMemberMap> MemberCache = new();

    private static readonly Dictionary<string, Func<object?[], object?>> BuiltinFunctions = new(StringComparer.Ordinal)
    {
        ["lower"] = args => args.Length > 0 ? args[0]?.ToString()?.ToLowerInvariant() : null,
        ["print"] = args => string.Concat(args.Select(a => a?.ToString())),
        ["printf"] = args =>
        {
            if (args.Length == 0) return string.Empty;
            string fmt = args[0]?.ToString() ?? string.Empty;
            var rest = args.Length > 1 ? args[1..] : Array.Empty<object?>();
            return SprintfFormatter.Format(fmt, rest);
        },
        ["html"] = args => WebUtility.HtmlEncode(string.Concat(args.Select(a => a?.ToString()))),
        ["js"] = args => JavaScriptEncoder.Default.Encode(string.Concat(args.Select(a => a?.ToString()))),
        ["urlquery"] = args => Uri.EscapeDataString(string.Concat(args.Select(a => a?.ToString()))),
        ["quote"] = args =>
        {
            if (args.Length == 0 || args[0] == null) return string.Empty;
            var s = args[0]?.ToString() ?? string.Empty;
            s = s.Replace("\"", "\\\"");
            return $"\"{s}\"";
        },
        ["len"] = args =>
        {
            if (args.Length == 0 || args[0] == null) return 0;
            var v = args[0]!;
            if (v is string s) return s.Length;
            if (v is ICollection col) return col.Count;
            if (v is IEnumerable enumerable)
            {
                int count = 0;
                var enumerator = enumerable.GetEnumerator();
                try
                {
                    while (enumerator.MoveNext()) count++;
                }
                finally
                {
                    (enumerator as IDisposable)?.Dispose();
                }
                return count;
            }
            return 0;
        },
        ["index"] = args =>
        {
            if (args.Length < 2) return null;
            object? current = args[0];
            for (int i = 1; i < args.Length && current != null; i++)
            {
                var key = args[i];
                if (key == null)
                {
                    current = null;
                    continue;
                }
                if (current is IList list && key is int idx)
                {
                    current = idx >= 0 && idx < list.Count ? list[idx] : null;
                    continue;
                }
                if (current is IDictionary dict)
                {
                    current = dict.Contains(key) ? dict[key] : null;
                    continue;
                }
                current = null;
            }
            return current;
        },
        ["slice"] = args =>
        {
            if (args.Length == 0 || args[0] == null) return null;
            int start = args.Length > 1 ? Convert.ToInt32(args[1], CultureInfo.InvariantCulture) : 0;
            int end = args.Length > 2 ? Convert.ToInt32(args[2], CultureInfo.InvariantCulture) : -1;
            var src = args[0];
            if (src is string str)
            {
                if (end < 0 || end > str.Length) end = str.Length;
                if (start < 0) start = 0;
                if (start >= end) return string.Empty;
                return str.Substring(start, end - start);
            }
            if (src is IList list)
            {
                if (end < 0 || end > list.Count) end = list.Count;
                if (start < 0) start = 0;
                if (start >= end) return Array.Empty<object?>();
                var result = new object?[end - start];
                for (int i = start; i < end; i++)
                    result[i - start] = list[i];
                return result;
            }
            return null;
        },
        ["call"] = args =>
        {
            if (args.Length == 0) return null;
            var fnSpec = args[0];
            var callArgs = args.Length > 1 ? args[1..] : Array.Empty<object?>();

            if (fnSpec is string name && RegisteredFunctions.TryGetValue(name, out var reg))
                return reg.DynamicInvoke(callArgs);

            return fnSpec switch
            {
                Func<object?[], object?> f => f(callArgs),
                Delegate d => d.DynamicInvoke(callArgs),
                _ => null
            };
        }
    };

    /// <summary>
    /// Processes <paramref name="templateString"/> by substituting tokens with values
    /// from <paramref name="data"/>.
    /// </summary>
    public static string Process(string templateString, IDictionary<string, object> data)
    {
        var compiled = Parse(templateString);
        return Process(compiled, data);
    }

    /// <summary>
    /// Processes <paramref name="templateString"/> using the public properties
    /// and fields of <paramref name="model"/> as template variables.
    /// </summary>
    public static string Process<T>(string templateString, T model)
    {
        var compiled = Parse(templateString);
        return Process(compiled, model);
    }

    /// <summary>
    /// Parses <paramref name="templateString"/> to ensure it contains valid syntax.
    /// </summary>
    public static void Validate(string templateString)
    {
        if (templateString == null) throw new ArgumentNullException(nameof(templateString));
        Parse(templateString);
    }

    internal static CompiledTemplate Parse(string templateString)
    {
        if (templateString == null) throw new ArgumentNullException(nameof(templateString));

        templateString = PreprocessTemplate(templateString);

        var inputStream = new AntlrInputStream(templateString);
        var lexer = new GoTextTemplateLexer(inputStream);
        lexer.RemoveErrorListeners();
        var tokens = new CommonTokenStream(lexer);
        var parser = new GoTextTemplateParser(tokens)
        {
            ErrorHandler = new BailErrorStrategy()
        };
        parser.RemoveErrorListeners();
        parser.Interpreter.PredictionMode = PredictionMode.SLL;

        GoTextTemplateParser.TemplateContext parseTree;
        try
        {
            parseTree = parser.template();
        }
        catch (ParseCanceledException)
        {
            inputStream = new AntlrInputStream(templateString);
            lexer = new GoTextTemplateLexer(inputStream);
            lexer.RemoveErrorListeners();
            tokens = new CommonTokenStream(lexer);
            parser = new GoTextTemplateParser(tokens)
            {
                ErrorHandler = new DefaultErrorStrategy()
            };
            parser.RemoveErrorListeners();
            parser.AddErrorListener(ThrowingParserErrorListener.Instance);
            parser.Interpreter.PredictionMode = PredictionMode.LL;
            parseTree = parser.template();
        }

        return AstBuilder.Build(parseTree);
    }

    internal static string Process(CompiledTemplate template, IDictionary<string, object> data) =>
        template.Execute(data);

    internal static string Process<T>(CompiledTemplate template, T model) =>
        template.Execute(model);

    internal static bool TryGetBuiltinFunction(string name, out Func<object?[], object?>? func) =>
        BuiltinFunctions.TryGetValue(name, out func);

    /// <summary>
    /// Registers a function that can be invoked from templates using the
    /// <c>call</c> pipeline helper.
    /// </summary>
    public static void RegisterFunction(string name, Delegate function)
    {
        if (name == null) throw new ArgumentNullException(nameof(name));
        if (function == null) throw new ArgumentNullException(nameof(function));
        RegisteredFunctions[name] = function;
    }

    /// <summary>
    /// Single-pass preprocessor that handles whitespace trimming ({{- and -}})
    /// and strips comment-only actions ({{/* ... */}}) while respecting string literals
    /// and comment boundaries inside template actions.
    /// </summary>
    private static string PreprocessTemplate(string template)
    {
        var sb = new StringBuilder(template.Length);
        int i = 0;
        int len = template.Length;

        while (i < len)
        {
            if (i + 1 < len && template[i] == '{' && template[i + 1] == '{')
            {
                bool openTrim = i + 2 < len && template[i + 2] == '-';
                int actionStart = i;
                int bodyStart = i + (openTrim ? 3 : 2);

                if (openTrim)
                {
                    while (sb.Length > 0 && char.IsWhiteSpace(sb[sb.Length - 1]))
                        sb.Length--;
                }

                int j = bodyStart;
                bool inString = false;
                bool hasNonCommentContent = false;
                bool closeTrim = false;
                bool foundClose = false;

                while (j < len)
                {
                    char c = template[j];

                    if (inString)
                    {
                        if (c == '\\' && j + 1 < len)
                        {
                            j += 2;
                            continue;
                        }
                        if (c == '"')
                            inString = false;
                        j++;
                        continue;
                    }

                    if (c == '/' && j + 1 < len && template[j + 1] == '*')
                    {
                        j += 2;
                        while (j + 1 < len && !(template[j] == '*' && template[j + 1] == '/'))
                            j++;
                        j = j + 1 < len ? j + 2 : len;
                        continue;
                    }

                    if (c == '-' && j + 2 < len && template[j + 1] == '}' && template[j + 2] == '}')
                    {
                        closeTrim = true;
                        foundClose = true;
                        j += 3;
                        break;
                    }

                    if (c == '}' && j + 1 < len && template[j + 1] == '}')
                    {
                        foundClose = true;
                        j += 2;
                        break;
                    }

                    if (c == '"')
                    {
                        inString = true;
                        hasNonCommentContent = true;
                        j++;
                        continue;
                    }

                    if (!char.IsWhiteSpace(c))
                        hasNonCommentContent = true;

                    j++;
                }

                if (!foundClose)
                {
                    sb.Append(template, actionStart, len - actionStart);
                    break;
                }

                if (hasNonCommentContent)
                    sb.Append(template, actionStart, j - actionStart);

                i = j;
                if (closeTrim)
                {
                    while (i < len && char.IsWhiteSpace(template[i]))
                        i++;
                }
                continue;
            }

            sb.Append(template[i]);
            i++;
        }

        return sb.ToString();
    }

    internal static bool IsTrue(object? value)
    {
        switch (value)
        {
            case null:
                return false;
            case bool b:
                return b;
            case string s:
                if (s.Length == 0) return false;
                if (s.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
                return true;
            case sbyte v:
                return v != 0;
            case byte v:
                return v != 0;
            case short v:
                return v != 0;
            case ushort v:
                return v != 0;
            case int v:
                return v != 0;
            case uint v:
                return v != 0;
            case long v:
                return v != 0;
            case ulong v:
                return v != 0;
            case float v:
                return v != 0f;
            case double v:
                return v != 0d;
            case decimal v:
                return v != 0m;
            case ICollection col:
                return col.Count > 0;
            case IEnumerable enumerable:
                var enumerator = enumerable.GetEnumerator();
                try
                {
                    return enumerator.MoveNext();
                }
                finally
                {
                    (enumerator as IDisposable)?.Dispose();
                }
            default:
                return true;
        }
    }

    private static bool IsNumeric(object? value) =>
        value is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal;

    internal static bool AreEqual(object? left, object? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left == null || right == null) return false;

        if (IsNumeric(left) && IsNumeric(right))
            return CompareNumeric(left, right) == 0;

        return Equals(left, right);
    }

    internal static bool TryCompare(object? left, object? right, out int comparison)
    {
        if (left == null || right == null)
        {
            comparison = 0;
            return false;
        }

        if (IsNumeric(left) && IsNumeric(right))
        {
            comparison = CompareNumeric(left, right);
            return true;
        }

        if (left is IComparable l && left.GetType() == right.GetType())
        {
            comparison = l.CompareTo(right);
            return true;
        }

        comparison = 0;
        return false;
    }

    private static int CompareNumeric(object left, object right)
    {
        if (left is float or double || right is float or double)
        {
            double l = Convert.ToDouble(left, CultureInfo.InvariantCulture);
            double r = Convert.ToDouble(right, CultureInfo.InvariantCulture);
            return l.CompareTo(r);
        }

        decimal ld = Convert.ToDecimal(left, CultureInfo.InvariantCulture);
        decimal rd = Convert.ToDecimal(right, CultureInfo.InvariantCulture);
        return ld.CompareTo(rd);
    }

    private static TypeMemberMap GetMemberMap(Type type) =>
        MemberCache.GetOrAdd(type, static t => new TypeMemberMap(t));

    internal static bool TryGetMemberValue(object target, string key, out object? value)
    {
        if (target is IDictionary<string, object> genericDict)
            return genericDict.TryGetValue(key, out value);

        if (target is IDictionary dict)
        {
            if (dict.Contains(key))
            {
                value = dict[key];
                return true;
            }
            value = null;
            return false;
        }

        var map = GetMemberMap(target.GetType());
        return map.TryGetValue(target, key, out value);
    }

    private sealed class TypeMemberMap
    {
        private readonly Dictionary<string, MemberInfo> _members;

        public TypeMemberMap(Type type)
        {
            _members = new Dictionary<string, MemberInfo>(StringComparer.Ordinal);
            foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (prop.GetIndexParameters().Length == 0 && prop.CanRead)
                    _members[prop.Name] = prop;
            }
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                _members.TryAdd(field.Name, field);
            }
        }

        public bool TryGetValue(object target, string name, out object? value)
        {
            if (_members.TryGetValue(name, out var member))
            {
                value = member switch
                {
                    PropertyInfo prop => prop.GetValue(target),
                    FieldInfo field => field.GetValue(target),
                    _ => null
                };
                return true;
            }

            value = null;
            return false;
        }
    }

    internal sealed class Scope
    {
        public Scope? Parent { get; }
        public object? Context { get; set; }
        private Dictionary<string, object?>? _variables;

        public Scope(Scope? parent, object? context)
        {
            Parent = parent;
            Context = context;
        }

        public void DeclareVariable(string name, object? value)
        {
            _variables ??= new Dictionary<string, object?>(StringComparer.Ordinal);
            _variables[name] = value;
        }

        public void ResetVariables()
        {
            _variables?.Clear();
        }

        public bool AssignVariable(string name, object? value)
        {
            for (var s = this; s != null; s = s.Parent)
            {
                if (s._variables != null && s._variables.ContainsKey(name))
                {
                    s._variables[name] = value;
                    return true;
                }
            }
            return false;
        }

        public bool TryGetVariable(string name, out object? value)
        {
            for (var s = this; s != null; s = s.Parent)
            {
                if (s._variables != null && s._variables.TryGetValue(name, out value))
                    return true;
            }
            value = null;
            return false;
        }

        public bool TryResolveName(string name, out object? value)
        {
            for (var s = this; s != null; s = s.Parent)
            {
                if (s._variables != null && s._variables.TryGetValue(name, out value))
                    return true;

                if (s.Context != null && TryGetMemberValue(s.Context, name, out value))
                    return true;
            }
            value = null;
            return false;
        }
    }

    private sealed class ThrowingParserErrorListener : BaseErrorListener
    {
        public static readonly ThrowingParserErrorListener Instance = new();

        public override void SyntaxError(
            TextWriter output,
            IRecognizer recognizer,
            IToken offendingSymbol,
            int line,
            int charPositionInLine,
            string msg,
            RecognitionException e)
        {
            throw new FormatException($"Template syntax error at line {line}:{charPositionInLine}: {msg}", e);
        }
    }
}

using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Net;
using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using Antlr4.Runtime.Misc;

namespace TextTemplate;

/// <summary>
/// Simple template processor that uses an ANTLR grammar
/// to replace {{variable}} tokens and evaluate {{if}}, {{range}}, {{with}}, and template directives.
/// </summary>
internal static class TemplateEngine
{
    private static readonly ConcurrentDictionary<string, Delegate> RegisteredFunctions = new();
    private static readonly ConcurrentDictionary<Type, TypeMemberMap> MemberCache = new();
    private static readonly ConcurrentDictionary<string, object[]> PathSegmentCache = new();

    /// <summary>
    /// Processes <paramref name="templateString"/> by substituting tokens with values
    /// from <paramref name="data"/>.
    /// </summary>
    public static string Process(string templateString, IDictionary<string, object> data)
    {
        var tree = Parse(templateString);
        return Process(tree, data);
    }

    /// <summary>
    /// Parses <paramref name="templateString"/> to ensure it contains valid syntax.
    /// </summary>
    public static void Validate(string templateString)
    {
        if (templateString == null) throw new ArgumentNullException(nameof(templateString));
        Parse(templateString);
    }

    internal static GoTextTemplateParser.TemplateContext Parse(string templateString)
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
        try
        {
            return parser.template();
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
            return parser.template();
        }
    }

    internal static string Process(GoTextTemplateParser.TemplateContext tree, IDictionary<string, object> data)
    {
        var templates = new Dictionary<string, GoTextTemplateParser.ContentContext>();
        var output = new StringBuilder();
        var rootScope = new Scope(null, data);
        var visitor = new ReplacementVisitor(rootScope, templates, output);
        visitor.Visit(tree);
        return output.ToString();
    }

    /// <summary>
    /// Processes <paramref name="templateString"/> using the public properties
    /// and fields of <paramref name="model"/> as template variables.
    /// </summary>
    public static string Process<T>(string templateString, T model)
    {
        var tree = Parse(templateString);
        return Process(tree, model);
    }

    internal static string Process<T>(GoTextTemplateParser.TemplateContext tree, T model)
    {
        var templates = new Dictionary<string, GoTextTemplateParser.ContentContext>();
        var output = new StringBuilder();
        var rootScope = new Scope(null, model);
        var visitor = new ReplacementVisitor(rootScope, templates, output);
        visitor.Visit(tree);
        return output.ToString();
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

                // Scan to matching }} or -}} respecting string literals and /* ... */ comments
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

                    // Comment inside action
                    if (c == '/' && j + 1 < len && template[j + 1] == '*')
                    {
                        j += 2;
                        while (j + 1 < len && !(template[j] == '*' && template[j + 1] == '/'))
                            j++;
                        if (j + 1 < len)
                            j += 2;
                        else
                            j = len;
                        continue;
                    }

                    // Close delimiter with trim: -}}
                    if (c == '-' && j + 2 < len && template[j + 1] == '}' && template[j + 2] == '}')
                    {
                        closeTrim = true;
                        foundClose = true;
                        j += 3;
                        break;
                    }

                    // Close delimiter: }}
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
                    // Unclosed action: pass remaining text to parser so it reports the syntax error
                    sb.Append(template, actionStart, len - actionStart);
                    break;
                }

                if (hasNonCommentContent)
                {
                    sb.Append(template, actionStart, j - actionStart);
                }

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

    private static bool IsTrue(object? value)
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

    private static bool AreEqual(object? left, object? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left == null || right == null) return false;

        if (IsNumeric(left) && IsNumeric(right))
        {
            return CompareNumeric(left, right) == 0;
        }

        return Equals(left, right);
    }

    private static bool TryCompare(object? left, object? right, out int comparison)
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

    /// <summary>
    /// Registers a function that can be invoked from templates using the
    /// <c>call</c> pipeline helper.
    /// </summary>
    /// <param name="name">The function name.</param>
    /// <param name="function">The delegate to invoke.</param>
    public static void RegisterFunction(string name, Delegate function)
    {
        if (name == null) throw new ArgumentNullException(nameof(name));
        if (function == null) throw new ArgumentNullException(nameof(function));
        RegisteredFunctions[name] = function;
    }

    private static TypeMemberMap GetMemberMap(Type type) =>
        MemberCache.GetOrAdd(type, static t => new TypeMemberMap(t));

    private static bool TryGetMemberValue(object target, string key, out object? value)
    {
        if (target is IDictionary<string, object> genericDict)
        {
            return genericDict.TryGetValue(key, out value);
        }

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

    private sealed class Scope
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

    private sealed class ReplacementVisitor : GoTextTemplateParserBaseVisitor<object?>
    {
        private readonly Scope _rootScope;
        private Scope _scope;
        private readonly Dictionary<string, GoTextTemplateParser.ContentContext> _templates;
        private readonly StringBuilder _output;

        private static readonly Dictionary<string, Func<object?[], object?>> PipelineFuncs = new(StringComparer.Ordinal)
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

        public ReplacementVisitor(Scope rootScope, Dictionary<string, GoTextTemplateParser.ContentContext> templates, StringBuilder output)
        {
            _rootScope = rootScope;
            _scope = rootScope;
            _templates = templates;
            _output = output;
        }

        public override object? VisitTemplate(GoTextTemplateParser.TemplateContext context)
        {
            VisitContent(context.content());
            return null;
        }

        public override object? VisitContent(GoTextTemplateParser.ContentContext context)
        {
            var parts = context.part();
            for (int i = 0; i < parts.Length; i++)
                VisitPart(parts[i]);
            return null;
        }

        public override object? VisitPart(GoTextTemplateParser.PartContext context)
        {
            var text = context.TEXT();
            if (text != null)
            {
                _output.Append(text.GetText());
                return null;
            }
            if (context.placeholder() != null) return VisitPlaceholder(context.placeholder());
            if (context.ifBlock() != null) return VisitIfBlock(context.ifBlock());
            if (context.forBlock() != null) return VisitForBlock(context.forBlock());
            if (context.rangeBlock() != null) return VisitRangeBlock(context.rangeBlock());
            if (context.withBlock() != null) return VisitWithBlock(context.withBlock());
            if (context.defineBlock() != null) return VisitDefineBlock(context.defineBlock());
            if (context.templateCall() != null) return VisitTemplateCall(context.templateCall());
            if (context.blockBlock() != null) return VisitBlockBlock(context.blockBlock());
            return null;
        }

        public override object? VisitPlaceholder(GoTextTemplateParser.PlaceholderContext context)
        {
            var (result, isAssignment) = EvaluatePipeline(context.pipeline());
            if (!isAssignment && result != null)
                _output.Append(result);
            return null;
        }

        public override object? VisitIfBlock(GoTextTemplateParser.IfBlockContext context)
        {
            if (IsTrue(EvaluateExpr(context.expr())))
            {
                VisitContent(context.content());
                return null;
            }

            foreach (var elif in context.elseIfBlock())
            {
                if (IsTrue(EvaluateExpr(elif.expr())))
                {
                    VisitContent(elif.content());
                    return null;
                }
            }

            var elseBlock = context.elseBlock();
            if (elseBlock != null)
                VisitContent(elseBlock.content());

            return null;
        }

        public override object? VisitForBlock(GoTextTemplateParser.ForBlockContext context)
        {
            var listObj = ResolvePath(context.path());
            if (listObj is not IEnumerable enumerable || listObj is string)
            {
                if (context.elseBlock() != null)
                    VisitContent(context.elseBlock().content());
                return null;
            }

            var itemName = context.IDENT().GetText();
            var content = context.content();
            var previousScope = _scope;
            var loopScope = new Scope(previousScope, null);
            bool hasItems = false;

            try
            {
                _scope = loopScope;
                foreach (var item in enumerable)
                {
                    hasItems = true;
                    loopScope.Context = item;
                    loopScope.ResetVariables();
                    loopScope.DeclareVariable(itemName, item);
                    VisitContent(content);
                }
            }
            finally
            {
                _scope = previousScope;
            }

            if (!hasItems && context.elseBlock() != null)
                VisitContent(context.elseBlock().content());

            return null;
        }

        public override object? VisitRangeBlock(GoTextTemplateParser.RangeBlockContext context)
        {
            var clause = context.rangeClause();
            var pathCtx = clause.path();
            object? sourceObj = pathCtx != null ? ResolvePath(pathCtx) : null;

            if (sourceObj is not IEnumerable enumerable || sourceObj is string)
            {
                if (context.elseBlock() != null)
                    VisitContent(context.elseBlock().content());
                return null;
            }

            string? firstVar = null;
            string? secondVar = null;
            var varList = clause.varList();
            if (varList != null)
            {
                var vars = varList.varName();
                if (vars.Length > 0) firstVar = vars[0].GetText().TrimStart('$');
                if (vars.Length > 1) secondVar = vars[1].GetText().TrimStart('$');
            }

            var content = context.content();
            var previousScope = _scope;
            var loopScope = new Scope(previousScope, null);
            bool hasItems = false;

            try
            {
                _scope = loopScope;
                if (sourceObj is IDictionary dict)
                {
                    foreach (DictionaryEntry entry in dict)
                    {
                        hasItems = true;
                        loopScope.Context = entry.Value;
                        loopScope.ResetVariables();
                        if (firstVar != null)
                            loopScope.DeclareVariable(firstVar, entry.Key);
                        if (secondVar != null)
                            loopScope.DeclareVariable(secondVar, entry.Value);
                        VisitContent(content);
                    }
                }
                else
                {
                    int index = 0;
                    foreach (var item in enumerable)
                    {
                        hasItems = true;
                        loopScope.Context = item;
                        loopScope.ResetVariables();
                        if (firstVar != null && secondVar != null)
                        {
                            loopScope.DeclareVariable(firstVar, index);
                            loopScope.DeclareVariable(secondVar, item);
                        }
                        else if (firstVar != null)
                        {
                            loopScope.DeclareVariable(firstVar, item);
                        }
                        VisitContent(content);
                        index++;
                    }
                }
            }
            finally
            {
                _scope = previousScope;
            }

            if (!hasItems && context.elseBlock() != null)
                VisitContent(context.elseBlock().content());

            return null;
        }

        public override object? VisitWithBlock(GoTextTemplateParser.WithBlockContext context)
        {
            var (value, _) = EvaluatePipeline(context.pipeline());
            if (IsTrue(value))
            {
                VisitInChildScope(value, context.content());
                return null;
            }

            if (context.elseBlock() != null)
                VisitContent(context.elseBlock().content());

            return null;
        }

        public override object? VisitDefineBlock(GoTextTemplateParser.DefineBlockContext context)
        {
            string name = UnescapeStringLiteral(context.STRING().GetText());
            _templates[name] = context.content();
            return null;
        }

        public override object? VisitTemplateCall(GoTextTemplateParser.TemplateCallContext context)
        {
            string name = UnescapeStringLiteral(context.STRING().GetText());
            if (!_templates.TryGetValue(name, out var tmpl))
                return null;

            object? ctxObj = context.pipeline() != null
                ? EvaluatePipeline(context.pipeline()).Value
                : _scope.Context;

            VisitInChildScope(ctxObj, tmpl);
            return null;
        }

        public override object? VisitBlockBlock(GoTextTemplateParser.BlockBlockContext context)
        {
            string name = UnescapeStringLiteral(context.STRING().GetText());
            if (!_templates.TryGetValue(name, out var tmpl))
            {
                tmpl = context.content();
                _templates[name] = tmpl;
            }

            var (ctxObj, _) = EvaluatePipeline(context.pipeline());
            VisitInChildScope(ctxObj, tmpl);
            return null;
        }

        private void VisitInChildScope(object? contextValue, GoTextTemplateParser.ContentContext content)
        {
            var previousScope = _scope;
            _scope = new Scope(previousScope, contextValue);
            try
            {
                VisitContent(content);
            }
            finally
            {
                _scope = previousScope;
            }
        }

        private object? ResolvePath(GoTextTemplateParser.PathContext context) =>
            ResolvePath(context.GetText());

        private object? ResolvePath(string text)
        {
            if (text == ".")
                return _scope.Context;

            if (text.StartsWith('$'))
            {
                if (text == "$")
                    return _rootScope.Context;

                if (text.StartsWith("$.", StringComparison.Ordinal))
                {
                    var segRoot = GetCachedSegments(text.Substring(2));
                    object? curRoot = _rootScope.Context;
                    for (int i = 0; i < segRoot.Length && curRoot != null; i++)
                        curRoot = ResolveSegment(curRoot, segRoot[i]);
                    return curRoot;
                }

                var segmentsVar = GetCachedSegments(text);
                if (segmentsVar.Length == 0 || segmentsVar[0] is not VariableName varSeg)
                    return null;

                if (!_scope.TryGetVariable(varSeg.Name, out object? current))
                    _scope.TryResolveName(varSeg.Name, out current);

                for (int idx = 1; idx < segmentsVar.Length && current != null; idx++)
                    current = ResolveSegment(current, segmentsVar[idx]);

                return current;
            }

            var segments = GetCachedSegments(text);
            if (segments.Length == 0)
                return null;

            object? currentVal;
            if (segments[0] is string firstKey)
            {
                if (!_scope.TryResolveName(firstKey, out currentVal))
                    return null;
            }
            else
            {
                currentVal = ResolveSegment(_scope.Context, segments[0]);
            }

            for (int i = 1; i < segments.Length && currentVal != null; i++)
                currentVal = ResolveSegment(currentVal, segments[i]);

            return currentVal;
        }

        private object? EvaluateExpr(GoTextTemplateParser.ExprContext context)
        {
            if (context.path() != null)
                return ResolvePath(context.path());

            if (context.EQ() != null)
            {
                var left = EvaluateValue(context.value(0));
                var right = EvaluateValue(context.value(1));
                return AreEqual(left, right);
            }

            if (context.NE() != null)
            {
                var left = EvaluateValue(context.value(0));
                var right = EvaluateValue(context.value(1));
                return !AreEqual(left, right);
            }

            if (context.LT() != null || context.LE() != null ||
                context.GT() != null || context.GE() != null)
            {
                var left = EvaluateValue(context.value(0));
                var right = EvaluateValue(context.value(1));
                if (TryCompare(left, right, out int cmp))
                {
                    if (context.LT() != null) return cmp < 0;
                    if (context.LE() != null) return cmp <= 0;
                    if (context.GT() != null) return cmp > 0;
                    if (context.GE() != null) return cmp >= 0;
                }
                return false;
            }

            if (context.AND() != null)
            {
                foreach (var e in context.expr())
                {
                    if (!IsTrue(EvaluateExpr(e)))
                        return false;
                }
                return true;
            }

            if (context.OR() != null)
            {
                foreach (var e in context.expr())
                {
                    if (IsTrue(EvaluateExpr(e)))
                        return true;
                }
                return false;
            }

            if (context.NOT() != null)
            {
                return !IsTrue(EvaluateExpr(context.expr(0)));
            }

            return null;
        }

        private (object? Value, bool IsAssignment) EvaluatePipeline(GoTextTemplateParser.PipelineContext context)
        {
            var varList = context.varList();
            bool isAssign = varList != null && context.ASSIGN() != null;

            var commands = context.command();
            object? result = null;
            for (int i = 0; i < commands.Length; i++)
            {
                result = ExecuteCommand(commands[i], i == 0 ? null : result);
            }

            if (varList != null)
            {
                foreach (var v in varList.varName())
                {
                    string name = v.GetText().TrimStart('$');
                    if (isAssign)
                    {
                        _scope.AssignVariable(name, result);
                    }
                    else
                    {
                        _scope.DeclareVariable(name, result);
                    }
                }
                return (result, true);
            }

            return (result, false);
        }

        private object? EvaluateLiteral(
            Antlr4.Runtime.Tree.ITerminalNode? numberNode,
            Antlr4.Runtime.Tree.ITerminalNode? stringNode,
            Antlr4.Runtime.Tree.ITerminalNode? boolNode)
        {
            if (numberNode != null)
            {
                string numText = numberNode.GetText();
                if (int.TryParse(numText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i))
                    return i;
                if (long.TryParse(numText, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l))
                    return l;
                return numText;
            }

            if (stringNode != null)
                return UnescapeStringLiteral(stringNode.GetText());

            if (boolNode != null)
                return bool.Parse(boolNode.GetText());

            return null;
        }

        private object? EvaluateValue(GoTextTemplateParser.ValueContext context)
        {
            if (context.path() != null)
                return ResolvePath(context.path());

            return EvaluateLiteral(context.NUMBER(), context.STRING(), context.BOOLEAN());
        }

        private object? ApplyPipelineFunction(string name, params object?[] args)
        {
            if (PipelineFuncs.TryGetValue(name, out var fn))
                return fn(args);
            return args.Length > 0 ? args[0] : null;
        }

        private object? ExecuteCommand(GoTextTemplateParser.CommandContext context, object? input)
        {
            if (context.path() != null && context.IDENT() == null)
            {
                string text = context.path().GetText();
                if (input != null && PipelineFuncs.ContainsKey(text))
                    return ApplyPipelineFunction(text, input);

                return ResolvePath(context.path());
            }

            if (context.IDENT() != null)
            {
                string name = context.IDENT().GetText();
                var argNodes = context.argument();
                int offset = input != null ? 1 : 0;
                var args = new object?[argNodes.Length + offset];
                if (input != null)
                    args[0] = input;
                for (int i = 0; i < argNodes.Length; i++)
                    args[i + offset] = EvaluateArgument(argNodes[i]);
                return ApplyPipelineFunction(name, args);
            }

            if (context.NUMBER() != null || context.STRING() != null || context.BOOLEAN() != null)
                return EvaluateLiteral(context.NUMBER(), context.STRING(), context.BOOLEAN());

            return input;
        }

        private object? EvaluateArgument(GoTextTemplateParser.ArgumentContext context)
        {
            if (context.path() != null)
                return ResolvePath(context.path());

            return EvaluateLiteral(context.NUMBER(), context.STRING(), context.BOOLEAN());
        }

        private static string UnescapeStringLiteral(string raw)
        {
            if (raw.Length < 2)
                return raw;

            string inner = raw.Substring(1, raw.Length - 2);
            if (!inner.Contains('\\'))
                return inner;

            var sb = new StringBuilder(inner.Length);
            for (int i = 0; i < inner.Length; i++)
            {
                char c = inner[i];
                if (c != '\\' || i + 1 >= inner.Length)
                {
                    sb.Append(c);
                    continue;
                }

                char next = inner[++i];
                switch (next)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'a': sb.Append('\a'); break;
                    case 'v': sb.Append('\v'); break;
                    case '0': sb.Append('\0'); break;
                    case 'u' when i + 4 < inner.Length &&
                        int.TryParse(inner.AsSpan(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int uCode):
                        sb.Append((char)uCode);
                        i += 4;
                        break;
                    case 'x' when i + 2 < inner.Length &&
                        int.TryParse(inner.AsSpan(i + 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int xCode):
                        sb.Append((char)xCode);
                        i += 2;
                        break;
                    default:
                        sb.Append('\\').Append(next);
                        break;
                }
            }
            return sb.ToString();
        }

        private sealed class PathReference
        {
            public string Path { get; }
            public PathReference(string path) => Path = path;
        }

        private sealed class VariableName
        {
            public string Name { get; }
            public VariableName(string name) => Name = name;
        }

        private static object[] GetCachedSegments(string text) =>
            PathSegmentCache.GetOrAdd(text, static t => ParseSegments(t));

        private static object[] ParseSegments(string text)
        {
            var result = new List<object>();
            int i = 0;
            if (text.StartsWith('$') && text.Length > 1 && text[1] != '.')
            {
                i = 1;
                int start = i;
                while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_')) i++;
                var varName = text.Substring(start, i - start);
                result.Add(new VariableName(varName));
                if (i < text.Length && text[i] == '.') i++;
            }
            else if (text.StartsWith('.'))
            {
                i++;
            }

            while (i < text.Length)
            {
                if (text[i] == '.')
                    i++;
                if (i >= text.Length) break;

                if (text[i] == '[')
                {
                    i++;
                    if (i < text.Length && text[i] == '"')
                    {
                        i++;
                        int start = i;
                        while (i < text.Length && text[i] != '"') i++;
                        var key = text.Substring(start, i - start);
                        result.Add(key);
                        if (i < text.Length && text[i] == '"') i++;
                    }
                    else
                    {
                        int start = i;
                        while (i < text.Length && text[i] != ']') i++;
                        var key = text.Substring(start, i - start).Trim();
                        if (int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int idx))
                            result.Add(idx);
                        else if (key.StartsWith('.') || key.StartsWith('$'))
                            result.Add(new PathReference(key));
                        else
                            result.Add(key);
                    }
                    if (i < text.Length && text[i] == ']') i++;
                }
                else
                {
                    int start = i;
                    while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_')) i++;
                    var id = text.Substring(start, i - start);
                    result.Add(id);
                }
            }

            return result.ToArray();
        }

        private object? ResolveSegment(object? current, object segment)
        {
            if (current == null) return null;

            if (segment is int idx)
            {
                return current is IList list && idx >= 0 && idx < list.Count ? list[idx] : null;
            }

            if (segment is PathReference pathRef)
            {
                var keyObj = ResolvePath(pathRef.Path);
                if (keyObj == null)
                    return null;

                if (keyObj is int dynIdx && current is IList listDyn)
                    return dynIdx >= 0 && dynIdx < listDyn.Count ? listDyn[dynIdx] : null;

                return TryGetMemberValue(current, keyObj.ToString()!, out var dynVal) ? dynVal : null;
            }

            string key = segment.ToString()!;
            return TryGetMemberValue(current, key, out var val) ? val : null;
        }
    }
}

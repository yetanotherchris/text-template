using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Encodings.Web;
using System.Net;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace TextTemplate;

/// <summary>
/// Simple template processor that uses a small ANTLR grammar
/// to replace {{variable}} tokens and evaluate {{if}} blocks.
/// </summary>
public static class TemplateEngine
{
    /// <summary>
    /// Processes <paramref name="templateString"/> by substituting tokens with values
    /// from <paramref name="data"/>.
    /// </summary>
    public static string Process(string templateString, IDictionary<string, object> data)
    {
        templateString = PreprocessWhitespace(templateString);
        templateString = PreprocessComments(templateString);
        AntlrInputStream inputStream = new(templateString);
        var lexer = new GoTextTemplateLexer(inputStream);
        var tokens = new CommonTokenStream(lexer);
        var parser = new GoTextTemplateParser(tokens);
        IParseTree tree = parser.template();

        var templates = new Dictionary<string, GoTextTemplateParser.ContentContext>();
        var visitor = new ReplacementVisitor(data, templates);
        return visitor.Visit(tree);
    }

    /// <summary>
    /// Processes <paramref name="templateString"/> using the public properties
    /// and fields of <paramref name="model"/> as template variables.
    /// </summary>
    public static string Process<T>(string templateString, T model)
    {
        IDictionary<string, object> dict = model as IDictionary<string, object> ??
            ToDictionary(model!);
        return Process(templateString, dict);
    }

    private static string PreprocessWhitespace(string template)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < template.Length;)
        {
            if (i + 3 <= template.Length && template[i] == '{' && template[i + 1] == '{' && template[i + 2] == '-')
            {
                while (sb.Length > 0 && char.IsWhiteSpace(sb[sb.Length - 1]))
                    sb.Length--;
                sb.Append("{{-");
                i += 3;
                continue;
            }

            if (i + 3 <= template.Length && template[i] == '-' && template[i + 1] == '}' && template[i + 2] == '}')
            {
                i += 3;
                while (i < template.Length && char.IsWhiteSpace(template[i]))
                    i++;
                sb.Append("-}}");
                continue;
            }

            sb.Append(template[i]);
            i++;
        }
        return sb.ToString();
    }

    private static string PreprocessComments(string template)
    {
        return Regex.Replace(template, "\\{\\{-?\\s*/\\*.*?\\*/\\s*-?\\}\\}", string.Empty, RegexOptions.Singleline);
    }

    private static IDictionary<string, object> ToDictionary(object model)
    {
        var dict = new Dictionary<string, object>();
        var type = model.GetType();
        foreach (var prop in type.GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
        {
            if (prop.GetIndexParameters().Length > 0)
                continue;
            var value = prop.GetValue(model);
            if (value != null)
                dict[prop.Name] = value;
        }
        foreach (var field in type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
        {
            var value = field.GetValue(model);
            if (value != null)
                dict[field.Name] = value;
        }
        return dict;
    }

    private static bool IsTrue(object? value)
    {
        return value switch
        {
            bool b => b,
            string s when bool.TryParse(s, out var b) => b,
            int i => i != 0,
            null => false,
            _ => true
        };
    }

    private static readonly Dictionary<string, Delegate> RegisteredFunctions = new();

    /// <summary>
    /// Registers a function that can be invoked from templates using the
    /// <c>call</c> pipeline helper.
    /// </summary>
    /// <param name="name">The function name.</param>
    /// <param name="function">The delegate to invoke.</param>
    public static void RegisterFunction(string name, Delegate function)
    {
        RegisteredFunctions[name] = function;
    }

    private class ReplacementVisitor : GoTextTemplateParserBaseVisitor<string>
    {
        private readonly IDictionary<string, object> _data;
        private readonly IDictionary<string, object> _rootData;
        private readonly object? _current;
        private readonly object? _rootCurrent;
        private bool _lastPipelineWasAssignment;
        private static readonly Dictionary<string, Func<object?[], object?>> PipelineFuncs = new()
        {
            ["lower"] = args => args.Length > 0 ? args[0]?.ToString()?.ToLowerInvariant() : null,
            ["print"] = args => string.Concat(args.Select(a => a?.ToString())),
            ["printf"] = args =>
            {
                if (args.Length == 0) return string.Empty;
                string fmt = args[0]?.ToString() ?? string.Empty;
                var rest = args.Skip(1).ToArray();
                return SprintfFormatter.Format(fmt, rest);
            },
            ["html"] = args => System.Net.WebUtility.HtmlEncode(string.Concat(args.Select(a => a?.ToString()))),
            ["js"] = args => System.Text.Encodings.Web.JavaScriptEncoder.Default.Encode(string.Concat(args.Select(a => a?.ToString()))),
            ["urlquery"] = args => Uri.EscapeDataString(string.Concat(args.Select(a => a?.ToString()))),
            ["len"] = args =>
            {
                if (args.Length == 0 || args[0] == null) return 0;
                var v = args[0]!;
                if (v is string s) return s.Length;
                if (v is ICollection col) return col.Count;
                if (v is IEnumerable enumerable)
                {
                    var list = enumerable.Cast<object?>().ToList();
                    return list.Count;
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
                    if (current is IList list && key is int idx)
                    {
                        current = idx >= 0 && idx < list.Count ? list[idx] : null;
                        continue;
                    }
                    if (current is IDictionary dict)
                    {
                        current = dict[key];
                        continue;
                    }
                    current = null;
                }
                return current;
            },
            ["slice"] = args =>
            {
                if (args.Length == 0 || args[0] == null) return null;
                int start = args.Length > 1 ? Convert.ToInt32(args[1]) : 0;
                int end = args.Length > 2 ? Convert.ToInt32(args[2]) : -1;
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
                    var result = new List<object?>();
                    for (int i = start; i < end; i++)
                        result.Add(list[i]);
                    return result.ToArray();
                }
                return null;
            },
            ["call"] = args =>
            {
                if (args.Length == 0) return null;
                var fnSpec = args[0];
                var callArgs = args.Skip(1).ToArray();

                if (fnSpec is string name && RegisteredFunctions.TryGetValue(name, out var reg))
                    return reg.DynamicInvoke(callArgs);

                switch (fnSpec)
                {
                    case Func<object?[], object?> f:
                        return f(callArgs);
                    case Delegate d:
                        return d.DynamicInvoke(callArgs);
                    default:
                        return null;
                }
            }
        };

        private readonly Dictionary<string, GoTextTemplateParser.ContentContext> _templates;

        public ReplacementVisitor(IDictionary<string, object> data, Dictionary<string, GoTextTemplateParser.ContentContext> templates)
            : this(data, data, data, data, templates)
        {
        }

        private ReplacementVisitor(IDictionary<string, object> data, IDictionary<string, object> rootData, object? current, object? rootCurrent, Dictionary<string, GoTextTemplateParser.ContentContext> templates)
        {
            _data = data;
            _rootData = rootData;
            _current = current;
            _rootCurrent = rootCurrent;
            _templates = templates;
        }

        public override string VisitTemplate(GoTextTemplateParser.TemplateContext context)
        {
            var sb = new StringBuilder();
            foreach (var part in context.content().part())
                sb.Append(Visit(part));
            return sb.ToString();
        }

        public override string VisitContent(GoTextTemplateParser.ContentContext context)
        {
            var sb = new StringBuilder();
            foreach (var part in context.part())
                sb.Append(Visit(part));
            return sb.ToString();
        }

        public override string VisitPart(GoTextTemplateParser.PartContext context)
        {
            if (context.TEXT() != null)
                return context.TEXT().GetText();
            if (context.placeholder() != null)
                return Visit(context.placeholder());
            if (context.ifBlock() != null)
                return Visit(context.ifBlock());
            if (context.forBlock() != null)
                return Visit(context.forBlock());
            if (context.rangeBlock() != null)
                return Visit(context.rangeBlock());
            if (context.withBlock() != null)
                return Visit(context.withBlock());
            if (context.defineBlock() != null)
                return Visit(context.defineBlock());
            if (context.templateCall() != null)
                return Visit(context.templateCall());
            if (context.blockBlock() != null)
                return Visit(context.blockBlock());
            return string.Empty;
        }

        public override string VisitPlaceholder(GoTextTemplateParser.PlaceholderContext context)
        {
            object? result = EvaluatePipeline(context.pipeline());
            if (_lastPipelineWasAssignment)
            {
                _lastPipelineWasAssignment = false;
                return string.Empty;
            }
            return result?.ToString() ?? string.Empty;
        }

        public override string VisitIfBlock(GoTextTemplateParser.IfBlockContext context)
        {
            object? condVal = EvaluateExpr(context.expr());
            if (IsTrue(condVal))
                return Visit(context.content());

            foreach (var elif in context.elseIfBlock())
            {
                condVal = EvaluateExpr(elif.expr());
                if (IsTrue(condVal))
                    return Visit(elif.content());
            }

            if (context.elseBlock() != null)
                return Visit(context.elseBlock().content());

            return string.Empty;
        }

        public override string VisitForBlock(GoTextTemplateParser.ForBlockContext context)
        {
            var listObj = ResolvePath(context.path());
            if (listObj is not IEnumerable enumerable)
            {
                if (context.elseBlock() != null)
                    return Visit(context.elseBlock().content());
                return string.Empty;
            }

            var itemName = context.IDENT().GetText();
            var sb = new StringBuilder();
            foreach (var item in enumerable)
            {
                var child = new Dictionary<string, object>(_data)
                {
                    [itemName] = item!,
                };
                var v = new ReplacementVisitor(child, _rootData, item!, _rootCurrent, _templates);
                sb.Append(v.Visit(context.content()));
            }

            if (sb.Length == 0 && context.elseBlock() != null)
                return Visit(context.elseBlock().content());

            return sb.ToString();
        }

        public override string VisitRangeBlock(GoTextTemplateParser.RangeBlockContext context)
        {
            object? sourceObj;
            var clause = context.rangeClause();
            var rcType = clause.GetType();
            object? pipelineObj = rcType.GetMethod("pipeline")?.Invoke(clause, null);
            if (clause.varList() != null && pipelineObj is GoTextTemplateParser.PipelineContext pc)
            {
                sourceObj = EvaluatePipeline(pc);
            }
            else
            {
                var pathCtx = rcType.GetMethod("path")?.Invoke(clause, null) as GoTextTemplateParser.PathContext;
                if (pathCtx != null)
                    sourceObj = ResolvePath(pathCtx);
                else
                    sourceObj = null;
            }

            if (sourceObj is not IEnumerable)
            {
                if (context.elseBlock() != null)
                    return Visit(context.elseBlock().content());
                return string.Empty;
            }
            IEnumerable enumerable = (IEnumerable)sourceObj;

            string? firstVar = null;
            string? secondVar = null;
            var varList = context.rangeClause().varList();
            if (varList != null)
            {
                var vars = varList.varName();
                if (vars.Length > 0) firstVar = vars[0].GetText().TrimStart('$');
                if (vars.Length > 1) secondVar = vars[1].GetText().TrimStart('$');
            }

            var sb = new StringBuilder();

            if (sourceObj is IDictionary dict)
            {
                foreach (DictionaryEntry entry in dict)
                {
                    var root = entry.Value is IDictionary<string, object> valueDict
                        ? new Dictionary<string, object>(valueDict)
                        : ToDictionary(entry.Value!);

                    foreach (var kv in _data)
                        if (!root.ContainsKey(kv.Key))
                            root[kv.Key] = kv.Value;

                    if (firstVar != null)
                        root[firstVar] = entry.Key!;
                    if (secondVar != null)
                        root[secondVar] = entry.Value!;

                    var v = new ReplacementVisitor(root, _rootData, entry.Value!, _rootCurrent, _templates);
                    sb.Append(v.Visit(context.content()));
                }
            }
            else
            {
                int index = 0;
                foreach (var item in enumerable)
                {
                    var root = item is IDictionary<string, object> itemDict
                        ? new Dictionary<string, object>(itemDict)
                        : ToDictionary(item!);

                    foreach (var kv in _data)
                        if (!root.ContainsKey(kv.Key))
                            root[kv.Key] = kv.Value;

                    if (firstVar != null && secondVar != null)
                    {
                        root[firstVar] = index;
                        root[secondVar] = item!;
                    }
                    else if (firstVar != null)
                    {
                        root[firstVar] = item!;
                    }

                    var v = new ReplacementVisitor(root, _rootData, item!, _rootCurrent, _templates);
                    sb.Append(v.Visit(context.content()));
                    index++;
                }
            }

            if (sb.Length == 0 && context.elseBlock() != null)
                return Visit(context.elseBlock().content());

            return sb.ToString();
        }

        public override string VisitWithBlock(GoTextTemplateParser.WithBlockContext context)
        {
            object? value = EvaluatePipeline(context.pipeline());
            if (IsTrue(value))
            {
                var root = value is IDictionary<string, object> dict
                    ? new Dictionary<string, object>(dict)
                    : ToDictionary(value!);

                foreach (var kv in _data)
                    if (!root.ContainsKey(kv.Key))
                        root[kv.Key] = kv.Value;

                var v = new ReplacementVisitor(root, _rootData, value!, _rootCurrent, _templates);
                return v.Visit(context.content());
            }

            if (context.elseBlock() != null)
                return Visit(context.elseBlock().content());

            return string.Empty;
        }

        public override string VisitDefineBlock(GoTextTemplateParser.DefineBlockContext context)
        {
            string name = context.STRING().GetText();
            name = name.Substring(1, name.Length - 2);
            _templates[name] = context.content();
            return string.Empty;
        }

        public override string VisitTemplateCall(GoTextTemplateParser.TemplateCallContext context)
        {
            string name = context.STRING().GetText();
            name = name.Substring(1, name.Length - 2);
            if (!_templates.TryGetValue(name, out var tmpl))
                return string.Empty;

            object? ctxObj = context.pipeline() != null ? EvaluatePipeline(context.pipeline()) : _data;

            IDictionary<string, object> root;
            if (ctxObj is IDictionary<string, object> dict)
                root = new Dictionary<string, object>(dict);
            else if (ctxObj == _data)
                root = new Dictionary<string, object>(_data);
            else if (ctxObj != null)
                root = ToDictionary(ctxObj!);
            else
                root = new Dictionary<string, object>();

            foreach (var kv in _data)
                if (!root.ContainsKey(kv.Key))
                    root[kv.Key] = kv.Value;

            object? newCurrent = ctxObj == _data ? _current : ctxObj;
            var v = new ReplacementVisitor(root, _rootData, newCurrent, _rootCurrent, _templates);
            return v.Visit(tmpl);
        }

        public override string VisitBlockBlock(GoTextTemplateParser.BlockBlockContext context)
        {
            string name = context.STRING().GetText();
            name = name.Substring(1, name.Length - 2);

            if (!_templates.TryGetValue(name, out var tmpl))
            {
                _templates[name] = context.content();
                tmpl = context.content();
            }

            object? ctxObj = EvaluatePipeline(context.pipeline());

            IDictionary<string, object> root;
            if (ctxObj is IDictionary<string, object> dict)
                root = new Dictionary<string, object>(dict);
            else if (ctxObj != null)
                root = ToDictionary(ctxObj!);
            else
                root = new Dictionary<string, object>();

            foreach (var kv in _data)
                if (!root.ContainsKey(kv.Key))
                    root[kv.Key] = kv.Value;

            var v = new ReplacementVisitor(root, _rootData, ctxObj, _rootCurrent, _templates);
            return v.Visit(tmpl);
        }

        private object? ResolvePath(GoTextTemplateParser.PathContext context)
        {
            string text = context.GetText();
            return ResolvePath(text);
        }

        private object? ResolvePath(string text)
        {
            if (text == ".")
                return _current;

            if (text.StartsWith("$"))
            {
                if (text == "$" || text.StartsWith("$."))
                {
                    string rootText = text.StartsWith("$.") ? text.Substring(2) : string.Empty;
                    if (rootText.Length == 0)
                        return _rootCurrent;
                    var segRoot = ParseSegments(rootText);
                    object? curRoot = _rootData;
                    foreach (var seg in segRoot)
                    {
                        if (curRoot == null)
                            return null;
                        curRoot = ResolveSegment(curRoot, seg);
                    }
                    return curRoot;
                }

                var segmentsVar = ParseSegments(text);
                if (segmentsVar.Count == 0 || segmentsVar[0] is not VariableName varSeg)
                    return null;
                _data.TryGetValue(varSeg.Name, out object? current);
                for (int idx = 1; idx < segmentsVar.Count; idx++)
                {
                    if (current == null)
                        return null;
                    current = ResolveSegment(current, segmentsVar[idx]);
                }
                return current;
            }

            var segments = ParseSegments(text);
            object? currentDefault = _data;
            foreach (var seg in segments)
            {
                if (currentDefault == null)
                    return null;
                currentDefault = ResolveSegment(currentDefault, seg);
            }
            return currentDefault;
        }

        private object? EvaluateExpr(GoTextTemplateParser.ExprContext context)
        {
            if (context.path() != null)
                return ResolvePath(context.path());

            if (context.EQ() != null)
            {
                var left = EvaluateValue(context.value(0));
                var right = EvaluateValue(context.value(1));
                return Equals(left, right);
            }

            if (context.NE() != null)
            {
                var left = EvaluateValue(context.value(0));
                var right = EvaluateValue(context.value(1));
                return !Equals(left, right);
            }

            if (context.LT() != null || context.LE() != null ||
                context.GT() != null || context.GE() != null)
            {
                var left = EvaluateValue(context.value(0));
                var right = EvaluateValue(context.value(1));
                if (left is IComparable l && right is IComparable r)
                {
                    int cmp = l.CompareTo(r);
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

        private object? EvaluatePipeline(GoTextTemplateParser.PipelineContext context)
        {
            _lastPipelineWasAssignment = false;

            // Use reflection to support optional var assignment fields
            GoTextTemplateParser.VarListContext? varList = null;
            bool isColoneq = false;
            bool isAssign = false;
            var type = context.GetType();
            var varListMethod = type.GetMethod("varList");
            if (varListMethod != null)
                varList = varListMethod.Invoke(context, null) as GoTextTemplateParser.VarListContext;
            if (varList != null)
            {
                isColoneq = type.GetMethod("COLONEQ")?.Invoke(context, null) != null;
                isAssign = type.GetMethod("ASSIGN")?.Invoke(context, null) != null;
            }

            var commands = context.command();
            object? result = null;
            bool first = true;
            foreach (var cmd in commands)
            {
                result = ExecuteCommand(cmd, first ? null : result);
                first = false;
            }

            if (varList != null)
            {
                foreach (var v in varList.varName())
                {
                    string name = v.GetText().TrimStart('$');
                    if (isAssign)
                    {
                        if (_data.ContainsKey(name))
                            _data[name] = result!;
                    }
                    else // COLONEQ
                    {
                        _data[name] = result!;
                    }
                }
                _lastPipelineWasAssignment = true;
            }

            return result;
        }

        private object? EvaluateValue(GoTextTemplateParser.ValueContext context)
        {
            if (context.path() != null)
                return ResolvePath(context.path());

            if (context.NUMBER() != null)
            {
                if (int.TryParse(context.NUMBER().GetText(), out int i))
                    return i;
                return context.NUMBER().GetText();
            }

            if (context.STRING() != null)
            {
                string s = context.STRING().GetText();
                s = s.Substring(1, s.Length - 2);
                s = Regex.Unescape(s);
                return s;
            }

            if (context.BOOLEAN() != null)
            {
                return bool.Parse(context.BOOLEAN().GetText());
            }

            return null;
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
                {
                    return ApplyPipelineFunction(text, input);
                }
                return ResolvePath(context.path());
            }

            if (context.IDENT() != null)
            {
                string name = context.IDENT().GetText();
                var args = new List<object?>();
                if (input != null)
                    args.Add(input);
                foreach (var a in context.argument())
                    args.Add(EvaluateArgument(a));
                return ApplyPipelineFunction(name, args.ToArray());
            }

            return input;
        }

        private object? EvaluateArgument(GoTextTemplateParser.ArgumentContext context)
        {
            if (context.path() != null)
                return ResolvePath(context.path());

            if (context.NUMBER() != null)
            {
                if (int.TryParse(context.NUMBER().GetText(), out int i))
                    return i;
                return context.NUMBER().GetText();
            }

            if (context.STRING() != null)
            {
                string s = context.STRING().GetText();
                s = s.Substring(1, s.Length - 2);
                s = Regex.Unescape(s);
                return s;
            }

            if (context.BOOLEAN() != null)
            {
                return bool.Parse(context.BOOLEAN().GetText());
            }

            return null;
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

        private static List<object> ParseSegments(string text)
        {
            var result = new List<object>();
            int i = 0;
            if (text.StartsWith("$") && text.Length > 1 && text[1] != '.')
            {
                i = 1;
                int start = i;
                while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_')) i++;
                var varName = text.Substring(start, i - start);
                result.Add(new VariableName(varName));
                if (i < text.Length && text[i] == '.') i++;
            }
            else if (text.StartsWith(".")) i++;
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
                        if (int.TryParse(key, out int idx))
                            result.Add(idx);
                        else if (key.StartsWith("."))
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
            return result;
        }

        private object? ResolveSegment(object? current, object segment)
        {
            if (current == null) return null;

            if (segment is int idx)
            {
                if (current is IList list)
                {
                    return idx >= 0 && idx < list.Count ? list[idx] : null;
                }
                return null;
            }

            if (segment is PathReference pathRef)
            {
                var keyObj = ResolvePath(pathRef.Path);
                if (keyObj == null)
                    return null;

                if (keyObj is int dynIdx && current is IList listDyn)
                {
                    return dynIdx >= 0 && dynIdx < listDyn.Count ? listDyn[dynIdx] : null;
                }

                var dynKey = keyObj.ToString()!;
                if (current is IDictionary<string, object> dictDyn)
                {
                    return dictDyn.TryGetValue(dynKey, out var val) ? val : null;
                }

                if (current is IDictionary mapDyn)
                {
                    return mapDyn.Contains(dynKey) ? mapDyn[dynKey] : null;
                }

                var typeDyn = current.GetType();
                var propDyn = typeDyn.GetProperty(dynKey);
                if (propDyn != null)
                    return propDyn.GetValue(current);

                var fieldDyn = typeDyn.GetField(dynKey);
                if (fieldDyn != null)
                    return fieldDyn.GetValue(current);

                return null;
            }

            string key = segment.ToString()!;
            if (current is IDictionary<string, object> dict)
            {
                return dict.TryGetValue(key, out var val) ? val : null;
            }

            if (current is IDictionary mapObj)
            {
                return mapObj.Contains(key) ? mapObj[key] : null;
            }

            var type = current.GetType();
            var prop = type.GetProperty(key);
            if (prop != null)
                return prop.GetValue(current);

            var field = type.GetField(key);
            if (field != null)
                return field.GetValue(current);

            return null;
        }
    }
}

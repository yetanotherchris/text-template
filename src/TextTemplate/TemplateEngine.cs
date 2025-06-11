using System.Collections.Generic;
using System.Collections;
using System.Text;
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
        AntlrInputStream inputStream = new(templateString);
        var lexer = new GoTextTemplateLexer(inputStream);
        var tokens = new CommonTokenStream(lexer);
        var parser = new GoTextTemplateParser(tokens);
        IParseTree tree = parser.template();

        var visitor = new ReplacementVisitor(data);
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

    private static IDictionary<string, object> ToDictionary(object model)
    {
        var dict = new Dictionary<string, object>();
        var type = model.GetType();
        foreach (var prop in type.GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
        {
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
            _ => false
        };
    }

    private class ReplacementVisitor : GoTextTemplateParserBaseVisitor<string>
    {
        private readonly IDictionary<string, object> _data;

        public ReplacementVisitor(IDictionary<string, object> data)
        {
            _data = data;
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
            return string.Empty;
        }

        public override string VisitPlaceholder(GoTextTemplateParser.PlaceholderContext context)
        {
            var value = ResolvePath(context.path());
            return value?.ToString() ?? string.Empty;
        }

        public override string VisitIfBlock(GoTextTemplateParser.IfBlockContext context)
        {
            object? condVal = ResolvePath(context.path());
            if (IsTrue(condVal))
                return Visit(context.content());

            foreach (var elif in context.elseIfBlock())
            {
                condVal = ResolvePath(elif.path());
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
                var v = new ReplacementVisitor(child);
                sb.Append(v.Visit(context.content()));
            }

            if (sb.Length == 0 && context.elseBlock() != null)
                return Visit(context.elseBlock().content());

            return sb.ToString();
        }

        private object? ResolvePath(GoTextTemplateParser.PathContext context)
        {
            string text = context.GetText();
            var segments = ParseSegments(text);
            object? current = _data;
            foreach (var seg in segments)
            {
                if (current == null)
                    return null;
                current = ResolveSegment(current, seg);
            }
            return current;
        }

        private static List<object> ParseSegments(string text)
        {
            var result = new List<object>();
            int i = 0;
            if (text.StartsWith(".")) i++;
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
                        var key = text.Substring(start, i - start);
                        if (int.TryParse(key, out int idx))
                            result.Add(idx);
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

        private static object? ResolveSegment(object? current, object segment)
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

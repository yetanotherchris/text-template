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
public static class AntlrTemplate
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
            var text = context.IDENT()?.GetText() ?? context.DOTIDENT()?.GetText();
            var key = text!.TrimStart('.');
            if (_data.TryGetValue(key, out var value))
                return value?.ToString() ?? string.Empty;
            return "{{UNDEFINED:" + key + "}}";
        }

        public override string VisitIfBlock(GoTextTemplateParser.IfBlockContext context)
        {
            var text = context.IDENT()?.GetText() ?? context.DOTIDENT()?.GetText();
            var key = text!.TrimStart('.');
            bool cond = _data.TryGetValue(key, out var val) && IsTrue(val);
            if (cond)
                return Visit(context.content(0));
            if (context.ELSE() != null)
                return Visit(context.content(1));
            return string.Empty;
        }

        public override string VisitForBlock(GoTextTemplateParser.ForBlockContext context)
        {
            var listToken = context.IDENT(1)?.GetText() ?? context.DOTIDENT()?.GetText();
            var listKey = listToken!.TrimStart('.');
            if (!_data.TryGetValue(listKey, out var value) || value is not System.Collections.IEnumerable enumerable)
                return string.Empty;

            var itemName = context.IDENT(0).GetText();
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
            return sb.ToString();
        }
    }
}

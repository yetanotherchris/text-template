using System.Collections.Generic;
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
        var lexer = new SimpleTemplateLexer(inputStream);
        var tokens = new CommonTokenStream(lexer);
        var parser = new SimpleTemplateParser(tokens);
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

    private class ReplacementVisitor : SimpleTemplateParserBaseVisitor<string>
    {
        private readonly IDictionary<string, object> _data;

        public ReplacementVisitor(IDictionary<string, object> data)
        {
            _data = data;
        }

        public override string VisitTemplate(SimpleTemplateParser.TemplateContext context)
        {
            var sb = new StringBuilder();
            foreach (var part in context.content().part())
                sb.Append(Visit(part));
            return sb.ToString();
        }

        public override string VisitPart(SimpleTemplateParser.PartContext context)
        {
            if (context.TEXT() != null)
                return context.TEXT().GetText();
            if (context.placeholder() != null)
                return Visit(context.placeholder());
            if (context.ifBlock() != null)
                return Visit(context.ifBlock());
            return string.Empty;
        }

        public override string VisitPlaceholder(SimpleTemplateParser.PlaceholderContext context)
        {
            var text = context.IDENT()?.GetText() ?? context.DOTIDENT()?.GetText();
            var key = text!.TrimStart('.');
            if (_data.TryGetValue(key, out var value))
                return value?.ToString() ?? string.Empty;
            return "{{UNDEFINED:" + key + "}}";
        }

        public override string VisitIfBlock(SimpleTemplateParser.IfBlockContext context)
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
    }
}

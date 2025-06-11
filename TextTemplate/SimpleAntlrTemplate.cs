using System.Collections.Generic;
using System.Text;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace TextTemplate;

/// <summary>
/// Very small template processor using the simple ANTLR grammar generated
/// from <c>SimpleTemplate.g4</c>. It replaces placeholders of the form
/// <c>{{name}}</c> with values from a dictionary.
/// </summary>
public static class SimpleAntlrTemplate
{
    public static string Process(string templateString, IDictionary<string, object> data)
    {
        AntlrInputStream input = new(templateString);
        var lexer = new SimpleTemplateLexer(input);
        var tokens = new CommonTokenStream(lexer);
        var parser = new SimpleTemplateParser(tokens);
        var tree = parser.template();
        var visitor = new RenderVisitor(data);
        return visitor.Visit(tree);
    }

    private class RenderVisitor : SimpleTemplateParserBaseVisitor<string>
    {
        private readonly IDictionary<string, object> _data;
        public RenderVisitor(IDictionary<string, object> data)
        {
            _data = data;
        }

        public override string VisitTemplate(SimpleTemplateParser.TemplateContext context)
        {
            var sb = new StringBuilder();
            foreach (var part in context.part())
                sb.Append(Visit(part));
            return sb.ToString();
        }

        public override string VisitPart(SimpleTemplateParser.PartContext context)
        {
            if (context.TEXT() != null)
                return context.TEXT().GetText();
            if (context.placeholder() != null)
                return Visit(context.placeholder());
            return string.Empty;
        }

        public override string VisitPlaceholder(SimpleTemplateParser.PlaceholderContext context)
        {
            string key = context.IDENT().GetText();
            if (_data.TryGetValue(key, out var value))
                return value?.ToString() ?? string.Empty;
            return "{{UNDEFINED:" + key + "}}";
        }
    }
}

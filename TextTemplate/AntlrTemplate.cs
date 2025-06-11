using System.Collections.Generic;
using System.Text;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace TextTemplate;

/// <summary>
/// Simple template processor that uses the generated GoTemplate parser
/// to replace {{variable}} tokens with values from a dictionary.
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
        var lexer = new GoTemplateLexer(inputStream);
        var tokens = new CommonTokenStream(lexer);
        var parser = new GoTemplateParser(tokens);
        IParseTree tree = parser.template();

        var visitor = new ReplacementVisitor(data);
        return visitor.Visit(tree);
    }

    private class ReplacementVisitor : GoTemplateBaseVisitor<string>
    {
        private readonly IDictionary<string, object> _data;

        public ReplacementVisitor(IDictionary<string, object> data)
        {
            _data = data;
        }

        public override string VisitTemplate(GoTemplateParser.TemplateContext context)
        {
            var sb = new StringBuilder();
            foreach (var elem in context.element())
                sb.Append(Visit(elem));
            return sb.ToString();
        }

        public override string VisitElement(GoTemplateParser.ElementContext context)
        {
            if (context.TEXT() != null)
                return context.TEXT().GetText();
            if (context.action() != null)
                return Visit(context.action());
            return string.Empty;
        }

        public override string VisitAction(GoTemplateParser.ActionContext context)
        {
            // Only handle simple variable actions of the form {{ name }} or {{ .name }}
            if (context.pipeline() != null)
            {
                var text = context.pipeline().GetText();
                var key = text.TrimStart('.');
                System.Console.Error.WriteLine($"VAR: '{text}' => '{key}'");
                if (_data.TryGetValue(key, out var value))
                    return value?.ToString() ?? string.Empty;
                return "{{UNDEFINED:" + key + "}}";
            }
            // For unsupported actions return them unchanged
            return context.GetText();
        }
    }
}

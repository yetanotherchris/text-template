using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
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
        // Parse template to validate syntax, though the parse tree is not yet
        // used for evaluation.
        AntlrInputStream inputStream = new(templateString);
        var lexer = new GoTemplateLexer(inputStream);
        var tokens = new CommonTokenStream(lexer);
        var parser = new GoTemplateParser(tokens);
        parser.template();

        string result = templateString;

        // Evaluate simple if/else blocks. Supports constructs like:
        // {{if .Condition}}text{{else}}other{{end}}
        const string ifPattern =
            "{{\\s*if\\s+(?<cond>\\.?[a-zA-Z_][a-zA-Z0-9_]*)\\s*}}" +
            "(?<then>.*?)" +
            "(?:{{\\s*else\\s*}}(?<else>.*?))?" +
            "{{\\s*end\\s*}}";
        Regex ifRegex = new(ifPattern, RegexOptions.Singleline);

        bool hasIf;
        do
        {
            hasIf = false;
            result = ifRegex.Replace(result, m =>
            {
                hasIf = true;
                string key = m.Groups["cond"].Value.TrimStart('.');
                bool cond = data.TryGetValue(key, out var v) && IsTrue(v);
                return cond ? m.Groups["then"].Value : m.Groups["else"].Value;
            });
        } while (hasIf);

        // Replace simple variables like {{ .Name }}
        result = Regex.Replace(
            result,
            @"{{\s*\.?(?<name>[a-zA-Z_][a-zA-Z0-9_]*)\s*}}",
            m => data.TryGetValue(m.Groups["name"].Value, out var val)
                ? val?.ToString() ?? string.Empty
                : "{{UNDEFINED:" + m.Groups["name"].Value + "}}");

        return result;
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

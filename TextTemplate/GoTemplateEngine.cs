using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace TextTemplate;

public static class GoTemplateEngine
{
    public static string Process(string templateString, Dictionary<string, object> data)
    {
        AntlrInputStream inputStream = new(templateString);
        GoTemplateLexer lexer = new(inputStream);
        CommonTokenStream tokens = new(lexer);
        GoTemplateParser parser = new(tokens);
        parser.RemoveErrorListeners();
        parser.AddErrorListener(new ThrowingErrorListener());
        var tree = parser.template();
        var processor = new GoTemplateProcessor(data);
        return processor.Visit(tree)?.ToString() ?? string.Empty;
    }
}

public class GoTemplateProcessor : GoTemplateBaseVisitor<object?>
{
    private readonly Dictionary<string, object> _data;
    private object? _currentContext;

    public GoTemplateProcessor(Dictionary<string, object> data)
    {
        _data = data;
    }

    public void SetCurrentContext(object? ctx) => _currentContext = ctx;

    public override object? VisitTemplate(GoTemplateParser.TemplateContext context)
    {
        var result = string.Empty;
        foreach (var element in context.element())
        {
            var r = Visit(element);
            if (r != null) result += r.ToString();
        }
        return result;
    }

    public override object? VisitElement(GoTemplateParser.ElementContext context)
    {
        if (context.TEXT() != null)
            return context.TEXT()!.GetText();
        if (context.action() != null)
            return Visit(context.action());
        return string.Empty;
    }

    public override object? VisitAction(GoTemplateParser.ActionContext context)
    {
        if (context.pipeline() != null && context.ChildCount == 3)
            return Visit(context.pipeline());

        var keyword = context.GetChild(1).GetText();
        return keyword switch
        {
            "if" => ProcessIfAction(context),
            "range" => ProcessRangeAction(context),
            "with" => ProcessWithAction(context),
            _ => string.Empty
        };
    }

    private object? ProcessIfAction(GoTemplateParser.ActionContext context)
    {
        var cond = Visit(context.pipeline());
        bool isTrue = EvaluateCondition(cond);
        var tpl = context.template();
        if (isTrue)
            return Visit(tpl);
        if (context.elseAction() != null)
        {
            var elseTpl = context.elseAction().template();
            return Visit(elseTpl);
        }
        return string.Empty;
    }

    private object? ProcessRangeAction(GoTemplateParser.ActionContext context)
    {
        var rangeVal = Visit(context.pipeline());
        var tpl = context.template();
        var result = string.Empty;

        if (rangeVal is Array arr)
        {
            foreach (var item in arr)
            {
                var proc = new GoTemplateProcessor(_data);
                proc.SetCurrentContext(item);
                result += proc.Visit(tpl)?.ToString();
            }
        }
        else if (rangeVal is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                var proc = new GoTemplateProcessor(_data);
                proc.SetCurrentContext(item);
                result += proc.Visit(tpl)?.ToString();
            }
        }
        return result;
    }

    private object? ProcessWithAction(GoTemplateParser.ActionContext context)
    {
        var withVal = Visit(context.pipeline());
        if (withVal != null)
        {
            var proc = new GoTemplateProcessor(_data);
            proc.SetCurrentContext(withVal);
            return proc.Visit(context.template());
        }
        return string.Empty;
    }

    public override object? VisitPipeline(GoTemplateParser.PipelineContext context)
    {
        object? result = null;
        foreach (var command in context.command())
            result = Visit(command);
        return result;
    }

    public override object? VisitCommand(GoTemplateParser.CommandContext context)
    {
        if (context.operand().Length > 0)
            return Visit(context.operand(0));
        return null;
    }

    public override object? VisitOperand(GoTemplateParser.OperandContext context)
    {
        return Visit(context.primary());
    }

    public override object? VisitPrimary(GoTemplateParser.PrimaryContext context)
    {
        if (context.IDENTIFIER() != null)
            return GetValue(context.IDENTIFIER()!.GetText());
        if (context.chainedField() != null)
            return Visit(context.chainedField());
        if (context.variable() != null)
            return Visit(context.variable());
        if (context.literal() != null)
            return Visit(context.literal());
        if (context.pipeline() != null)
            return Visit(context.pipeline());
        return null;
    }

    public override object? VisitChainedField(GoTemplateParser.ChainedFieldContext context)
    {
        object? cur = _currentContext ?? _data;
        foreach (var id in context.IDENTIFIER())
        {
            var name = id.GetText();
            cur = GetFieldValue(cur, name);
            if (cur == null) break;
        }
        return cur;
    }

    public override object? VisitVariable(GoTemplateParser.VariableContext context)
    {
        var name = context.IDENTIFIER().GetText();
        return GetValue(name);
    }

    public override object? VisitLiteral(GoTemplateParser.LiteralContext context)
    {
        if (context.STRING() != null)
        {
            var s = context.STRING().GetText();
            return s.Substring(1, s.Length - 2);
        }
        if (context.NUMBER() != null)
        {
            var n = context.NUMBER().GetText();
            return n.Contains('.') ? double.Parse(n) : int.Parse(n);
        }
        if (context.BOOLEAN() != null)
            return context.BOOLEAN().GetText() == "true";
        if (context.GetText() == "nil")
            return null;
        return null;
    }

    private object? GetValue(string key)
    {
        return _data.TryGetValue(key, out var value) ? value : null;
    }

    private static object? GetFieldValue(object? obj, string fieldName)
    {
        if (obj == null) return null;
        if (obj is Dictionary<string, object> dict)
        {
            dict.TryGetValue(fieldName, out var v);
            return v;
        }
        var type = obj.GetType();
        var prop = type.GetProperty(fieldName);
        if (prop != null) return prop.GetValue(obj);
        var field = type.GetField(fieldName);
        if (field != null) return field.GetValue(obj);
        return null;
    }

    private static bool EvaluateCondition(object? value)
    {
        if (value == null) return false;
        return value switch
        {
            bool b => b,
            string s => s.Length > 0,
            int i => i != 0,
            double d => d != 0.0,
            ICollection c => c.Count > 0,
            _ => true
        };
    }
}

public class ThrowingErrorListener : BaseErrorListener
{
    public override void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol,
        int line, int charPositionInLine, string msg, RecognitionException e)
    {
        throw new ArgumentException($"Syntax error at line {line}:{charPositionInLine} - {msg}");
    }
}

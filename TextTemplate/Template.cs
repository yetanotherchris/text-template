namespace TextTemplate;

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

public class Template
{
    private readonly string _name;
    internal List<INode> _nodes = new();
    private readonly Dictionary<string, Template> _templates = new();
    private readonly Dictionary<string, Delegate> _funcs = new();
    private string _leftDelim = "{{";
    private string _rightDelim = "}}";

    public Template(string name)
    {
        _name = name;
        _templates[name] = this;
        RegisterDefaultFuncs();
    }

    public static Template New(string name) => new Template(name);

    private void RegisterDefaultFuncs()
    {
        _funcs["join"] = (Func<IEnumerable, string, string>)((IEnumerable e, string sep) => string.Join(sep, e.Cast<object>()));
        _funcs["println"] = (Func<object[], string>)(args => string.Join(" ", args) + Environment.NewLine);
        _funcs["len"] = (Func<IEnumerable, int>)(e => e.Cast<object>().Count());
        _funcs["upper"] = (Func<string, string>)(s => s.ToUpperInvariant());
        _funcs["lower"] = (Func<string, string>)(s => s.ToLowerInvariant());
        _funcs["index"] = (Func<object, object, object?>)((col, key) =>
        {
            switch (col)
            {
                case IList list when key is int i:
                    return i >= 0 && i < list.Count ? list[i] : null;
                case IDictionary dict:
                    return dict.Contains(key) ? dict[key] : null;
                case string str when key is int j:
                    return j >= 0 && j < str.Length ? str[j] : null;
                default:
                    return null;
            }
        });
    }

    public Template Funcs(Dictionary<string, Delegate> funcs)
    {
        foreach (var kv in funcs)
        {
            _funcs[kv.Key] = kv.Value;
        }
        return this;
    }

    public Template Parse(string text)
    {
        _nodes = Parser.Parse(text, this);
        return this;
    }

    public Template Delims(string left, string right)
    {
        _leftDelim = left;
        _rightDelim = right;
        return this;
    }

    internal string LeftDelim => _leftDelim;
    internal string RightDelim => _rightDelim;

    public void Define(string name, Template tpl)
    {
        _templates[name] = tpl;
    }

    public Template Clone()
    {
        var t = new Template(_name);
        foreach (var kv in _templates)
        {
            if (kv.Key == _name) continue;
            t._templates[kv.Key] = kv.Value;
        }
        foreach (var kv in _funcs) t._funcs[kv.Key] = kv.Value;
        t._nodes = _nodes;
        return t;
    }

    public void AddTemplate(string name, Template tpl) => _templates[name] = tpl;

    public string Execute(object data)
    {
        var sb = new StringBuilder();
        var ctx = new Context(data, this);
        foreach (var n in _nodes)
        {
            n.Write(ctx, sb);
        }
        return sb.ToString();
    }

    internal bool TryGetFunc(string name, out Delegate? d) => _funcs.TryGetValue(name, out d);
    internal bool TryGetTemplate(string name, out Template? t) => _templates.TryGetValue(name, out t);
}

internal class Context
{
    public object Data { get; }
    public Template Template { get; }
    public Context(object data, Template t)
    {
        Data = data;
        Template = t;
    }
}

internal interface INode
{
    void Write(Context ctx, StringBuilder sb);
}

internal class TextNode : INode
{
    private readonly string _text;
    public TextNode(string text) { _text = text; }
    public void Write(Context ctx, StringBuilder sb) => sb.Append(_text);
}

internal class ActionNode : INode
{
    private readonly string _expr;
    public ActionNode(string expr) { _expr = expr.Trim(); }
    public void Write(Context ctx, StringBuilder sb)
    {
        var val = Evaluator.Eval(_expr, ctx);
        if (val != null) sb.Append(val);
    }
}

internal class IfNode : INode
{
    private readonly string _cond;
    private readonly List<INode> _then;
    private readonly List<INode>? _else;
    public IfNode(string cond, List<INode> thenPart, List<INode>? elsePart)
    {
        _cond = cond.Trim();
        _then = thenPart;
        _else = elsePart;
    }
    public void Write(Context ctx, StringBuilder sb)
    {
        var val = Evaluator.Eval(_cond, ctx);
        if (Evaluator.IsTrue(val))
        {
            foreach (var n in _then) n.Write(ctx, sb);
        }
        else if (_else != null)
        {
            foreach (var n in _else) n.Write(ctx, sb);
        }
    }
}

internal class RangeNode : INode
{
    private readonly string _expr;
    private readonly List<INode> _body;
    private readonly List<INode>? _else;
    public RangeNode(string expr, List<INode> body, List<INode>? elsePart)
    {
        _expr = expr.Trim();
        _body = body;
        _else = elsePart;
    }
    public void Write(Context ctx, StringBuilder sb)
    {
        var val = Evaluator.Eval(_expr, ctx);
        if (val is IEnumerable enumerable && enumerable.GetEnumerator().MoveNext())
        {
            foreach (var item in enumerable)
            {
                var childCtx = new Context(item!, ctx.Template);
                foreach (var n in _body) n.Write(childCtx, sb);
            }
        }
        else if (_else != null)
        {
            foreach (var n in _else) n.Write(ctx, sb);
        }
    }
}

internal class WithNode : INode
{
    private readonly string _expr;
    private readonly List<INode> _body;
    private readonly List<INode>? _else;
    public WithNode(string expr, List<INode> body, List<INode>? elsePart)
    {
        _expr = expr.Trim();
        _body = body;
        _else = elsePart;
    }
    public void Write(Context ctx, StringBuilder sb)
    {
        var val = Evaluator.Eval(_expr, ctx);
        if (Evaluator.IsTrue(val))
        {
            var childCtx = new Context(val!, ctx.Template);
            foreach (var n in _body) n.Write(childCtx, sb);
        }
        else if (_else != null)
        {
            foreach (var n in _else) n.Write(ctx, sb);
        }
    }
}

internal class TemplateNode : INode
{
    private readonly string _name;
    private readonly string? _expr;
    public TemplateNode(string name, string? expr)
    {
        _name = name;
        _expr = expr;
    }
    public void Write(Context ctx, StringBuilder sb)
    {
        if (!ctx.Template.TryGetTemplate(_name, out var tpl)) return;
        object data = ctx.Data;
        if (_expr != null)
            data = Evaluator.Eval(_expr, ctx)!;
        var childCtx = new Context(data, tpl);
        foreach (var n in tpl._nodes)
            n.Write(childCtx, sb);
    }
}

internal static class Parser
{
    public static List<INode> Parse(string text, Template owner)
    {
        var tokens = Tokenize(text, owner.LeftDelim, owner.RightDelim);
        var idx = 0;
        return ParseList(tokens, ref idx, owner);
    }

    private static List<Token> Tokenize(string text, string ldelim, string rdelim)
    {
        var tokens = new List<Token>();
        int i = 0;
        while (i < text.Length)
        {
            int start = text.IndexOf(ldelim, i, StringComparison.Ordinal);
            if (start == -1)
            {
                tokens.Add(new Token(TokenType.Text, text.Substring(i)));
                break;
            }

            if (start > i)
            {
                tokens.Add(new Token(TokenType.Text, text.Substring(i, start - i)));
            }

            bool trimLeft = start + ldelim.Length < text.Length && text[start + ldelim.Length] == '-';
            int actionStart = start + ldelim.Length + (trimLeft ? 1 : 0);

            int end = text.IndexOf(rdelim, actionStart, StringComparison.Ordinal);
            if (end == -1) throw new InvalidOperationException("unclosed action");

            bool trimRight = end > actionStart && text[end - 1] == '-';
            int actionEnd = trimRight ? end - 1 : end;

            string action = text.Substring(actionStart, actionEnd - actionStart);
            bool isComment = false;
            var trimmed = action.Trim();
            if (trimmed.StartsWith("/*") && trimmed.EndsWith("*/"))
            {
                isComment = true;
            }
            else
            {
                tokens.Add(new Token(TokenType.Action, action));
            }

            i = end + rdelim.Length;

            int textIdx = tokens.Count - 1;
            if (!isComment) textIdx--; // previous token before action
            if (trimLeft && textIdx >= 0 && tokens[textIdx].Type == TokenType.Text)
            {
                var txtTrim = tokens[textIdx].Text.TrimEnd();
                tokens[textIdx] = new Token(TokenType.Text, txtTrim);
            }

            if (trimRight)
            {
                while (i < text.Length && char.IsWhiteSpace(text[i]))
                {
                    i++;
                }
            }
        }
        return tokens;
    }

    private static string TrimDelims(string text)
    {
        var t = text.Trim();
        if (t.StartsWith("-")) t = t[1..].TrimStart();
        if (t.EndsWith("-")) t = t[..^1].TrimEnd();
        return t;
    }

    private static List<INode> ParseList(List<Token> tokens, ref int idx, Template owner)
    {
        var list = new List<INode>();
        while (idx < tokens.Count)
        {
            var tok = tokens[idx];
            if (tok.Type == TokenType.Text)
            {
                list.Add(new TextNode(tok.Text));
                idx++;
                continue;
            }
            var actionText = TrimDelims(tok.Text);
            var words = actionText.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            var head = words[0];
            string rest = words.Length > 1 ? words[1] : string.Empty;
            switch (head)
            {
                case "end":
                    return list;
                case "else":
                    return list;
                case "if":
                    idx++;
                    var thenPart = ParseList(tokens, ref idx, owner);
                    List<INode>? elsePart = null;
                    if (idx < tokens.Count && tokens[idx].Type == TokenType.Action && TrimDelims(tokens[idx].Text).StartsWith("else"))
                    {
                        idx++;
                        elsePart = ParseList(tokens, ref idx, owner);
                    }
                    if (idx >= tokens.Count || TrimDelims(tokens[idx].Text) != "end")
                        throw new InvalidOperationException("missing end");
                    idx++;
                    list.Add(new IfNode(rest, thenPart, elsePart));
                    break;
                case "range":
                    idx++;
                    var body = ParseList(tokens, ref idx, owner);
                    List<INode>? elseRange = null;
                    if (idx < tokens.Count && TrimDelims(tokens[idx].Text).StartsWith("else"))
                    {
                        idx++;
                        elseRange = ParseList(tokens, ref idx, owner);
                    }
                    if (idx >= tokens.Count || TrimDelims(tokens[idx].Text) != "end")
                        throw new InvalidOperationException("missing end");
                    idx++;
                    list.Add(new RangeNode(rest, body, elseRange));
                    break;
                case "with":
                    idx++;
                    var withBody = ParseList(tokens, ref idx, owner);
                    List<INode>? withElse = null;
                    if (idx < tokens.Count && TrimDelims(tokens[idx].Text).StartsWith("else"))
                    {
                        idx++;
                        withElse = ParseList(tokens, ref idx, owner);
                    }
                    if (idx >= tokens.Count || TrimDelims(tokens[idx].Text) != "end")
                        throw new InvalidOperationException("missing end");
                    idx++;
                    list.Add(new WithNode(rest, withBody, withElse));
                    break;
                case "template":
                    var parts = rest.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    string name = parts[0].Trim('"');
                    string? expr = parts.Length > 1 ? parts[1] : null;
                    list.Add(new TemplateNode(name, expr));
                    idx++;
                    break;
                case "define":
                    idx++;
                    var defNodes = ParseList(tokens, ref idx, owner);
                    if (idx >= tokens.Count || TrimDelims(tokens[idx].Text) != "end")
                        throw new InvalidOperationException("missing end");
                    idx++;
                    string defName = rest.Trim().Trim('"');
                    var tpl = new Template(defName);
                    tpl._nodes = defNodes;
                    owner.AddTemplate(defName, tpl);
                    break;
                case "block":
                    var blockParts = rest.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    string blockName = blockParts[0].Trim('"');
                    string blockExpr = blockParts.Length > 1 ? blockParts[1] : string.Empty;
                    idx++;
                    var blockBody = ParseList(tokens, ref idx, owner);
                    if (idx >= tokens.Count || TrimDelims(tokens[idx].Text) != "end")
                        throw new InvalidOperationException("missing end");
                    idx++;
                    var blockTpl = new Template(blockName);
                    blockTpl._nodes = blockBody;
                    owner.AddTemplate(blockName, blockTpl);
                    list.Add(new TemplateNode(blockName, blockExpr));
                    break;
                default:
                    list.Add(new ActionNode(tok.Text));
                    idx++;
                    break;
            }
        }
        return list;
    }
}

internal enum TokenType { Text, Action }
internal record Token(TokenType Type, string Text);

internal static class Evaluator
{
    public static object? Eval(string expr, Context ctx)
    {
        expr = expr.Trim();
        var pipeParts = expr.Split('|', StringSplitOptions.RemoveEmptyEntries);
        object? val = EvalSimple(pipeParts[0].Trim(), ctx);
        for (int i = 1; i < pipeParts.Length; i++)
        {
            var cmd = pipeParts[i].Trim();
            var pieces = Split(cmd);
            if (!ctx.Template.TryGetFunc(pieces[0], out var fn))
                throw new NotSupportedException($"unknown function {pieces[0]}");
            var args = new object?[pieces.Length];
            args[0] = val;
            for (int j = 1; j < pieces.Length; j++)
                args[j] = Eval(pieces[j], ctx);
            val = fn.DynamicInvoke(args);
        }
        return val;
    }

    private static object? EvalSimple(string expr, Context ctx)
    {
        expr = expr.Trim();
        if (expr == ".") return ctx.Data;
        if (expr.StartsWith("\"") && expr.EndsWith("\""))
            return expr.Substring(1, expr.Length - 2);
        var parts = Split(expr);
        if (parts.Length == 0) return null;
        if (ctx.Template.TryGetFunc(parts[0], out var fn))
        {
            var args = parts.Skip(1).Select(p => Eval(p, ctx)).ToArray();
            return fn.DynamicInvoke(args);
        }
        object? val = Resolve(ctx.Data, parts[0]);
        if (parts.Length == 1) return val;
        throw new NotSupportedException("complex expressions not supported");
    }

    private static string[] Split(string expr)
    {
        return expr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    private static object? Resolve(object? data, string field)
    {
        if (data == null) return null;
        if (field == ".") return data;
        if (field.StartsWith('.'))
            field = field[1..];
        if (data is IDictionary dict)
        {
            if (dict.Contains(field)) return dict[field];
        }
        var t = data.GetType();
        var prop = t.GetProperty(field, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (prop != null) return prop.GetValue(data);
        var f = t.GetField(field, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (f != null) return f.GetValue(data);
        var m = t.GetMethod(field, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase, null, Type.EmptyTypes, null);
        if (m != null) return m.Invoke(data, null);
        return null;
    }

    public static bool IsTrue(object? val)
    {
        if (val == null) return false;
        if (val is bool b) return b;
        if (val is string s) return s.Length > 0;
        if (val is IEnumerable e) return e.GetEnumerator().MoveNext();
        if (val is int i) return i != 0;
        return true;
    }
}

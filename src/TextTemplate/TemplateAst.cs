using System.Collections;
using System.Text;

namespace TextTemplate;

internal sealed class CompiledTemplate
{
    public TemplateNode[] Nodes { get; }
    public int EstimatedOutputLength { get; }

    public CompiledTemplate(TemplateNode[] nodes, int estimatedOutputLength)
    {
        Nodes = nodes;
        EstimatedOutputLength = estimatedOutputLength;
    }

    public string Execute(object? rootContext)
    {
        var output = new StringBuilder(EstimatedOutputLength);
        var ctx = new EvalContext(rootContext, output);
        RenderNodes(Nodes, ctx);
        return output.ToString();
    }

    internal static void RenderNodes(TemplateNode[] nodes, EvalContext ctx)
    {
        for (int i = 0; i < nodes.Length; i++)
            nodes[i].Render(ctx);
    }
}

internal sealed class EvalContext
{
    public TemplateEngine.Scope RootScope { get; }
    public TemplateEngine.Scope CurrentScope { get; set; }
    public StringBuilder Output { get; }
    private Dictionary<string, TemplateNode[]>? _templates;

    public EvalContext(object? rootContext, StringBuilder output)
    {
        RootScope = new TemplateEngine.Scope(null, rootContext);
        CurrentScope = RootScope;
        Output = output;
    }

    public void DefineTemplate(string name, TemplateNode[] body)
    {
        _templates ??= new Dictionary<string, TemplateNode[]>(StringComparer.Ordinal);
        _templates[name] = body;
    }

    public bool TryGetTemplate(string name, out TemplateNode[] body)
    {
        if (_templates != null && _templates.TryGetValue(name, out body!))
            return true;
        body = Array.Empty<TemplateNode>();
        return false;
    }

    public void RenderInChildScope(object? contextValue, TemplateNode[] body)
    {
        var previous = CurrentScope;
        CurrentScope = new TemplateEngine.Scope(previous, contextValue);
        try
        {
            CompiledTemplate.RenderNodes(body, this);
        }
        finally
        {
            CurrentScope = previous;
        }
    }
}

internal abstract class TemplateNode
{
    public abstract void Render(EvalContext ctx);
}

internal sealed class TextNode : TemplateNode
{
    public string Text { get; }

    public TextNode(string text) => Text = text;

    public override void Render(EvalContext ctx) => ctx.Output.Append(Text);
}

internal sealed class PlaceholderNode : TemplateNode
{
    private readonly PipelineNode _pipeline;

    public PlaceholderNode(PipelineNode pipeline) => _pipeline = pipeline;

    public override void Render(EvalContext ctx)
    {
        object? result = _pipeline.Evaluate(ctx);
        if (!_pipeline.IsAssignment && result != null)
            ctx.Output.Append(result);
    }
}

internal readonly struct ElseIfBranch
{
    public ExprNode Condition { get; }
    public TemplateNode[] Body { get; }

    public ElseIfBranch(ExprNode condition, TemplateNode[] body)
    {
        Condition = condition;
        Body = body;
    }
}

internal sealed class IfNode : TemplateNode
{
    private readonly ExprNode _condition;
    private readonly TemplateNode[] _body;
    private readonly ElseIfBranch[] _elseIfs;
    private readonly TemplateNode[]? _elseBody;

    public IfNode(ExprNode condition, TemplateNode[] body, ElseIfBranch[] elseIfs, TemplateNode[]? elseBody)
    {
        _condition = condition;
        _body = body;
        _elseIfs = elseIfs;
        _elseBody = elseBody;
    }

    public override void Render(EvalContext ctx)
    {
        if (TemplateEngine.IsTrue(_condition.Evaluate(ctx)))
        {
            CompiledTemplate.RenderNodes(_body, ctx);
            return;
        }

        for (int i = 0; i < _elseIfs.Length; i++)
        {
            if (TemplateEngine.IsTrue(_elseIfs[i].Condition.Evaluate(ctx)))
            {
                CompiledTemplate.RenderNodes(_elseIfs[i].Body, ctx);
                return;
            }
        }

        if (_elseBody != null)
            CompiledTemplate.RenderNodes(_elseBody, ctx);
    }
}

internal sealed class ForNode : TemplateNode
{
    private readonly PathExpr _source;
    private readonly string _itemVarName;
    private readonly TemplateNode[] _body;
    private readonly TemplateNode[]? _elseBody;

    public ForNode(PathExpr source, string itemVarName, TemplateNode[] body, TemplateNode[]? elseBody)
    {
        _source = source;
        _itemVarName = itemVarName;
        _body = body;
        _elseBody = elseBody;
    }

    public override void Render(EvalContext ctx)
    {
        var listObj = _source.Evaluate(ctx);
        if (listObj is not IEnumerable enumerable || listObj is string)
        {
            if (_elseBody != null)
                CompiledTemplate.RenderNodes(_elseBody, ctx);
            return;
        }

        var previousScope = ctx.CurrentScope;
        var loopScope = new TemplateEngine.Scope(previousScope, null);
        bool hasItems = false;

        try
        {
            ctx.CurrentScope = loopScope;
            foreach (var item in enumerable)
            {
                hasItems = true;
                loopScope.Context = item;
                loopScope.ResetVariables();
                loopScope.DeclareVariable(_itemVarName, item);
                CompiledTemplate.RenderNodes(_body, ctx);
            }
        }
        finally
        {
            ctx.CurrentScope = previousScope;
        }

        if (!hasItems && _elseBody != null)
            CompiledTemplate.RenderNodes(_elseBody, ctx);
    }
}

internal sealed class RangeNode : TemplateNode
{
    private readonly PathExpr? _source;
    private readonly string? _firstVar;
    private readonly string? _secondVar;
    private readonly TemplateNode[] _body;
    private readonly TemplateNode[]? _elseBody;

    public RangeNode(PathExpr? source, string? firstVar, string? secondVar, TemplateNode[] body, TemplateNode[]? elseBody)
    {
        _source = source;
        _firstVar = firstVar;
        _secondVar = secondVar;
        _body = body;
        _elseBody = elseBody;
    }

    public override void Render(EvalContext ctx)
    {
        object? sourceObj = _source?.Evaluate(ctx);
        if (sourceObj is not IEnumerable enumerable || sourceObj is string)
        {
            if (_elseBody != null)
                CompiledTemplate.RenderNodes(_elseBody, ctx);
            return;
        }

        var previousScope = ctx.CurrentScope;
        var loopScope = new TemplateEngine.Scope(previousScope, null);
        bool hasItems = false;

        try
        {
            ctx.CurrentScope = loopScope;
            if (sourceObj is IDictionary dict)
            {
                foreach (DictionaryEntry entry in dict)
                {
                    hasItems = true;
                    loopScope.Context = entry.Value;
                    loopScope.ResetVariables();
                    if (_firstVar != null)
                        loopScope.DeclareVariable(_firstVar, entry.Key);
                    if (_secondVar != null)
                        loopScope.DeclareVariable(_secondVar, entry.Value);
                    CompiledTemplate.RenderNodes(_body, ctx);
                }
            }
            else if (sourceObj is IList list)
            {
                int count = list.Count;
                for (int index = 0; index < count; index++)
                {
                    var item = list[index];
                    hasItems = true;
                    loopScope.Context = item;
                    loopScope.ResetVariables();
                    if (_firstVar != null && _secondVar != null)
                    {
                        loopScope.DeclareVariable(_firstVar, index);
                        loopScope.DeclareVariable(_secondVar, item);
                    }
                    else if (_firstVar != null)
                    {
                        loopScope.DeclareVariable(_firstVar, item);
                    }
                    CompiledTemplate.RenderNodes(_body, ctx);
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
                    if (_firstVar != null && _secondVar != null)
                    {
                        loopScope.DeclareVariable(_firstVar, index);
                        loopScope.DeclareVariable(_secondVar, item);
                    }
                    else if (_firstVar != null)
                    {
                        loopScope.DeclareVariable(_firstVar, item);
                    }
                    CompiledTemplate.RenderNodes(_body, ctx);
                    index++;
                }
            }
        }
        finally
        {
            ctx.CurrentScope = previousScope;
        }

        if (!hasItems && _elseBody != null)
            CompiledTemplate.RenderNodes(_elseBody, ctx);
    }
}

internal sealed class WithNode : TemplateNode
{
    private readonly PipelineNode _pipeline;
    private readonly TemplateNode[] _body;
    private readonly TemplateNode[]? _elseBody;

    public WithNode(PipelineNode pipeline, TemplateNode[] body, TemplateNode[]? elseBody)
    {
        _pipeline = pipeline;
        _body = body;
        _elseBody = elseBody;
    }

    public override void Render(EvalContext ctx)
    {
        object? value = _pipeline.Evaluate(ctx);
        if (TemplateEngine.IsTrue(value))
        {
            ctx.RenderInChildScope(value, _body);
            return;
        }

        if (_elseBody != null)
            CompiledTemplate.RenderNodes(_elseBody, ctx);
    }
}

internal sealed class DefineNode : TemplateNode
{
    private readonly string _name;
    private readonly TemplateNode[] _body;

    public DefineNode(string name, TemplateNode[] body)
    {
        _name = name;
        _body = body;
    }

    public override void Render(EvalContext ctx) => ctx.DefineTemplate(_name, _body);
}

internal sealed class TemplateCallNode : TemplateNode
{
    private readonly string _name;
    private readonly PipelineNode? _pipeline;

    public TemplateCallNode(string name, PipelineNode? pipeline)
    {
        _name = name;
        _pipeline = pipeline;
    }

    public override void Render(EvalContext ctx)
    {
        if (!ctx.TryGetTemplate(_name, out var body))
            return;

        object? ctxObj = _pipeline != null
            ? _pipeline.Evaluate(ctx)
            : ctx.CurrentScope.Context;

        ctx.RenderInChildScope(ctxObj, body);
    }
}

internal sealed class BlockNode : TemplateNode
{
    private readonly string _name;
    private readonly PipelineNode _pipeline;
    private readonly TemplateNode[] _defaultBody;

    public BlockNode(string name, PipelineNode pipeline, TemplateNode[] defaultBody)
    {
        _name = name;
        _pipeline = pipeline;
        _defaultBody = defaultBody;
    }

    public override void Render(EvalContext ctx)
    {
        if (!ctx.TryGetTemplate(_name, out var body))
        {
            body = _defaultBody;
            ctx.DefineTemplate(_name, body);
        }

        object? ctxObj = _pipeline.Evaluate(ctx);
        ctx.RenderInChildScope(ctxObj, body);
    }
}

// --- Expressions, Values, Pipelines, Paths ---

internal abstract class ValueNode
{
    public abstract object? Evaluate(EvalContext ctx);
}

internal sealed class LiteralValueNode : ValueNode
{
    private readonly object? _value;
    public LiteralValueNode(object? value) => _value = value;
    public override object? Evaluate(EvalContext ctx) => _value;
}

internal sealed class PathValueNode : ValueNode
{
    private readonly PathExpr _path;
    public PathValueNode(PathExpr path) => _path = path;
    public override object? Evaluate(EvalContext ctx) => _path.Evaluate(ctx);
}

internal abstract class ExprNode
{
    public abstract object? Evaluate(EvalContext ctx);
}

internal sealed class PathExprNode : ExprNode
{
    private readonly PathExpr _path;
    public PathExprNode(PathExpr path) => _path = path;
    public override object? Evaluate(EvalContext ctx) => _path.Evaluate(ctx);
}

internal enum ComparisonOp { Eq, Ne, Lt, Le, Gt, Ge }

internal sealed class ComparisonExprNode : ExprNode
{
    private readonly ComparisonOp _op;
    private readonly ValueNode _left;
    private readonly ValueNode _right;

    public ComparisonExprNode(ComparisonOp op, ValueNode left, ValueNode right)
    {
        _op = op;
        _left = left;
        _right = right;
    }

    public override object? Evaluate(EvalContext ctx)
    {
        var left = _left.Evaluate(ctx);
        var right = _right.Evaluate(ctx);

        if (_op == ComparisonOp.Eq) return TemplateEngine.AreEqual(left, right);
        if (_op == ComparisonOp.Ne) return !TemplateEngine.AreEqual(left, right);

        if (TemplateEngine.TryCompare(left, right, out int cmp))
        {
            return _op switch
            {
                ComparisonOp.Lt => cmp < 0,
                ComparisonOp.Le => cmp <= 0,
                ComparisonOp.Gt => cmp > 0,
                ComparisonOp.Ge => cmp >= 0,
                _ => false
            };
        }

        return false;
    }
}

internal sealed class AndExprNode : ExprNode
{
    private readonly ExprNode[] _operands;
    public AndExprNode(ExprNode[] operands) => _operands = operands;

    public override object? Evaluate(EvalContext ctx)
    {
        for (int i = 0; i < _operands.Length; i++)
        {
            if (!TemplateEngine.IsTrue(_operands[i].Evaluate(ctx)))
                return false;
        }
        return true;
    }
}

internal sealed class OrExprNode : ExprNode
{
    private readonly ExprNode[] _operands;
    public OrExprNode(ExprNode[] operands) => _operands = operands;

    public override object? Evaluate(EvalContext ctx)
    {
        for (int i = 0; i < _operands.Length; i++)
        {
            if (TemplateEngine.IsTrue(_operands[i].Evaluate(ctx)))
                return true;
        }
        return false;
    }
}

internal sealed class NotExprNode : ExprNode
{
    private readonly ExprNode _operand;
    public NotExprNode(ExprNode operand) => _operand = operand;
    public override object? Evaluate(EvalContext ctx) => !TemplateEngine.IsTrue(_operand.Evaluate(ctx));
}

internal sealed class PipelineNode
{
    private readonly string[]? _varNames;
    private readonly bool _isAssign;
    private readonly CommandNode[] _commands;

    public bool IsAssignment => _varNames != null;

    public PipelineNode(string[]? varNames, bool isAssign, CommandNode[] commands)
    {
        _varNames = varNames;
        _isAssign = isAssign;
        _commands = commands;
    }

    public object? Evaluate(EvalContext ctx)
    {
        object? result = null;
        for (int i = 0; i < _commands.Length; i++)
            result = _commands[i].Execute(ctx, i == 0 ? null : result);

        if (_varNames != null)
        {
            for (int i = 0; i < _varNames.Length; i++)
            {
                if (_isAssign)
                    ctx.CurrentScope.AssignVariable(_varNames[i], result);
                else
                    ctx.CurrentScope.DeclareVariable(_varNames[i], result);
            }
        }

        return result;
    }
}

internal abstract class CommandNode
{
    public abstract object? Execute(EvalContext ctx, object? input);
}

internal sealed class PathCommandNode : CommandNode
{
    private readonly PathExpr _path;
    private readonly Func<object?[], object?>? _builtinFunc;

    public PathCommandNode(PathExpr path, Func<object?[], object?>? builtinFunc)
    {
        _path = path;
        _builtinFunc = builtinFunc;
    }

    public override object? Execute(EvalContext ctx, object? input)
    {
        if (input != null && _builtinFunc != null)
            return _builtinFunc(new[] { input });
        return _path.Evaluate(ctx);
    }
}

internal sealed class CallCommandNode : CommandNode
{
    private readonly Func<object?[], object?>? _builtinFunc;
    private readonly ValueNode[] _args;

    public CallCommandNode(Func<object?[], object?>? builtinFunc, ValueNode[] args)
    {
        _builtinFunc = builtinFunc;
        _args = args;
    }

    public override object? Execute(EvalContext ctx, object? input)
    {
        int offset = input != null ? 1 : 0;
        var args = new object?[_args.Length + offset];
        if (input != null)
            args[0] = input;
        for (int i = 0; i < _args.Length; i++)
            args[i + offset] = _args[i].Evaluate(ctx);

        if (_builtinFunc != null)
            return _builtinFunc(args);

        return args.Length > 0 ? args[0] : null;
    }
}

internal sealed class LiteralCommandNode : CommandNode
{
    private readonly object? _value;
    public LiteralCommandNode(object? value) => _value = value;
    public override object? Execute(EvalContext ctx, object? input) => _value;
}

internal sealed class PassthroughCommandNode : CommandNode
{
    public static readonly PassthroughCommandNode Instance = new();
    public override object? Execute(EvalContext ctx, object? input) => input;
}

// --- Pre-parsed Path Expressions ---

internal enum PathKind
{
    CurrentDot,
    RootDot,
    RootPath,
    Variable,
    ScopePath
}

internal enum SegmentKind
{
    Property,
    Index,
    Dynamic
}

internal readonly struct PathSegment
{
    public SegmentKind Kind { get; }
    public string? PropertyName { get; }
    public int Index { get; }
    public PathExpr? DynamicPath { get; }

    private PathSegment(SegmentKind kind, string? propertyName, int index, PathExpr? dynamicPath)
    {
        Kind = kind;
        PropertyName = propertyName;
        Index = index;
        DynamicPath = dynamicPath;
    }

    public static PathSegment Property(string name) => new(SegmentKind.Property, name, 0, null);
    public static PathSegment IndexAt(int index) => new(SegmentKind.Index, null, index, null);
    public static PathSegment Dynamic(PathExpr path) => new(SegmentKind.Dynamic, null, 0, path);
}

internal sealed class PathExpr
{
    public static readonly PathExpr CurrentDotExpr = new(PathKind.CurrentDot, null, Array.Empty<PathSegment>());
    public static readonly PathExpr RootDotExpr = new(PathKind.RootDot, null, Array.Empty<PathSegment>());

    private readonly PathKind _kind;
    private readonly string? _rootName;
    private readonly PathSegment[] _segments;

    public PathExpr(PathKind kind, string? rootName, PathSegment[] segments)
    {
        _kind = kind;
        _rootName = rootName;
        _segments = segments;
    }

    public object? Evaluate(EvalContext ctx)
    {
        object? current;
        int startIndex = 0;

        switch (_kind)
        {
            case PathKind.CurrentDot:
                return ctx.CurrentScope.Context;

            case PathKind.RootDot:
                return ctx.RootScope.Context;

            case PathKind.RootPath:
                current = ctx.RootScope.Context;
                break;

            case PathKind.Variable:
                if (!ctx.CurrentScope.TryGetVariable(_rootName!, out current))
                    ctx.CurrentScope.TryResolveName(_rootName!, out current);
                break;

            case PathKind.ScopePath:
                if (_rootName != null)
                {
                    if (!ctx.CurrentScope.TryResolveName(_rootName, out current))
                        return null;
                }
                else
                {
                    current = ctx.CurrentScope.Context;
                }
                break;

            default:
                return null;
        }

        for (int i = startIndex; i < _segments.Length && current != null; i++)
            current = ResolveSegment(ctx, current, in _segments[i]);

        return current;
    }

    private static object? ResolveSegment(EvalContext ctx, object current, in PathSegment seg)
    {
        switch (seg.Kind)
        {
            case SegmentKind.Property:
                return TemplateEngine.TryGetMemberValue(current, seg.PropertyName!, out var propVal) ? propVal : null;

            case SegmentKind.Index:
                return current is IList list && seg.Index >= 0 && seg.Index < list.Count ? list[seg.Index] : null;

            case SegmentKind.Dynamic:
                var keyObj = seg.DynamicPath!.Evaluate(ctx);
                if (keyObj == null)
                    return null;
                if (keyObj is int dynIdx && current is IList listDyn)
                    return dynIdx >= 0 && dynIdx < listDyn.Count ? listDyn[dynIdx] : null;
                return TemplateEngine.TryGetMemberValue(current, keyObj.ToString()!, out var dynVal) ? dynVal : null;

            default:
                return null;
        }
    }
}

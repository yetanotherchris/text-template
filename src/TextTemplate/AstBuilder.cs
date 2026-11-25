using System.Globalization;
using System.Text;
using Antlr4.Runtime.Tree;

namespace TextTemplate;

/// <summary>
/// Lowers an ANTLR <see cref="GoTextTemplateParser.TemplateContext"/> parse tree
/// into an immutable <see cref="CompiledTemplate"/> AST once at parse time.
/// </summary>
internal static class AstBuilder
{
    public static CompiledTemplate Build(GoTextTemplateParser.TemplateContext context)
    {
        int estimatedLength = 0;
        var nodes = BuildContent(context.content(), ref estimatedLength);
        return new CompiledTemplate(nodes, Math.Max(64, estimatedLength + (estimatedLength >> 2)));
    }

    private static TemplateNode[] BuildContent(GoTextTemplateParser.ContentContext context, ref int estimatedLength)
    {
        var parts = context.part();
        if (parts.Length == 0)
            return Array.Empty<TemplateNode>();

        var result = new List<TemplateNode>(parts.Length);
        for (int i = 0; i < parts.Length; i++)
        {
            var node = BuildPart(parts[i], ref estimatedLength);
            if (node != null)
                result.Add(node);
        }
        return result.ToArray();
    }

    private static TemplateNode? BuildPart(GoTextTemplateParser.PartContext context, ref int estimatedLength)
    {
        var text = context.TEXT();
        if (text != null)
        {
            string str = text.GetText();
            estimatedLength += str.Length;
            return new TextNode(str);
        }

        if (context.placeholder() != null)
            return new PlaceholderNode(BuildPipeline(context.placeholder().pipeline()));

        if (context.ifBlock() != null)
            return BuildIfBlock(context.ifBlock(), ref estimatedLength);

        if (context.forBlock() != null)
            return BuildForBlock(context.forBlock(), ref estimatedLength);

        if (context.rangeBlock() != null)
            return BuildRangeBlock(context.rangeBlock(), ref estimatedLength);

        if (context.withBlock() != null)
            return BuildWithBlock(context.withBlock(), ref estimatedLength);

        if (context.defineBlock() != null)
        {
            var def = context.defineBlock();
            string name = UnescapeStringLiteral(def.STRING().GetText());
            int subLen = 0;
            var body = BuildContent(def.content(), ref subLen);
            return new DefineNode(name, body);
        }

        if (context.templateCall() != null)
        {
            var call = context.templateCall();
            string name = UnescapeStringLiteral(call.STRING().GetText());
            var pipeline = call.pipeline() != null ? BuildPipeline(call.pipeline()) : null;
            return new TemplateCallNode(name, pipeline);
        }

        if (context.blockBlock() != null)
        {
            var blk = context.blockBlock();
            string name = UnescapeStringLiteral(blk.STRING().GetText());
            var pipeline = BuildPipeline(blk.pipeline());
            var body = BuildContent(blk.content(), ref estimatedLength);
            return new BlockNode(name, pipeline, body);
        }

        return null;
    }

    private static IfNode BuildIfBlock(GoTextTemplateParser.IfBlockContext context, ref int estimatedLength)
    {
        var cond = BuildExpr(context.expr());
        var body = BuildContent(context.content(), ref estimatedLength);

        var elifCtxs = context.elseIfBlock();
        var elseIfs = elifCtxs.Length == 0 ? Array.Empty<ElseIfBranch>() : new ElseIfBranch[elifCtxs.Length];
        for (int i = 0; i < elifCtxs.Length; i++)
        {
            int branchLen = 0;
            elseIfs[i] = new ElseIfBranch(
                BuildExpr(elifCtxs[i].expr()),
                BuildContent(elifCtxs[i].content(), ref branchLen));
        }

        TemplateNode[]? elseBody = null;
        if (context.elseBlock() != null)
        {
            int elseLen = 0;
            elseBody = BuildContent(context.elseBlock().content(), ref elseLen);
        }

        return new IfNode(cond, body, elseIfs, elseBody);
    }

    private static ForNode BuildForBlock(GoTextTemplateParser.ForBlockContext context, ref int estimatedLength)
    {
        var source = ParsePath(context.path().GetText());
        var itemVar = context.IDENT().GetText();
        var body = BuildContent(context.content(), ref estimatedLength);

        TemplateNode[]? elseBody = null;
        if (context.elseBlock() != null)
        {
            int elseLen = 0;
            elseBody = BuildContent(context.elseBlock().content(), ref elseLen);
        }

        return new ForNode(source, itemVar, body, elseBody);
    }

    private static RangeNode BuildRangeBlock(GoTextTemplateParser.RangeBlockContext context, ref int estimatedLength)
    {
        var clause = context.rangeClause();
        var pathCtx = clause.path();
        var source = pathCtx != null ? ParsePath(pathCtx.GetText()) : null;

        string? firstVar = null;
        string? secondVar = null;
        var varList = clause.varList();
        if (varList != null)
        {
            var vars = varList.varName();
            if (vars.Length > 0) firstVar = vars[0].GetText().TrimStart('$');
            if (vars.Length > 1) secondVar = vars[1].GetText().TrimStart('$');
        }

        var body = BuildContent(context.content(), ref estimatedLength);

        TemplateNode[]? elseBody = null;
        if (context.elseBlock() != null)
        {
            int elseLen = 0;
            elseBody = BuildContent(context.elseBlock().content(), ref elseLen);
        }

        return new RangeNode(source, firstVar, secondVar, body, elseBody);
    }

    private static WithNode BuildWithBlock(GoTextTemplateParser.WithBlockContext context, ref int estimatedLength)
    {
        var pipeline = BuildPipeline(context.pipeline());
        var body = BuildContent(context.content(), ref estimatedLength);

        TemplateNode[]? elseBody = null;
        if (context.elseBlock() != null)
        {
            int elseLen = 0;
            elseBody = BuildContent(context.elseBlock().content(), ref elseLen);
        }

        return new WithNode(pipeline, body, elseBody);
    }

    private static ExprNode BuildExpr(GoTextTemplateParser.ExprContext context)
    {
        if (context.path() != null)
            return new PathExprNode(ParsePath(context.path().GetText()));

        if (context.EQ() != null)
            return new ComparisonExprNode(ComparisonOp.Eq, BuildValue(context.value(0)), BuildValue(context.value(1)));
        if (context.NE() != null)
            return new ComparisonExprNode(ComparisonOp.Ne, BuildValue(context.value(0)), BuildValue(context.value(1)));
        if (context.LT() != null)
            return new ComparisonExprNode(ComparisonOp.Lt, BuildValue(context.value(0)), BuildValue(context.value(1)));
        if (context.LE() != null)
            return new ComparisonExprNode(ComparisonOp.Le, BuildValue(context.value(0)), BuildValue(context.value(1)));
        if (context.GT() != null)
            return new ComparisonExprNode(ComparisonOp.Gt, BuildValue(context.value(0)), BuildValue(context.value(1)));
        if (context.GE() != null)
            return new ComparisonExprNode(ComparisonOp.Ge, BuildValue(context.value(0)), BuildValue(context.value(1)));

        if (context.AND() != null)
        {
            var exprs = context.expr();
            var operands = new ExprNode[exprs.Length];
            for (int i = 0; i < exprs.Length; i++)
                operands[i] = BuildExpr(exprs[i]);
            return new AndExprNode(operands);
        }

        if (context.OR() != null)
        {
            var exprs = context.expr();
            var operands = new ExprNode[exprs.Length];
            for (int i = 0; i < exprs.Length; i++)
                operands[i] = BuildExpr(exprs[i]);
            return new OrExprNode(operands);
        }

        if (context.NOT() != null)
            return new NotExprNode(BuildExpr(context.expr(0)));

        return new PathExprNode(PathExpr.CurrentDotExpr);
    }

    private static ValueNode BuildValue(GoTextTemplateParser.ValueContext context)
    {
        if (context.path() != null)
            return new PathValueNode(ParsePath(context.path().GetText()));

        return new LiteralValueNode(ParseLiteral(context.NUMBER(), context.STRING(), context.BOOLEAN()));
    }

    private static ValueNode BuildArgument(GoTextTemplateParser.ArgumentContext context)
    {
        if (context.path() != null)
            return new PathValueNode(ParsePath(context.path().GetText()));

        return new LiteralValueNode(ParseLiteral(context.NUMBER(), context.STRING(), context.BOOLEAN()));
    }

    private static PipelineNode BuildPipeline(GoTextTemplateParser.PipelineContext context)
    {
        string[]? varNames = null;
        bool isAssign = false;
        var varList = context.varList();
        if (varList != null)
        {
            isAssign = context.ASSIGN() != null;
            var vars = varList.varName();
            varNames = new string[vars.Length];
            for (int i = 0; i < vars.Length; i++)
                varNames[i] = vars[i].GetText().TrimStart('$');
        }

        var cmdCtxs = context.command();
        var commands = new CommandNode[cmdCtxs.Length];
        for (int i = 0; i < cmdCtxs.Length; i++)
            commands[i] = BuildCommand(cmdCtxs[i]);

        return new PipelineNode(varNames, isAssign, commands);
    }

    private static CommandNode BuildCommand(GoTextTemplateParser.CommandContext context)
    {
        if (context.path() != null && context.IDENT() == null)
        {
            string text = context.path().GetText();
            TemplateEngine.TryGetBuiltinFunction(text, out var builtin);
            return new PathCommandNode(ParsePath(text), builtin);
        }

        if (context.IDENT() != null)
        {
            string name = context.IDENT().GetText();
            TemplateEngine.TryGetBuiltinFunction(name, out var builtin);
            var argCtxs = context.argument();
            var args = argCtxs.Length == 0 ? Array.Empty<ValueNode>() : new ValueNode[argCtxs.Length];
            for (int i = 0; i < argCtxs.Length; i++)
                args[i] = BuildArgument(argCtxs[i]);
            return new CallCommandNode(builtin, args);
        }

        if (context.NUMBER() != null || context.STRING() != null || context.BOOLEAN() != null)
            return new LiteralCommandNode(ParseLiteral(context.NUMBER(), context.STRING(), context.BOOLEAN()));

        return PassthroughCommandNode.Instance;
    }

    private static object? ParseLiteral(ITerminalNode? numberNode, ITerminalNode? stringNode, ITerminalNode? boolNode)
    {
        if (numberNode != null)
        {
            string numText = numberNode.GetText();
            if (int.TryParse(numText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i))
                return i;
            if (long.TryParse(numText, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l))
                return l;
            return numText;
        }

        if (stringNode != null)
            return UnescapeStringLiteral(stringNode.GetText());

        if (boolNode != null)
            return bool.Parse(boolNode.GetText());

        return null;
    }

    internal static PathExpr ParsePath(string text)
    {
        if (text == ".")
            return PathExpr.CurrentDotExpr;
        if (text == "$")
            return PathExpr.RootDotExpr;

        if (text.StartsWith("$.", StringComparison.Ordinal))
        {
            var segs = ParseSegmentList(text, 2);
            return new PathExpr(PathKind.RootPath, null, segs);
        }

        if (text.StartsWith('$'))
        {
            int i = 1;
            while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_')) i++;
            string varName = text.Substring(1, i - 1);
            if (i < text.Length && text[i] == '.') i++;
            var segs = i < text.Length ? ParseSegmentList(text, i) : Array.Empty<PathSegment>();
            return new PathExpr(PathKind.Variable, varName, segs);
        }

        int start = text.StartsWith('.') ? 1 : 0;
        var allSegs = ParseSegmentList(text, start);
        if (allSegs.Length > 0 && allSegs[0].Kind == SegmentKind.Property)
        {
            string rootName = allSegs[0].PropertyName!;
            var rest = allSegs.Length > 1 ? allSegs[1..] : Array.Empty<PathSegment>();
            return new PathExpr(PathKind.ScopePath, rootName, rest);
        }

        return new PathExpr(PathKind.ScopePath, null, allSegs);
    }

    private static PathSegment[] ParseSegmentList(string text, int startIndex)
    {
        var result = new List<PathSegment>();
        int i = startIndex;

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
                    result.Add(PathSegment.Property(text.Substring(start, i - start)));
                    if (i < text.Length && text[i] == '"') i++;
                }
                else
                {
                    int start = i;
                    while (i < text.Length && text[i] != ']') i++;
                    var key = text.Substring(start, i - start).Trim();
                    if (int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int idx))
                        result.Add(PathSegment.IndexAt(idx));
                    else if (key.StartsWith('.') || key.StartsWith('$'))
                        result.Add(PathSegment.Dynamic(ParsePath(key)));
                    else
                        result.Add(PathSegment.Property(key));
                }
                if (i < text.Length && text[i] == ']') i++;
            }
            else
            {
                int start = i;
                while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_')) i++;
                result.Add(PathSegment.Property(text.Substring(start, i - start)));
            }
        }

        return result.Count == 0 ? Array.Empty<PathSegment>() : result.ToArray();
    }

    private static string UnescapeStringLiteral(string raw)
    {
        if (raw.Length < 2)
            return raw;

        string inner = raw.Substring(1, raw.Length - 2);
        if (!inner.Contains('\\'))
            return inner;

        var sb = new StringBuilder(inner.Length);
        for (int i = 0; i < inner.Length; i++)
        {
            char c = inner[i];
            if (c != '\\' || i + 1 >= inner.Length)
            {
                sb.Append(c);
                continue;
            }

            char next = inner[++i];
            switch (next)
            {
                case '"': sb.Append('"'); break;
                case '\\': sb.Append('\\'); break;
                case 'n': sb.Append('\n'); break;
                case 'r': sb.Append('\r'); break;
                case 't': sb.Append('\t'); break;
                case 'b': sb.Append('\b'); break;
                case 'f': sb.Append('\f'); break;
                case 'a': sb.Append('\a'); break;
                case 'v': sb.Append('\v'); break;
                case '0': sb.Append('\0'); break;
                case 'u' when i + 4 < inner.Length &&
                    int.TryParse(inner.AsSpan(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int uCode):
                    sb.Append((char)uCode);
                    i += 4;
                    break;
                case 'x' when i + 2 < inner.Length &&
                    int.TryParse(inner.AsSpan(i + 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int xCode):
                    sb.Append((char)xCode);
                    i += 2;
                    break;
                default:
                    sb.Append('\\').Append(next);
                    break;
            }
        }
        return sb.ToString();
    }
}

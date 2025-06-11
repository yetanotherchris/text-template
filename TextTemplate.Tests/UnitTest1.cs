using Xunit;
using TextTemplate;
using Antlr4.Runtime;
using System.Collections.Generic;

namespace TextTemplate.Tests;

public class Recipient
{
    public string Name { get; set; } = string.Empty;
    public string Gift { get; set; } = string.Empty;
    public bool Attended { get; set; }
}

public class UnitTest1
{

    [Fact]
    public void AntlrParser_ParsesPlainText()
    {
        const string text = "Hello world";
        var input = new AntlrInputStream(text);
        var lexer = new GoTemplateLexer(input);
        var tokens = new CommonTokenStream(lexer);
        var parser = new GoTemplateParser(tokens);
        var tree = parser.template();
        Assert.Equal(0, parser.NumberOfSyntaxErrors);
        Assert.Equal("(template (element Hello world))", tree.ToStringTree(parser));
    }

    [Fact]
    public void AntlrLexer_TokenizesAction()
    {
        const string text = "Hello {{.Name}}!";
        var input = new AntlrInputStream(text);
        var lexer = new GoTemplateLexer(input);
        var tokens = new List<IToken>();
        IToken t;
        while ((t = lexer.NextToken()).Type != TokenConstants.EOF)
            tokens.Add(t);

        Assert.Collection(tokens,
            tok => Assert.Equal(GoTemplateLexer.TEXT, tok.Type),
            tok => Assert.Equal(GoTemplateLexer.LEFT_DELIM, tok.Type),
            tok => Assert.Equal(GoTemplateLexer.TEXT, tok.Type)
        );
    }

    [Fact]
    public void AntlrTemplate_ReplacesVariables()
    {
        const string text = "Hello {{.Name}}!";
        var result = AntlrTemplate.Process(text, new Dictionary<string, object>
        {
            ["Name"] = "World"
        });
        Assert.Equal("Hello World!", result);
    }

    [Fact]
    public void AntlrParser_ParsesExampleTemplate()
    {
        const string letter = @"Dear {{.Name}},
{{if .Attended}}
It was a pleasure to see you at the wedding.
{{- else}}
It is a shame you couldn't make it to the wedding.
{{- end}}
{{with .Gift -}}
Thank you for the lovely {{.}}.
{{end}}
Best wishes,
Josie";

        var input = new AntlrInputStream(letter);
        var lexer = new GoTemplateLexer(input);
        var tokens = new CommonTokenStream(lexer);
        var parser = new GoTemplateParser(tokens);
        parser.template();
        Assert.Equal(0, parser.NumberOfSyntaxErrors);
    }
}

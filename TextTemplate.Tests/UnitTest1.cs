using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using TextTemplate;
using Antlr4.Runtime;

namespace TextTemplate.Tests;

public class Recipient
{
    public string Name { get; set; } = string.Empty;
    public string Gift { get; set; } = string.Empty;
    public bool Attended { get; set; }
}

public class UnitTest1
{
    // Example ported directly from Go's text/template documentation. This
    // serves as a simple integration test for the Template API.
    [Fact]
    public void ExampleTemplate()
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

        var t = Template.New("letter").Parse(letter);
        var recipients = new[]
        {
            new Recipient { Name = "Aunt Mildred", Gift = "bone china tea set", Attended = true },
            new Recipient { Name = "Uncle John", Gift = "moleskin pants", Attended = false },
            new Recipient { Name = "Cousin Rodney", Gift = "", Attended = false }
        };

        var sb = new System.Text.StringBuilder();
        foreach (var r in recipients)
        {
            sb.Append(t.Execute(r));
            sb.Append('\n');
        }

        var outText = sb.ToString();
        const string expected = "Dear Aunt Mildred,\n\n" +
            "It was a pleasure to see you at the wedding.\n" +
            "Thank you for the lovely bone china tea set.\n\n" +
            "Best wishes,\nJosie\n" +
            "Dear Uncle John,\n\n" +
            "It is a shame you couldn't make it to the wedding.\n" +
            "Thank you for the lovely moleskin pants.\n\n" +
            "Best wishes,\nJosie\n" +
            "Dear Cousin Rodney,\n\n" +
            "It is a shame you couldn't make it to the wedding.\n\n" +
            "Best wishes,\nJosie\n";
        Assert.Equal(expected, outText);
    }

    [Fact(Skip="Block parsing not fully implemented")]
    public void ExampleTemplateBlock_FromFile()
    {
        const string master = "Names:{{block \"list\" .}}{{\n}}{{range .}}{{println \"- \" .}}{{end}}{{end}}";
        const string overlay = "{{define \"list\"}} {{join . \", \"}}{{end}} ";

        var masterTmpl = Template.New("master").Funcs(new Dictionary<string, Delegate>()).Parse(master);
        var overlayTmpl = masterTmpl.Clone().Parse(overlay);

        var guardians = new[] { "Gamora", "Groot", "Nebula", "Rocket", "Star-Lord" };
        var masterOut = masterTmpl.Execute(guardians);
        var overlayOut = overlayTmpl.Execute(guardians);

        var tmpDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tmpDir);
        var file = Path.Combine(tmpDir, "master.txt");
        File.WriteAllText(file, master);
        var fromFile = Template.New("f").Parse(File.ReadAllText(file));
        var fileOut = fromFile.Execute(guardians);

        Assert.Equal(masterOut, fileOut);
        Assert.NotEmpty(masterOut);
        Assert.NotEmpty(overlayOut);
    }

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

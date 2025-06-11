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
    public void Template_ReturnsPlainTextUnchanged()
    {
        const string text = "Hello world";
        var result = TemplateEngine.Process(text, new Dictionary<string, object>());
        Assert.Equal("Hello world", result);
    }

    [Fact]
    public void Template_ReplacesVariable()
    {
        const string text = "Hello {{.Name}}!";
        var result = TemplateEngine.Process(text, new Dictionary<string, object>
        {
            ["Name"] = "World"
        });

        Assert.Equal("Hello World!", result);
    }

    [Fact]
    public void AntlrTemplate_ReplacesMultipleVariables()
    {
        const string text = "Hello {{.Name}}, you brought a {{.Gift}}.";
        var result = TemplateEngine.Process(text, new Dictionary<string, object>
        {
            ["Name"] = "Alice",
            ["Gift"] = "book"
        });
        Assert.Equal("Hello Alice, you brought a book.", result);
    }

    [Fact]
    public void AntlrTemplate_ReplacesVariablesInLetter()
    {
        const string letter = @"Dear {{ .Name }},
{{ if .Attended }}
It was a pleasure to see you at the wedding.
{{ else }}
It is a shame you couldn't make it to the wedding.
{{ end }}
Thank you for the lovely {{ .Gift }}.
Best wishes,
Josie";

        var result = TemplateEngine.Process(letter, new Dictionary<string, object>
        {
            ["Name"] = "Bob",
            ["Gift"] = "toaster",
            ["Attended"] = true
        });

        const string expected = "Dear Bob,\n\n" +
            "It was a pleasure to see you at the wedding.\n\n" +
            "Thank you for the lovely toaster.\n" +
            "Best wishes,\n" +
            "Josie";

        Assert.Equal(expected, result);
    }

    [Fact]
    public void AntlrTemplate_HandlesForLoop()
    {
        const string tmpl = "Numbers: {{ for n in Items }}{{ n }},{{ end }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Items"] = new[] { 1, 2, 3 }
        });
        Assert.Equal("Numbers: 1,2,3,", result);
    }

    [Fact]
    public void AntlrTemplate_NestedField()
    {
        const string tmpl = "User: {{ .User.Name }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["User"] = new Recipient { Name = "John" }
        });
        Assert.Equal("User: John", result);
    }

    [Fact]
    public void AntlrTemplate_ElseIfBlock()
    {
        const string tmpl = "{{ if .A }}A{{ else if .B }}B{{ else }}C{{ end }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["A"] = false,
            ["B"] = true
        });
        Assert.Equal("B", result);
    }

    [Fact]
    public void AntlrTemplate_ForLoopElse()
    {
        const string tmpl = "{{ for x in Items }}X{{ else }}Empty{{ end }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Items"] = new int[0]
        });
        Assert.Equal("Empty", result);
    }

    [Fact]
    public void TemplateEngine_UsesObjectProperties()
    {
        const string tmpl = "Hello {{ .Name }}!";
        var model = new Recipient { Name = "World" };
        var result = TemplateEngine.Process(tmpl, model);
        Assert.Equal("Hello World!", result);
    }

    [Fact]
    public void AntlrTemplate_ArrayIndexing()
    {
        const string tmpl = "First: {{ .Items[0] }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Items"] = new[] { "one", "two" }
        });
        Assert.Equal("First: one", result);
    }

    [Fact]
    public void AntlrTemplate_MapAccess()
    {
        const string tmpl = "Value: {{ .Data.key }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Data"] = new Dictionary<string, object> { ["key"] = "val" }
        });
        Assert.Equal("Value: val", result);
    }

    [Fact]
    public void AntlrTemplate_WhitespaceControl()
    {
        const string tmpl = "A  {{- .Name -}}  B";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Name"] = "X"
        });
        Assert.Equal("AXB", result);
    }
}

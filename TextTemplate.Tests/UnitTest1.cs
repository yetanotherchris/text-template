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
        var result = AntlrTemplate.Process(text, new Dictionary<string, object>());
        Assert.Equal("Hello world", result);
    }

    [Fact]
    public void Template_ReplacesVariable()
    {
        const string text = "Hello {{.Name}}!";
        var result = AntlrTemplate.Process(text, new Dictionary<string, object>
        {
            ["Name"] = "World"
        });

        Assert.Equal("Hello World!", result);
    }

    [Fact]
    public void AntlrTemplate_ReplacesMultipleVariables()
    {
        const string text = "Hello {{Name}}, you brought a {{Gift}}.";
        var result = AntlrTemplate.Process(text, new Dictionary<string, object>
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

        var result = AntlrTemplate.Process(letter, new Dictionary<string, object>
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
}

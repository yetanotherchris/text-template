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

public class User
{
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
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
    public void AntlrTemplate_EqualityCondition()
    {
        const string tmpl = "{{ if eq .Status \"active\" }}OK{{ else }}NO{{ end }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Status"] = "active"
        });
        Assert.Equal("OK", result);
    }

    [Fact]
    public void AntlrTemplate_NotEqualCondition()
    {
        const string tmpl = "{{ if ne .Count 0 }}non-zero{{ else }}zero{{ end }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Count"] = 5
        });
        Assert.Equal("non-zero", result);
    }

    [Fact]
    public void AntlrTemplate_ComparisonOperators()
    {
        const string tmpl = "{{ if lt .A .B }}lt{{ end }}{{ if le .A .A }}le{{ end }}" +
            "{{ if gt .B .A }}gt{{ end }}{{ if ge .B .B }}ge{{ end }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["A"] = 1,
            ["B"] = 2
        });
        Assert.Equal("ltlegtge", result);
    }

    [Fact]
    public void AntlrTemplate_LogicalOperations()
    {
        const string tmpl = "{{ if and .IsActive .IsValid }}ok{{ else }}bad{{ end }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["IsActive"] = true,
            ["IsValid"] = true
        });
        Assert.Equal("ok", result);
    }

    [Fact]
    public void AntlrTemplate_LogicalNegation()
    {
        const string tmpl = "{{ if not .Hidden }}show{{ else }}hide{{ end }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Hidden"] = false
        });
        Assert.Equal("show", result);
    }

    [Fact]
    public void AntlrTemplate_ExistenceCheck()
    {
        const string tmpl = "{{ if .User }}Yes{{ else }}No{{ end }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["User"] = new Recipient()
        });
        Assert.Equal("Yes", result);
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
    public void AntlrTemplate_ArrayIndexingVariable()
    {
        const string tmpl = "Current: {{ .Items[.CurrentIndex] }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Items"] = new[] { "zero", "one", "two" },
            ["CurrentIndex"] = 2
        });
        Assert.Equal("Current: two", result);
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
    public void AntlrTemplate_MapAccessSpecialKey()
    {
        const string tmpl = "Val: {{ .Data[\"key-with-dashes\"] }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Data"] = new Dictionary<string, object> { ["key-with-dashes"] = "x" }
        });
        Assert.Equal("Val: x", result);
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

    [Fact]
    public void AntlrTemplate_TrimLeadingWhitespace()
    {
        const string tmpl = "  {{- .Field }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Field"] = "X"
        });
        Assert.Equal("X", result);
    }

    [Fact]
    public void AntlrTemplate_TrimTrailingWhitespace()
    {
        const string tmpl = "{{ .Field -}}  ";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Field"] = "X"
        });
        Assert.Equal("X", result);
    }

    [Fact]
    public void AntlrTemplate_TrimBothWhitespace()
    {
        const string tmpl = "  {{- .Field -}}  ";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Field"] = "X"
        });
        Assert.Equal("X", result);
    }

    [Fact]
    public void AntlrTemplate_WhitespaceControlInLoop()
    {
        const string tmpl = "{{ range .Items -}}\n{{- .Name }}\n{{- end }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Items"] = new[]
            {
                new Dictionary<string, object> { ["Name"] = "A" },
                new Dictionary<string, object> { ["Name"] = "B" }
            }
        });
        Assert.Equal("AB", result);
    }

    [Fact]
    public void AntlrTemplate_RangeArray()
    {
        const string tmpl = "{{ range v := .Items }}{{ v }};{{ end }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Items"] = new[] { "a", "b", "c" }
        });
        Assert.Equal("a;b;c;", result);
    }

    [Fact]
    public void AntlrTemplate_RangeArrayIndex()
    {
        const string tmpl = "{{ range i, v := .Items }}{{ i }}={{ v }},{{ end }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Items"] = new[] { "x", "y" }
        });
        Assert.Equal("0=x,1=y,", result);
    }

    [Fact]
    public void AntlrTemplate_RangeMapKeyValue()
    {
        const string tmpl = "{{ range k, v := .Data }}{{ k }}={{ v }},{{ end }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Data"] = new Dictionary<string, object> { ["a"] = 1, ["b"] = 2 }
        });
        Assert.Equal("a=1,b=2,", result);
    }

    [Fact]
    public void AntlrTemplate_RangeElse()
    {
        const string tmpl = "{{ range .Empty }}X{{ else }}Empty{{ end }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Empty"] = new int[0]
        });
        Assert.Equal("Empty", result);
    }

    [Fact]
    public void AntlrTemplate_RangeElseIndentation()
    {
        const string tmpl = @"{{ range item := .Items }}
  Item: {{ item }}
{{ else }}
  No items found
{{ end }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Items"] = Array.Empty<string>()
        });
        const string expected = "\n  No items found\n";
        Assert.Equal(expected, result);
    }

    [Fact]
    public void AntlrTemplate_NestedLoops()
    {
        const string tmpl = @"{{ range .Categories }}
  Category: {{ .Name }}
  {{ range .Items }}
    - {{ .Title }}
  {{ end }}
{{ end }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Categories"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["Name"] = "CatA",
                    ["Items"] = new[]
                    {
                        new Dictionary<string, object> { ["Title"] = "A1" },
                        new Dictionary<string, object> { ["Title"] = "A2" }
                    }
                },
                new Dictionary<string, object>
                {
                    ["Name"] = "CatB",
                    ["Items"] = new[]
                    {
                        new Dictionary<string, object> { ["Title"] = "B1" }
                    }
                }
            }
        });
        const string expected = "\n  Category: CatA\n  \n    - A1\n  \n    - A2\n  \n\n  Category: CatB\n  \n    - B1\n  \n";
        Assert.Equal(expected, result);
    }

    [Fact]
    public void AntlrTemplate_RangeIndexEmptySlice()
    {
        const string tmpl = @"{{ range i, item := .EmptySlice }}
  Never executed
{{ else }}
  Empty slice message
{{ end }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["EmptySlice"] = Array.Empty<int>()
        });
        const string expected = "\n  Empty slice message\n";
        Assert.Equal(expected, result);
    }

    [Fact]
    public void AntlrTemplate_BasicComment()
    {
        const string tmpl = "Hello {{/* ignore */}}World";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>());
        Assert.Equal("Hello World", result);
    }

    [Fact]
    public void AntlrTemplate_MissingFieldAccess()
    {
        const string tmpl = "Missing: {{ .MaybeNil.Field }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>());
        Assert.Equal("Missing: ", result);
    }

    [Fact]
    public void AntlrTemplate_TrimmedComment()
    {
        const string tmpl = "A {{-/* c */-}} B";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>());
        Assert.Equal("AB", result);
    }

    [Fact]
    public void AntlrTemplate_NestedConditions()
    {
        const string tmpl = @"{{- if .User -}}
	{{- if .User.IsActive -}}
		Active user: {{ .User.Name }}
	{{- else -}}
		Inactive user
{{- end -}}
{{- end -}}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["User"] = new User { Name = "Alice", IsActive = true }
        });

		        Assert.Equal("Active user: Alice", result);
    }

    [Fact]
    public void AntlrTemplate_ComplexBooleanExpression()
    {
        const string tmpl = @"{{- if and (eq .Status ""active"") (gt .Count 0) -}}
	Status is active and count is positive
{{- end -}}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Status"] = "active",
            ["Count"] = 2
        });

	        Assert.Equal("Status is active and count is positive", result);
    }

    [Fact]
    public void AntlrTemplate_ZeroValueCheck()
    {
        const string tmpl = @"{{- if .Count -}}
	Count: {{ .Count }}
	{{- else -}}
	No items
{{- end -}}";
        var result1 = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Count"] = 3
        });
        Assert.Equal("Count: 3", result1);

        var result2 = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Count"] = 0
        });
        Assert.Equal("No items", result2);
    }

    [Fact]
    public void AntlrTemplate_PipelineLower()
    {
        const string tmpl = "Name: {{ .Name | lower }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Name"] = "Alice"
        });
        Assert.Equal("Name: alice", result);
    }
}

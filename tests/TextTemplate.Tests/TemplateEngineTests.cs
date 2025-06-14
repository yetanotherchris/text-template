using Xunit;
using Shouldly;
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

public class TemplateEngineTests
{

    [Fact]
    public void Template_ReturnsPlainTextUnchanged()
    {
        const string text = "Hello world";
        var result = TemplateEngine.Process(text, new Dictionary<string, object>());
        result.ShouldBe("Hello world");
    }

    [Fact]
    public void Template_ReplacesVariable()
    {
        const string text = "Hello {{.Name}}!";
        var result = TemplateEngine.Process(text, new Dictionary<string, object>
        {
            ["Name"] = "World"
        });

        result.ShouldBe("Hello World!");
    }

    [Fact]
    public void ReplacesMultipleVariables()
    {
        const string text = "Hello {{.Name}}, you brought a {{.Gift}}.";
        var result = TemplateEngine.Process(text, new Dictionary<string, object>
        {
            ["Name"] = "Alice",
            ["Gift"] = "book"
        });
        result.ShouldBe("Hello Alice, you brought a book.");
    }

    [Fact]
    public void ReplacesVariablesInLetter()
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

        result.ShouldBe(expected);
    }

    [Fact]
    public void HandlesForLoop()
    {
        const string tmpl = "Numbers: {{ for n in Items }}{{ n }},{{ end }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Items"] = new[] { 1, 2, 3 }
        });
        result.ShouldBe("Numbers: 1,2,3,");
    }

    [Fact]
    public void NestedField()
    {
        const string tmpl = "User: {{ .User.Name }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["User"] = new Recipient { Name = "John" }
        });
        result.ShouldBe("User: John");
    }

    [Fact]
    public void ElseIfBlock()
    {
        const string tmpl = "{{ if .A }}A{{ else if .B }}B{{ else }}C{{ end }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["A"] = false,
            ["B"] = true
        });
        result.ShouldBe("B");
    }

    [Fact]
    public void EqualityCondition()
    {
        const string tmpl = "{{ if eq .Status \"active\" }}OK{{ else }}NO{{ end }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Status"] = "active"
        });
        result.ShouldBe("OK");
    }

    [Fact]
    public void NotEqualCondition()
    {
        const string tmpl = "{{ if ne .Count 0 }}non-zero{{ else }}zero{{ end }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Count"] = 5
        });
        result.ShouldBe("non-zero");
    }

    [Fact]
    public void ComparisonOperators()
    {
        const string tmpl = "{{ if lt .A .B }}lt{{ end }}{{ if le .A .A }}le{{ end }}" +
            "{{ if gt .B .A }}gt{{ end }}{{ if ge .B .B }}ge{{ end }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["A"] = 1,
            ["B"] = 2
        });
        result.ShouldBe("ltlegtge");
    }

    [Fact]
    public void LogicalOperations()
    {
        const string tmpl = "{{ if and .IsActive .IsValid }}ok{{ else }}bad{{ end }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["IsActive"] = true,
            ["IsValid"] = true
        });
        result.ShouldBe("ok");
    }

    [Fact]
    public void LogicalNegation()
    {
        const string tmpl = "{{ if not .Hidden }}show{{ else }}hide{{ end }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Hidden"] = false
        });
        result.ShouldBe("show");
    }

    [Fact]
    public void MultiArgumentLogic()
    {
        const string tmpl = "{{ if and .A .B .C }}and{{ else }}none{{ end }} {{ if or .X .Y .Z }}or{{ else }}no{{ end }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["A"] = true,
            ["B"] = true,
            ["C"] = true,
            ["X"] = false,
            ["Y"] = false,
            ["Z"] = true
        });
        result.ShouldBe("and or");
    }

    [Fact]
    public void ExistenceCheck()
    {
        const string tmpl = "{{ if .User }}Yes{{ else }}No{{ end }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["User"] = new Recipient()
        });
        result.ShouldBe("Yes");
    }

    [Fact]
    public void ForLoopElse()
    {
        const string tmpl = "{{ for x in Items }}X{{ else }}Empty{{ end }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Items"] = new int[0]
        });
        result.ShouldBe("Empty");
    }

    [Fact]
    public void TemplateEngine_UsesObjectProperties()
    {
        const string tmpl = "Hello {{ .Name }}!";
        var model = new Recipient { Name = "World" };
        var result = TemplateEngine.Process(tmpl, model);
        result.ShouldBe("Hello World!");
    }

    [Fact]
    public void ArrayIndexing()
    {
        const string tmpl = "First: {{ .Items[0] }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Items"] = new[] { "one", "two" }
        });
        result.ShouldBe("First: one");
    }

    [Fact]
    public void ArrayIndexingVariable()
    {
        const string tmpl = "Current: {{ .Items[.CurrentIndex] }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Items"] = new[] { "zero", "one", "two" },
            ["CurrentIndex"] = 2
        });
        result.ShouldBe("Current: two");
    }

    [Fact]
    public void MapAccess()
    {
        const string tmpl = "Value: {{ .Data.key }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Data"] = new Dictionary<string, object> { ["key"] = "val" }
        });
        result.ShouldBe("Value: val");
    }

    [Fact]
    public void MapAccessSpecialKey()
    {
        const string tmpl = "Val: {{ .Data[\"key-with-dashes\"] }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Data"] = new Dictionary<string, object> { ["key-with-dashes"] = "x" }
        });
        result.ShouldBe("Val: x");
    }

    [Fact]
    public void WhitespaceControl()
    {
        const string tmpl = "A  {{- .Name -}}  B"; 
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Name"] = "X"
        });
        result.ShouldBe("AXB");
    }

    [Fact]
    public void TrimLeadingWhitespace()
    {
        const string tmpl = "  {{- .Field }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Field"] = "X"
        });
        result.ShouldBe("X");
    }

    [Fact]
    public void TrimTrailingWhitespace()
    {
        const string tmpl = "{{ .Field -}}  ";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Field"] = "X"
        });
        result.ShouldBe("X");
    }

    [Fact]
    public void TrimBothWhitespace()
    {
        const string tmpl = "  {{- .Field -}}  ";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Field"] = "X"
        });
        result.ShouldBe("X");
    }

    [Fact]
    public void WhitespaceControlInLoop()
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
        result.ShouldBe("AB");
    }

    [Fact]
    public void RangeArray()
    {
        const string tmpl = "{{ range v := .Items }}{{ v }};{{ end }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Items"] = new[] { "a", "b", "c" }
        });
        result.ShouldBe("a;b;c;");
    }

    [Fact]
    public void RangeArrayIndex()
    {
        const string tmpl = "{{ range i, v := .Items }}{{ i }}={{ v }},{{ end }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Items"] = new[] { "x", "y" }
        });
        result.ShouldBe("0=x,1=y,");
    }

    [Fact]
    public void RangeMapKeyValue()
    {
        const string tmpl = "{{ range k, v := .Data }}{{ k }}={{ v }},{{ end }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Data"] = new Dictionary<string, object> { ["a"] = 1, ["b"] = 2 }
        });
        result.ShouldBe("a=1,b=2,");
    }

    [Fact]
    public void RangeElse()
    {
        const string tmpl = "{{ range .Empty }}X{{ else }}Empty{{ end }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Empty"] = new int[0]
        });
        result.ShouldBe("Empty");
    }

    [Fact]
    public void RangeElseIndentation()
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
        result.ShouldBe(expected);
    }

    [Fact]
    public void NestedLoops()
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
        result.ShouldBe(expected);
    }

    [Fact]
    public void RangeIndexEmptySlice()
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
        result.ShouldBe(expected);
    }

    [Fact]
    public void BasicComment()
    {
        const string tmpl = "Hello {{/* ignore */}}World";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>());
        result.ShouldBe("Hello World");
    }

    [Fact]
    public void MissingFieldAccess()
    {
        const string tmpl = "Missing: {{ .MaybeNil.Field }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>());
        result.ShouldBe("Missing: ");
    }

    [Fact]
    public void TrimmedComment()
    {
        const string tmpl = "A {{-/* c */-}} B";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>());
        result.ShouldBe("AB");
    }

    [Fact]
    public void NestedConditions()
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

		        result.ShouldBe("Active user: Alice");
    }

    [Fact]
    public void ComplexBooleanExpression()
    {
        const string tmpl = @"{{- if and (eq .Status ""active"") (gt .Count 0) -}}
	Status is active and count is positive
{{- end -}}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Status"] = "active",
            ["Count"] = 2
        });

	        result.ShouldBe("Status is active and count is positive");
    }

    [Fact]
    public void ZeroValueCheck()
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
        result1.ShouldBe("Count: 3");

        var result2 = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Count"] = 0
        });
        result2.ShouldBe("No items");
    }

    [Fact]
    public void PipelineLower()
    {
        const string tmpl = "Name: {{ .Name | lower }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Name"] = "Alice"
        });
        result.ShouldBe("Name: alice");
    }

    [Fact]
    public void PipelinePrint()
    {
        const string tmpl = "{{ print .A .B }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["A"] = "hello",
            ["B"] = "world"
        });
        result.ShouldBe("helloworld");
    }

    [Fact]
    public void PipelinePrintf()
    {
        const string tmpl = "{{ printf \"Hi %s\" .Name }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Name"] = "Bob"
        });
        result.ShouldBe("Hi Bob");
    }

    [Fact]
    public void PipelineHtml()
    {
        const string tmpl = "{{ .Txt | html }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Txt"] = "<b>bold</b>"
        });
        result.ShouldBe("&lt;b&gt;bold&lt;/b&gt;");
    }

    [Fact]
    public void PipelineJs()
    {
        const string tmpl = "{{ .Txt | js }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Txt"] = "var x = 1 && 2;"
        });
        result.ShouldBe("var x = 1 \\u0026\\u0026 2;");
    }

    [Fact]
    public void PipelineUrlQuery()
    {
        const string tmpl = "{{ .Txt | urlquery }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Txt"] = "a b&c"
        });
        result.ShouldBe("a%20b%26c");
    }

    [Fact]
    public void PipelineChainedLowerHtml()
    {
        const string tmpl = "{{ .Name | lower | html }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Name"] = "<BOLD>"
        });
        result.ShouldBe("&lt;bold&gt;");
    }

    [Fact]
    public void PipelineChainedPrintLower()
    {
        const string tmpl = "{{ print .A .B | lower }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["A"] = "HELLO",
            ["B"] = "WORLD"
        });
        result.ShouldBe("helloworld");
    }

    [Fact]
    public void PipelineLen()
    {
        const string tmpl = "{{ len .Items }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Items"] = new[] { 1, 2, 3 }
        });
        result.ShouldBe("3");
    }

    [Fact]
    public void PipelineIndex()
    {
        const string tmpl = "{{ index .Items 1 }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Items"] = new[] { "a", "b", "c" }
        });
        result.ShouldBe("b");
    }

    [Fact]
    public void PipelineSliceString()
    {
        const string tmpl = "{{ slice .Txt 1 4 }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Txt"] = "hello"
        });
        result.ShouldBe("ell");
    }

    [Fact]
    public void PipelineCall()
    {
        const string tmpl = "{{ call \"Add\" 2 3 }}";
        TemplateEngine.RegisterFunction("Add", new Func<int, int, int>((a, b) => a + b));
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>());
        result.ShouldBe("5");
    }

    [Fact]
    public void DotNotation_VariableFormsEquivalent()
    {
        const string tmpl = "{{ Variable }}|{{ .Variable }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Variable"] = "X"
        });
        result.ShouldBe("X|X");
    }

    [Fact]
    public void Range_DotAndRootAccess()
    {
        const string tmpl = "{{ range .Items }}- {{ . }} {{ $.Answer }};{{ end }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Items"] = new[] { "a", "b" },
            ["Answer"] = "root"
        });
        result.ShouldBe("- a root;- b root;");
    }

    [Fact]
    public void Variable_Assignment_And_Usage()
    {
        const string tmpl = "{{ $greeting := \"Hi there\" }}{{ $greeting }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>());
        result.ShouldBe("Hi there");
    }

    [Fact]
    public void Range_With_DollarVariables()
    {
        const string tmpl = "{{ range $index, $item := .Items }}{{ $index }}: {{ $item }};{{ end }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Items"] = new[] { "a", "b" }
        });
        result.ShouldBe("0: a;1: b;");
    }
}

using Xunit;
using Shouldly;
using TextTemplate;
using System.Collections.Generic;

namespace TextTemplate.Tests;

public class AllFeaturesStressTests
{
    [Fact]
    public void AllFeatures_ExecutedManyTimes()
    {
        const string tmpl = @"{{$greeting := printf ""Hi %s"" .Name}}
{{ with .User }}{{ $greeting }}, {{ .Name }}!{{ else }}{{ $greeting }}{{ end }}
{{ if lt .Count 10 }}small{{ else }}big{{ end }}
Items: {{ range $i, $v := .Items }}{{ $i }}={{ $v }},{{ end }}
Len={{ len .Items }}
First={{ index .Items 0 }}
Slice={{ slice .Items 1 3 | print }}
Html={{ .Raw | html }}
Url={{ .UrlValue | urlquery }}
Sum={{ call ""Add"" 2 3 }}";

        TemplateEngine.RegisterFunction("Add", new Func<int, int, int>((a, b) => a + b));
        var model = new Dictionary<string, object>
        {
            ["Name"] = "Bob",
            ["User"] = new Dictionary<string, object> { ["Name"] = "Alice" },
            ["Count"] = 3,
            ["Items"] = new[] { "a", "b", "c" },
            ["Raw"] = "<b>x</b>",
            ["JsSrc"] = "a && b",
            ["UrlValue"] = "a b&c"
        };

        var expected = TemplateEngine.Process(tmpl, model);
        for (int i = 0; i < 1000; i++)
        {
            var result = TemplateEngine.Process(tmpl, model);
            result.ShouldBe(expected);
        }
    }
}

using Xunit;
using Shouldly;
using TextTemplate;
using System.Collections.Generic;

namespace TextTemplate.Tests;

public class WithBlockTests
{
    [Fact]
    public void WithBlock_Basic()
    {
        const string tmpl = "{{ with .User }}Name: {{ .Name }}{{ end }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["User"] = new User { Name = "Alice" }
        });
        result.ShouldBe("Name: Alice");
    }

    [Fact]
    public void WithBlock_Else()
    {
        const string tmpl = "{{ with .User }}X{{ else }}No user{{ end }}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>());
        result.ShouldBe("No user");
    }
}

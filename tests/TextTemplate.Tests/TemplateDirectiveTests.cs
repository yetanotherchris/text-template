using Xunit;
using Shouldly;
using TextTemplate;
using System.Collections.Generic;

namespace TextTemplate.Tests;

public class TemplateDirectiveTests
{
    [Fact]
    public void DefineAndTemplate_Basic()
    {
        const string tmpl = "{{define \"greet\"}}Hello, {{.Name}}!{{end}}{{template \"greet\" .User}}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["User"] = new Dictionary<string, object> { ["Name"] = "Bob" }
        });
        result.ShouldBe("Hello, Bob!");
    }

    [Fact]
    public void BlockDirective_DefaultAndOverride()
    {
        const string tmpl = "{{define \"title\"}}Custom{{end}}{{block \"title\" .Ctx}}Default{{end}}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Ctx"] = new object()
        });
        result.ShouldBe("Custom");
    }

    [Fact]
    public void BlockDirective_UsesDefaultWhenUndefined()
    {
        const string tmpl = "{{block \"title\" .Ctx}}Default Title{{end}}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Ctx"] = new object()
        });
        result.ShouldBe("Default Title");
    }
}

using Xunit;
using Shouldly;
using TextTemplate;
using System.Collections.Generic;
using System.IO;

namespace TextTemplate.Tests;

public class DefineBlockDirectiveTests
{
    [Fact]
    public void DefineWithContext_Basic()
    {
        const string tmpl = "{{define \"user\"}}Name: {{.Name}}, Age: {{.Age}}{{end}}{{template \"user\" .}}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Name"] = "John",
            ["Age"] = 30
        });
        result.ShouldBe("Name: John, Age: 30");
    }

    [Fact]
    public void DefineMultipleTemplates_Basic()
    {
        const string tmpl = "{{define \"header\"}}HEADER{{end}}{{define \"footer\"}}FOOTER{{end}}{{template \"header\"}}|{{template \"footer\"}}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>());
        result.ShouldBe("HEADER|FOOTER");
    }

    [Fact]
    public void DefineOverwrite_LastWins()
    {
        const string tmpl = "{{define \"test\"}}First{{end}}{{define \"test\"}}Second{{end}}{{template \"test\"}}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>());
        result.ShouldBe("Second");
    }

    [Fact]
    public void DefineNestedInTemplate()
    {
        const string tmpl = "{{define \"outer\"}}{{define \"inner\"}}INNER{{end}}OUTER{{template \"inner\"}}{{end}}{{template \"outer\"}}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>());
        result.ShouldBe("OUTERINNER");
    }

    [Fact]
    public void TemplateChain_Calls()
    {
        const string tmpl = "{{define \"a\"}}A{{template \"b\"}}{{end}}{{define \"b\"}}B{{template \"c\"}}{{end}}{{define \"c\"}}C{{end}}{{template \"a\"}}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>());
        result.ShouldBe("ABC");
    }

    [Fact]
    public void BlockOverrideAfterDefinition()
    {
        const string tmpl = "{{block \"test\" .Ctx}}Default{{end}}{{define \"test\"}}Override{{end}}{{block \"test\" .Ctx}}Default2{{end}}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Ctx"] = new object()
        });
        result.ShouldBe("DefaultOverride");
    }

    [Fact]
    public void BlockInBlockWithOverrides()
    {
        const string tmpl = "{{define \"inner\"}}INNER_OVERRIDE{{end}}{{block \"outer\" .Ctx}}OUTER{{block \"inner\" .Ctx}}INNER_DEFAULT{{end}}{{end}}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Ctx"] = new object()
        });
        result.ShouldBe("OUTERINNER_OVERRIDE");
    }

    [Fact]
    public void TemplateInheritance_FileTemplate()
    {
        string baseDir = AppContext.BaseDirectory;
        string templatePath = Path.Combine(baseDir, "TestData", "inheritance_template.txt");
        string expectedPath = Path.Combine(baseDir, "TestData", "inheritance_expected.txt");

        string template = File.ReadAllText(templatePath);
        string expected = File.ReadAllText(expectedPath);

        var result = TemplateEngine.Process(template, new Dictionary<string, object>
        {
            ["Ctx"] = new object(),
            ["DefaultTitle"] = "Default",
            ["CustomTitle"] = "Custom",
            ["DefaultContent"] = "DefContent",
            ["CustomContent"] = "CustContent"
        });

        result.ShouldBe(expected);
    }
}

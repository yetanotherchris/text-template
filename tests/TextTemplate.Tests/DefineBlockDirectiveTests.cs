using Xunit;
using Shouldly;
using TextTemplate;
using System.Collections.Generic;
using System.IO;

namespace TextTemplate.Tests;

public class DefineBlockDirectiveTests
{
    [Fact]
    public void DefineWithContext()
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
    public void DefineMultipleTemplates()
    {
        const string tmpl = "{{define \"header\"}}HEADER{{end}}{{define \"footer\"}}FOOTER{{end}}{{template \"header\"}}|{{template \"footer\"}}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>());
        result.ShouldBe("HEADER|FOOTER");
    }

    [Fact]
    public void BlockWithContext()
    {
        const string tmpl = "{{block \"greeting\" .Ctx}}Hello {{.Name}}{{end}}";
        var result = TemplateEngine.Process(tmpl, new Dictionary<string, object>
        {
            ["Ctx"] = new Dictionary<string, object> { ["Name"] = "World" }
        });
        result.ShouldBe("Hello World");
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
    public void FileTemplate_DefineAndBlock()
    {
        string baseDir = AppContext.BaseDirectory;
        string templatePath = Path.Combine(baseDir, "TestData", "define_block_template.txt");
        string expectedPath = Path.Combine(baseDir, "TestData", "define_block_expected.txt");
        string template = File.ReadAllText(templatePath);
        string expected = File.ReadAllText(expectedPath);
        var result = TemplateEngine.Process(template, new Dictionary<string, object>
        {
            ["Ctx"] = new Dictionary<string, object> { ["DefaultTitle"] = "Default Title" }
        });
        result.ShouldBe(expected);
    }
}

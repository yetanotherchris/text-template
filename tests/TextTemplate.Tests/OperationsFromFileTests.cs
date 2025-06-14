using Xunit;
using Shouldly;
using TextTemplate;
using System.Collections.Generic;
using System.IO;

namespace TextTemplate.Tests;

public class OperationsFromFileTests
{
    [Fact]
    public void FileTemplate_Operations()
    {
        string baseDir = AppContext.BaseDirectory;
        string templatePath = Path.Combine(baseDir, "TestData", "operations_template.txt");
        string expectedPath = Path.Combine(baseDir, "TestData", "operations_expected.txt");

        string template = File.ReadAllText(templatePath);
        string expected = File.ReadAllText(expectedPath);

        var result = TemplateEngine.Process(template, new Dictionary<string, object>
        {
            ["User"] = new Dictionary<string, object>
            {
                ["Name"] = "Alice",
                ["IsActive"] = true,
                ["HasPermission"] = true
            },
            ["DefaultValue"] = "",
            ["UserValue"] = "Custom",
            ["Status"] = "active",
            ["Name"] = "Alice",
            ["MessageCount"] = 5,
            ["UserComment"] = "<b>Hello</b>",
            ["SearchQuery"] = "foo bar&baz",
            ["Items"] = new[] { "apple", "banana", "cherry" },
            ["ItemCount"] = 3
        });

        result.ShouldBe(expected);
    }
}

using Xunit;
using TextTemplate;
using System.Collections.Generic;
using System.IO;

namespace TextTemplate.Tests;

public class FullTemplateFromFileTests
{
    [Fact]
    public void AntlrTemplate_FileTemplate_AllFeatures()
    {
        string baseDir = AppContext.BaseDirectory;
        string templatePath = Path.Combine(baseDir, "TestData", "full_template.txt");
        string expectedPath = Path.Combine(baseDir, "TestData", "full_template_expected.txt");

        string template = File.ReadAllText(templatePath);
        string expected = File.ReadAllText(expectedPath);

        var result = TemplateEngine.Process(template, new Dictionary<string, object>
        {
            ["Name"] = "Bob",
            ["Gift"] = "toaster",
            ["Attended"] = false,
            ["Items"] = new[] { "book", "pen" }
        });

        Assert.Equal(expected, result);
    }
}

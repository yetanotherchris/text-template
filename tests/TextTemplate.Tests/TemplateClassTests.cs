using Shouldly;
using TextTemplate;
using Xunit;
using System.IO;

namespace TextTemplate.Tests;

public class TemplateClassTests
{
    [Fact]
    public void NewParseExecutePattern_Works()
    {
        var tmpl = Template.New("t").Parse("Hello {{ .Name }}! {{ range .Items }}{{ . }} {{ end }}");
        var result = tmpl.Execute(new { Name = "Bob", Items = new[] { "a", "b" } });
        result.ShouldBe("Hello Bob! a b ");
    }

    [Fact]
    public void ParseFiles_ReadsAndParsesAllFiles()
    {
        var f1 = Path.GetTempFileName();
        var f2 = Path.GetTempFileName();
        var f3 = Path.GetTempFileName();
        try
        {
            File.WriteAllText(f1, "{{define \"header\"}}Hello {{.Name}}{{end}}");
            File.WriteAllText(f2, "{{define \"exclaim\"}}!{{end}}");
            File.WriteAllText(f3, "{{template \"header\" .}}{{template \"exclaim\"}}");

            var tmpl = Template.New("t").ParseFiles(f1, f2, f3);
            var result = tmpl.Execute(new { Name = "Ann" });
            result.ShouldBe("Hello Ann!");
        }
        finally
        {
            File.Delete(f1);
            File.Delete(f2);
            File.Delete(f3);
        }
    }
}

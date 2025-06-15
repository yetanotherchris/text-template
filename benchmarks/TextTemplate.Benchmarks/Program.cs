using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using TextTemplate;
using HandlebarsDotNet;
using Scriban;
using DotLiquid;
using ScribanTemplateClass = Scriban.Template;
using DotLiquidTemplateClass = DotLiquid.Template;
using Hbs = HandlebarsDotNet.Handlebars;

public class TemplateBenchmarks
{
    private const string TTTemplate = "Hello {{ .Name }}! {{ range .Items }}{{ . }} {{ end }}";
    private const string HBTemplate = "Hello {{Name}}! {{#each Items}}{{this}} {{/each}}";
    private const string ScribanTmpl = "Hello {{name}}! {{ for item in items }}{{item}} {{end}}";
    private const string DotLiquidTmpl = "Hello {{Name}}! {% for item in Items %}{{item}} {% endfor %}";

    private Dictionary<string, object> _model = null!;

    [GlobalSetup]
    public void Setup()
    {
        _model = new Dictionary<string, object>
        {
            ["Name"] = "Bob",
            ["Items"] = new List<string> { "one", "two", "three", "four", "five" }
        };
        DotLiquidTemplateClass.NamingConvention = new DotLiquid.NamingConventions.CSharpNamingConvention();
    }

    [Benchmark]
    public string GoTextTemplate() => TemplateEngine.Process(TTTemplate, _model);

    [Benchmark]
    public string Handlebars() => Hbs.Compile(HBTemplate)(_model);

    [Benchmark]
    public string Scriban() => ScribanTemplateClass.Parse(ScribanTmpl).Render(_model);

    [Benchmark]
    public string DotLiquid() => DotLiquidTemplateClass.Parse(DotLiquidTmpl).Render(Hash.FromDictionary(_model));
}

public class Program
{
    public static void Main(string[] args)
    {
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}

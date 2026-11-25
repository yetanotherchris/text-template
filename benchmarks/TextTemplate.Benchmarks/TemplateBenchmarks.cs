using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using DotLiquid;
using HandlebarsDotNet;
using DotLiquidTemplateClass = DotLiquid.Template;
using Hbs = HandlebarsDotNet.Handlebars;
using ScribanTemplateClass = Scriban.Template;
using TTTemplate = TextTemplate.Template;

public class TemplateBenchmarks
{
    private const string TTTemplateText = "Hello {{ .Name }}! {{ range .Items }}{{ . }} {{ end }}";
    private const string HBTemplate = "Hello {{Name}}! {{#each Items}}{{this}} {{/each}}";
    private const string ScribanTmpl = "Hello {{name}}! {{ for item in items }}{{item}} {{end}}";
    private const string DotLiquidTmpl = "Hello {{Name}}! {% for item in Items %}{{item}} {% endfor %}";

    private Dictionary<string, object> _model = null!;
    private TTTemplate _ttCompiled = null!;
    private HandlebarsTemplate<object, object> _hbCompiled = null!;
    private ScribanTemplateClass _scribanCompiled = null!;
    private DotLiquidTemplateClass _liquidCompiled = null!;

    [GlobalSetup]
    public void Setup()
    {
        _model = new Dictionary<string, object>
        {
            ["Name"] = "Bob",
            ["Items"] = new List<string> { "one", "two", "three", "four", "five" }
        };
        DotLiquidTemplateClass.NamingConvention = new DotLiquid.NamingConventions.CSharpNamingConvention();

        _ttCompiled = TTTemplate.New("bench").Parse(TTTemplateText);
        _hbCompiled = Hbs.Compile(HBTemplate);
        _scribanCompiled = ScribanTemplateClass.Parse(ScribanTmpl);
        _liquidCompiled = DotLiquidTemplateClass.Parse(DotLiquidTmpl);
    }

    [Benchmark]
    public string GoTextTemplate() => _ttCompiled.Execute(_model);

    [Benchmark]
    public string Handlebars() => _hbCompiled(_model);

    [Benchmark]
    public string Scriban() => _scribanCompiled.Render(_model);

    [Benchmark]
    public string DotLiquid() => _liquidCompiled.Render(Hash.FromDictionary(_model));
}

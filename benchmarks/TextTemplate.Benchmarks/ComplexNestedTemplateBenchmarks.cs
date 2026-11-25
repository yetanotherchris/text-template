using System;
using System.Collections.Generic;
using System.IO;
using BenchmarkDotNet.Attributes;
using DotLiquid;
using HandlebarsDotNet;
using TextTemplate.Tests;
using DotLiquidTemplateClass = DotLiquid.Template;
using Hbs = HandlebarsDotNet.Handlebars;
using ScribanTemplateClass = Scriban.Template;
using TTTemplate = TextTemplate.Template;

[MemoryDiagnoser]
public class ComplexNestedTemplateBenchmarks
{
    private TTTemplate _tmpl = null!;
    private HandlebarsTemplate<object, object> _hbCompiled = null!;
    private ScribanTemplateClass _scribanCompiled = null!;
    private DotLiquidTemplateClass _liquidCompiled = null!;
    private Dictionary<string, object> _values = null!;

    [GlobalSetup]
    public void Setup()
    {
        string baseDir = AppContext.BaseDirectory;
        string depPath = Path.Combine(baseDir, "TestData", "complex-template-deployment.yml");
        string svcPath = Path.Combine(baseDir, "TestData", "complex-template-service.yml");
        string ingPath = Path.Combine(baseDir, "TestData", "complex-template-ingress.yml");
        string rootPath = Path.Combine(baseDir, "TestData", "complex-template-data.yml");
        string hbPath = Path.Combine(baseDir, "TestData", "complex-template.hbs");
        string scribanPath = Path.Combine(baseDir, "TestData", "complex-template.scriban");
        string liquidPath = Path.Combine(baseDir, "TestData", "complex-template.liquid");

        DotLiquidTemplateClass.NamingConvention = new DotLiquid.NamingConventions.CSharpNamingConvention();

        _tmpl = TTTemplate.New("complex").ParseFiles(depPath, svcPath, ingPath, rootPath);
        _hbCompiled = Hbs.Compile(File.ReadAllText(hbPath));
        _scribanCompiled = ScribanTemplateClass.Parse(File.ReadAllText(scribanPath));
        _liquidCompiled = DotLiquidTemplateClass.Parse(File.ReadAllText(liquidPath));
        _values = ComplexNestedTemplateData.Create();
    }

    [Benchmark]
    public string GoTextTemplate_NET() => _tmpl.Execute(_values);

    [Benchmark]
    public string Handlebars() => _hbCompiled(_values);

    [Benchmark]
    public string Scriban() => _scribanCompiled.Render(_values);

    [Benchmark]
    public string DotLiquid() => _liquidCompiled.Render(Hash.FromDictionary(_values));
}

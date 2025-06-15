using System;
using System.IO;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using TextTemplate;
using TTTemplate = TextTemplate.Template;
using TextTemplate.Tests;
using HandlebarsDotNet;
using Scriban;
using DotLiquid;
using ScribanTemplateClass = Scriban.Template;
using DotLiquidTemplateClass = DotLiquid.Template;
using Hbs = HandlebarsDotNet.Handlebars;

public class ComplexNestedTemplateBenchmarks
{
    private TTTemplate _tmpl = null!;
    private string _hbText = null!;
    private string _scribanText = null!;
    private string _liquidText = null!;
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

        _tmpl = TTTemplate.New("complex").ParseFiles(depPath, svcPath, ingPath, rootPath);
        _hbText = File.ReadAllText(hbPath);
        _scribanText = File.ReadAllText(scribanPath);
        _liquidText = File.ReadAllText(liquidPath);
        DotLiquidTemplateClass.NamingConvention = new DotLiquid.NamingConventions.CSharpNamingConvention();
        _values = ComplexNestedTemplateData.Create();
    }

    [Benchmark]
    public string GoTextTemplate_NET() => _tmpl.Execute(_values);

    [Benchmark]
    public string Handlebars()
    {
        var compiled = Hbs.Compile(_hbText);
        return compiled(_values);
    }

    [Benchmark]
    public string Scriban()
    {
        var tmpl = ScribanTemplateClass.Parse(_scribanText);
        return tmpl.Render(_values);
    }

    [Benchmark]
    public string DotLiquid()
    {
        var tmpl = DotLiquidTemplateClass.Parse(_liquidText);
        return tmpl.Render(Hash.FromDictionary(_values));
    }
}


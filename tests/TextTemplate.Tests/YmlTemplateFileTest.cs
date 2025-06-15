using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Shouldly;
using TextTemplate;
using Xunit;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace TextTemplate.Tests;

public class YmlTemplateFileTest
{
    private static string NormalizeYaml(string text)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(text));
        var serializer = new SerializerBuilder().JsonCompatible().Build();
        var sb = new StringBuilder();
        foreach (var doc in stream.Documents)
        {
            sb.AppendLine(serializer.Serialize(doc.RootNode));
        }
        return sb.ToString();
    }

    [Fact]
    public void Template_YamlFile_RendersExpectedOutput()
    {
        string baseDir = AppContext.BaseDirectory;
        string templatePath = Path.Combine(baseDir, "TestData", "single-complex-data.yml");
        string expectedPath = Path.Combine(baseDir, "TestData", "single-complex-expected.yml");

        string template = File.ReadAllText(templatePath);
        string expected = File.ReadAllText(expectedPath);

        var values = ComplexNestedTemplateData.Create();

        string result = TemplateEngine.Process(template, values);

        string normalizedResult = NormalizeYaml(result);
        string normalizedExpected = NormalizeYaml(expected);

        normalizedResult.ShouldBe(normalizedExpected);
    }

    [Fact]
    public void Template_YamlFilesWithSubTemplates_RendersExpectedOutput()
    {
        string baseDir = AppContext.BaseDirectory;
        string depPath = Path.Combine(baseDir, "TestData", "complex-template-deployment.yml");
        string svcPath = Path.Combine(baseDir, "TestData", "complex-template-service.yml");
        string ingPath = Path.Combine(baseDir, "TestData", "complex-template-ingress.yml");
        string rootPath = Path.Combine(baseDir, "TestData", "complex-template-data.yml");
        string expectedPath = Path.Combine(baseDir, "TestData", "complex-template-expected.yml");

        var tmpl = Template.New("complex").ParseFiles(depPath, svcPath, ingPath, rootPath);
        string expected = File.ReadAllText(expectedPath);

        var values = ComplexNestedTemplateData.Create();

        string result = tmpl.Execute(values);
        string normalizedResult = NormalizeYaml(result);
        string normalizedExpected = NormalizeYaml(expected);

        normalizedResult.ShouldBe(normalizedExpected);
    }
}

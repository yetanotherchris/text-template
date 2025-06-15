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
        string templatePath = Path.Combine(baseDir, "TestData", "single-k8s-data.yml");
        string expectedPath = Path.Combine(baseDir, "TestData", "single-k8s-expected.yml");

        string template = File.ReadAllText(templatePath);
        string expected = File.ReadAllText(expectedPath);

        var values = new Dictionary<string, object>
        {
            ["Values"] = new Dictionary<string, object>
            {
                ["appName"] = "my-web-app",
                ["namespace"] = "production",
                ["version"] = "\"1.2.3\"",
                ["environment"] = "prod",
                ["customLabels"] = new Dictionary<string, object>
                {
                    ["team"] = "backend",
                    ["cost-center"] = "engineering",
                    ["project"] = "web-platform"
                },
                ["replicaCount"] = 5,
                ["image"] = new Dictionary<string, object>
                {
                    ["repository"] = "myregistry.com/my-web-app",
                    ["tag"] = "v1.2.3"
                },
                ["service"] = new Dictionary<string, object>
                {
                    ["enabled"] = true,
                    ["type"] = "LoadBalancer",
                    ["port"] = 80,
                    ["targetPort"] = 8080,
                    ["nodePort"] = 30080
                },
                ["env"] = new object[]
                {
                    new Dictionary<string, object>{{"name","DATABASE_URL"},{"value","postgresql://db.example.com:5432/myapp"}},
                    new Dictionary<string, object>{{"name","REDIS_HOST"},{"value","redis.example.com"}},
                    new Dictionary<string, object>{{"name","LOG_LEVEL"},{"value","info"}},
                    new Dictionary<string, object>{{"name","API_KEY"},{"value","secret-api-key-123"}}
                },
                ["resources"] = new Dictionary<string, object>
                {
                    ["limits"] = new Dictionary<string, object>
                    {
                        ["cpu"] = "\"1000m\"",
                        ["memory"] = "\"1Gi\""
                    },
                    ["requests"] = new Dictionary<string, object>
                    {
                        ["cpu"] = "\"500m\"",
                        ["memory"] = "\"512Mi\""
                    }
                },
                ["volumeMounts"] = new object[]
                {
                    new Dictionary<string, object>{{"name","config-volume"},{"mountPath","/etc/config"},{"readOnly","true"}},
                    new Dictionary<string, object>{{"name","secret-volume"},{"mountPath","/etc/secrets"},{"readOnly","true"}},
                    new Dictionary<string, object>{{"name","data-volume"},{"mountPath","/var/data"}}
                },
                ["volumes"] = new object[]
                {
                    new Dictionary<string, object>{{"name","config-volume"},{"configMap",new Dictionary<string, object>{{"name","my-web-app-config"}}}},
                    new Dictionary<string, object>{{"name","secret-volume"},{"secret",new Dictionary<string, object>{{"secretName","my-web-app-secrets"}}}},
                    new Dictionary<string, object>{{"name","data-volume"},{"persistentVolumeClaim",new Dictionary<string, object>{{"claimName","my-web-app-data"}}}}
                },
                ["nodeSelector"] = new Dictionary<string, object>
                {
                    ["kubernetes.io/os"] = "linux",
                    ["node-type"] = "web-tier"
                },
                ["tolerations"] = new object[]
                {
                    new Dictionary<string, object>{{"key","\"node-type\""},{"operator","\"Equal\""},{"value","\"web-tier\""},{"effect","\"NoSchedule\""}},
                    new Dictionary<string, object>{{"key","\"dedicated\""},{"operator","\"Equal\""},{"value","\"web-app\""},{"effect","\"NoExecute\""}}
                },
                ["ingress"] = new Dictionary<string, object>
                {
                    ["enabled"] = true,
                    ["annotations"] = new Dictionary<string, object>
                    {
                        ["kubernetes.io/ingress.class"] = "nginx",
                        ["cert-manager.io/cluster-issuer"] = "letsencrypt-prod",
                        ["nginx.ingress.kubernetes.io/rewrite-target"] = "/"
                    },
                    ["tls"] = new object[]
                    {
                        new Dictionary<string, object>
                        {
                            ["hosts"] = new object[]{"myapp.example.com","api.myapp.example.com"},
                            ["secretName"] = "myapp-tls-cert"
                        }
                    },
                    ["hosts"] = new object[]
                    {
                        new Dictionary<string, object>
                        {
                            ["host"] = "myapp.example.com",
                            ["paths"] = new object[]
                            {
                                new Dictionary<string, object>{{"path","/"},{"pathType","Prefix"}},
                                new Dictionary<string, object>{{"path","/api"},{"pathType","Prefix"}}
                            }
                        },
                        new Dictionary<string, object>
                        {
                            ["host"] = "api.myapp.example.com",
                            ["paths"] = new object[]
                            {
                                new Dictionary<string, object>{{"path","/"},{"pathType","Prefix"}}
                            }
                        }
                    }
                }
            }
        };

        string result = TemplateEngine.Process(template, values);

        string normalizedResult = NormalizeYaml(result);
        string normalizedExpected = NormalizeYaml(expected);

        normalizedResult.ShouldBe(normalizedExpected);
    }

    [Fact]
    public void Template_YamlFilesWithSubTemplates_RendersExpectedOutput()
    {
        string baseDir = AppContext.BaseDirectory;
        string depPath = Path.Combine(baseDir, "TestData", "k8s-template-deployment.yml");
        string svcPath = Path.Combine(baseDir, "TestData", "k8s-template-service.yml");
        string ingPath = Path.Combine(baseDir, "TestData", "k8s-template-ingress.yml");
        string rootPath = Path.Combine(baseDir, "TestData", "k8s-template-data.yml");
        string expectedPath = Path.Combine(baseDir, "TestData", "k8s-template-expected.yml");

        var tmpl = Template.New("k8s").ParseFiles(depPath, svcPath, ingPath, rootPath);
        string expected = File.ReadAllText(expectedPath);

        var values = new Dictionary<string, object>
        {
            ["Values"] = new Dictionary<string, object>
            {
                ["appName"] = "my-web-app",
                ["namespace"] = "production",
                ["version"] = "\"1.2.3\"",
                ["environment"] = "prod",
                ["customLabels"] = new Dictionary<string, object>
                {
                    ["team"] = "backend",
                    ["cost-center"] = "engineering",
                    ["project"] = "web-platform"
                },
                ["replicaCount"] = 5,
                ["image"] = new Dictionary<string, object>
                {
                    ["repository"] = "myregistry.com/my-web-app",
                    ["tag"] = "v1.2.3"
                },
                ["service"] = new Dictionary<string, object>
                {
                    ["enabled"] = true,
                    ["type"] = "LoadBalancer",
                    ["port"] = 80,
                    ["targetPort"] = 8080,
                    ["nodePort"] = 30080
                },
                ["env"] = new object[]
                {
                    new Dictionary<string, object>{{"name","DATABASE_URL"},{"value","postgresql://db.example.com:5432/myapp"}},
                    new Dictionary<string, object>{{"name","REDIS_HOST"},{"value","redis.example.com"}},
                    new Dictionary<string, object>{{"name","LOG_LEVEL"},{"value","info"}},
                    new Dictionary<string, object>{{"name","API_KEY"},{"value","secret-api-key-123"}}
                },
                ["resources"] = new Dictionary<string, object>
                {
                    ["limits"] = new Dictionary<string, object>
                    {
                        ["cpu"] = "\"1000m\"",
                        ["memory"] = "\"1Gi\""
                    },
                    ["requests"] = new Dictionary<string, object>
                    {
                        ["cpu"] = "\"500m\"",
                        ["memory"] = "\"512Mi\""
                    }
                },
                ["volumeMounts"] = new object[]
                {
                    new Dictionary<string, object>{{"name","config-volume"},{"mountPath","/etc/config"},{"readOnly","true"}},
                    new Dictionary<string, object>{{"name","secret-volume"},{"mountPath","/etc/secrets"},{"readOnly","true"}},
                    new Dictionary<string, object>{{"name","data-volume"},{"mountPath","/var/data"}}
                },
                ["volumes"] = new object[]
                {
                    new Dictionary<string, object>{{"name","config-volume"},{"configMap",new Dictionary<string, object>{{"name","my-web-app-config"}}}},
                    new Dictionary<string, object>{{"name","secret-volume"},{"secret",new Dictionary<string, object>{{"secretName","my-web-app-secrets"}}}},
                    new Dictionary<string, object>{{"name","data-volume"},{"persistentVolumeClaim",new Dictionary<string, object>{{"claimName","my-web-app-data"}}}}
                },
                ["nodeSelector"] = new Dictionary<string, object>
                {
                    ["kubernetes.io/os"] = "linux",
                    ["node-type"] = "web-tier"
                },
                ["tolerations"] = new object[]
                {
                    new Dictionary<string, object>{{"key","\"node-type\""},{"operator","\"Equal\""},{"value","\"web-tier\""},{"effect","\"NoSchedule\""}},
                    new Dictionary<string, object>{{"key","\"dedicated\""},{"operator","\"Equal\""},{"value","\"web-app\""},{"effect","\"NoExecute\""}}
                },
                ["ingress"] = new Dictionary<string, object>
                {
                    ["enabled"] = true,
                    ["annotations"] = new Dictionary<string, object>
                    {
                        ["kubernetes.io/ingress.class"] = "nginx",
                        ["cert-manager.io/cluster-issuer"] = "letsencrypt-prod",
                        ["nginx.ingress.kubernetes.io/rewrite-target"] = "/"
                    },
                    ["tls"] = new object[]
                    {
                        new Dictionary<string, object>
                        {
                            ["hosts"] = new object[]{"myapp.example.com","api.myapp.example.com"},
                            ["secretName"] = "myapp-tls-cert"
                        }
                    },
                    ["hosts"] = new object[]
                    {
                        new Dictionary<string, object>
                        {
                            ["host"] = "myapp.example.com",
                            ["paths"] = new object[]
                            {
                                new Dictionary<string, object>{{"path","/"},{"pathType","Prefix"}},
                                new Dictionary<string, object>{{"path","/api"},{"pathType","Prefix"}}
                            }
                        },
                        new Dictionary<string, object>
                        {
                            ["host"] = "api.myapp.example.com",
                            ["paths"] = new object[]
                            {
                                new Dictionary<string, object>{{"path","/"},{"pathType","Prefix"}}
                            }
                        }
                    }
                }
            }
        };

        string result = tmpl.Execute(values);
        string normalizedResult = NormalizeYaml(result);
        string normalizedExpected = NormalizeYaml(expected);

        normalizedResult.ShouldBe(normalizedExpected);
    }
}

package main

import (
    "testing"
    "text/template"
    "bytes"
)

var goTmpl = template.Must(template.New("t").Parse("Hello {{ .Name }}! {{ range .Items }}{{ . }} {{ end }}"))
var goData = map[string]any{
    "Name":  "Bob",
    "Items": []string{"one", "two", "three", "four", "five"},
}

var complexTmpl = template.New("complex")

func init() {
    base := "testdata/"
    template.Must(complexTmpl.ParseFiles(base+"complex-template-deployment.yml", base+"complex-template-service.yml", base+"complex-template-ingress.yml", base+"complex-template-data.yml"))
}

func BenchmarkGoTextTemplate(b *testing.B) {
    for i := 0; i < b.N; i++ {
        var buf bytes.Buffer
        goTmpl.Execute(&buf, goData)
        _ = buf.String()
    }
}

func BenchmarkGoComplexTemplate(b *testing.B) {
    data := map[string]any{"Values": complexData()}
    for i := 0; i < b.N; i++ {
        var buf bytes.Buffer
        complexTmpl.Execute(&buf, data)
        _ = buf.String()
    }
}


func BenchmarkGoComplexTemplate(b *testing.B) {
    values := map[string]any{
        "Values": complexData(),
    }
    for i := 0; i < b.N; i++ {
        var buf bytes.Buffer
        complexTmpl.Execute(&buf, values)
        _ = buf.String()
    }
}

// complexData returns the same nested dictionary used by the C# benchmarks.
// It is constructed inline so the benchmark does not rely on file I/O.
func complexData() map[string]any {
    return map[string]any{
        "appName": "my-web-app",
        "namespace": "production",
        "version": "\"1.2.3\"",
        "environment": "prod",
        "customLabels": map[string]any{
            "team":        "backend",
            "cost-center": "engineering",
            "project":     "web-platform",
        },
        "replicaCount": 5,
        "image": map[string]any{
            "repository": "myregistry.com/my-web-app",
            "tag":        "v1.2.3",
        },
        "service": map[string]any{
            "enabled":    true,
            "type":       "LoadBalancer",
            "port":       80,
            "targetPort": 8080,
            "nodePort":   30080,
        },
        "env": []map[string]any{
            {"name": "DATABASE_URL", "value": "postgresql://db.example.com:5432/myapp"},
            {"name": "REDIS_HOST", "value": "redis.example.com"},
            {"name": "LOG_LEVEL", "value": "info"},
            {"name": "API_KEY", "value": "secret-api-key-123"},
        },
        "resources": map[string]any{
            "limits": map[string]any{"cpu": "\"1000m\"", "memory": "\"1Gi\""},
            "requests": map[string]any{"cpu": "\"500m\"", "memory": "\"512Mi\""},
        },
        "volumeMounts": []map[string]any{
            {"name": "config-volume", "mountPath": "/etc/config", "readOnly": true},
            {"name": "secret-volume", "mountPath": "/etc/secrets", "readOnly": true},
            {"name": "data-volume", "mountPath": "/var/data"},
        },
        "volumes": []map[string]any{
            {"name": "config-volume", "configMap": map[string]any{"name": "my-web-app-config"}},
            {"name": "secret-volume", "secret": map[string]any{"secretName": "my-web-app-secrets"}},
            {"name": "data-volume", "persistentVolumeClaim": map[string]any{"claimName": "my-web-app-data"}},
        },
        "nodeSelector": map[string]any{
            "kubernetes.io/os": "linux",
            "node-type":       "web-tier",
        },
        "tolerations": []map[string]any{
            {"key": "\"node-type\"", "operator": "\"Equal\"", "value": "\"web-tier\"", "effect": "\"NoSchedule\""},
            {"key": "\"dedicated\"", "operator": "\"Equal\"", "value": "\"web-app\"", "effect": "\"NoExecute\""},
        },
        "ingress": map[string]any{
            "enabled": true,
            "annotations": map[string]any{
                "kubernetes.io/ingress.class":              "nginx",
                "cert-manager.io/cluster-issuer":            "letsencrypt-prod",
                "nginx.ingress.kubernetes.io/rewrite-target": "/",
            },
            "tls": []map[string]any{
                {"hosts": []any{"myapp.example.com", "api.myapp.example.com"}, "secretName": "myapp-tls-cert"},
            },
            "hosts": []map[string]any{
                {"host": "myapp.example.com", "paths": []map[string]any{
                    {"path": "/", "pathType": "Prefix"},
                    {"path": "/api", "pathType": "Prefix"},
                }},
                {"host": "api.myapp.example.com", "paths": []map[string]any{
                    {"path": "/", "pathType": "Prefix"},
                }},
            },
        },
    }
}


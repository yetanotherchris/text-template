package main

import (
	"bytes"
	"fmt"
	"os"
	"reflect"
	"sort"
	"strconv"
	"strings"
	"testing"
	"text/template"
)

var goTmpl = template.Must(template.New("t").Parse("Hello {{ .Name }}! {{ range .Items }}{{ . }} {{ end }}"))
var goData = map[string]any{
	"Name":  "Bob",
	"Items": []string{"one", "two", "three", "four", "five"},
}

var complexTmpl = template.New("complex").Funcs(template.FuncMap{
	"default": func(def, val interface{}) interface{} {
		if val == nil {
			return def
		}
		rv := reflect.ValueOf(val)
		if rv.Kind() == reflect.Ptr && rv.IsNil() {
			return def
		}
		if rv.IsZero() {
			return def
		}
		return val
	},
	"quote": func(v interface{}) string {
		if v == nil {
			return "\"\""
		}
		return strconv.Quote(fmt.Sprintf("%v", v))
	},
})

func init() {
	base := "testdata/"
	template.Must(complexTmpl.ParseFiles(base+"complex-template-deployment.yml", base+"complex-template-service.yml", base+"complex-template-ingress.yml", base+"complex-template-data.yml"))
}

func normalizeLines(s string) []string {
	s = strings.ReplaceAll(s, "\r\n", "\n")
	raw := strings.Split(s, "\n")
	out := make([]string, 0, len(raw))
	for _, line := range raw {
		trimmed := strings.TrimSpace(line)
		if trimmed != "" {
			out = append(out, trimmed)
		}
	}
	sort.Strings(out)
	return out
}

func TestGoTextTemplateOutput(t *testing.T) {
	var buf bytes.Buffer
	if err := goTmpl.Execute(&buf, goData); err != nil {
		t.Fatal(err)
	}
	const expected = "Hello Bob! one two three four five "
	if got := buf.String(); got != expected {
		t.Fatalf("unexpected output:\ngot:  %q\nwant: %q", got, expected)
	}
}

func TestGoComplexTemplateOutput(t *testing.T) {
	values := map[string]any{"Values": complexData()}
	var buf bytes.Buffer
	if err := complexTmpl.ExecuteTemplate(&buf, "complex-template-data.yml", values); err != nil {
		t.Fatal(err)
	}
	expectedBytes, err := os.ReadFile("testdata/complex-template-expected.yml")
	if err != nil {
		t.Fatal(err)
	}
	gotLines := normalizeLines(buf.String())
	wantLines := normalizeLines(string(expectedBytes))
	if !reflect.DeepEqual(gotLines, wantLines) {
		t.Fatalf("rendered YAML does not match complex-template-expected.yml (got %d non-empty lines, want %d)", len(gotLines), len(wantLines))
	}
}

func BenchmarkGoTextTemplate(b *testing.B) {
	for i := 0; i < b.N; i++ {
		var buf bytes.Buffer
		if err := goTmpl.Execute(&buf, goData); err != nil {
			b.Fatal(err)
		}
		_ = buf.String()
	}
}

func BenchmarkGoComplexTemplate(b *testing.B) {
	values := map[string]any{"Values": complexData()}
	for i := 0; i < b.N; i++ {
		var buf bytes.Buffer
		if err := complexTmpl.ExecuteTemplate(&buf, "complex-template-data.yml", values); err != nil {
			b.Fatal(err)
		}
		_ = buf.String()
	}
}

// complexData returns the same nested dictionary used by the C# benchmarks.
// It is constructed inline so the benchmark does not rely on file I/O.
func complexData() map[string]any {
	return map[string]any{
		"appName":     "my-web-app",
		"namespace":   "production",
		"version":     "\"1.2.3\"",
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
			"limits":   map[string]any{"cpu": "\"1000m\"", "memory": "\"1Gi\""},
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
			"node-type":        "web-tier",
		},
		"tolerations": []map[string]any{
			{"key": "\"node-type\"", "operator": "\"Equal\"", "value": "\"web-tier\"", "effect": "\"NoSchedule\""},
			{"key": "\"dedicated\"", "operator": "\"Equal\"", "value": "\"web-app\"", "effect": "\"NoExecute\""},
		},
		"ingress": map[string]any{
			"enabled": true,
			"annotations": map[string]any{
				"kubernetes.io/ingress.class":                "nginx",
				"cert-manager.io/cluster-issuer":             "letsencrypt-prod",
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

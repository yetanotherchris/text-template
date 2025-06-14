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

func BenchmarkGoTextTemplate(b *testing.B) {
    for i := 0; i < b.N; i++ {
        var buf bytes.Buffer
        goTmpl.Execute(&buf, goData)
        _ = buf.String()
    }
}

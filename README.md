# text/template
See if Codex can port Go's template package to C#:

- https://pkg.go.dev/text/template#pkg-overview
- https://cs.opensource.google/go/go/+/refs/tags/go1.24.4:src/text/template/template.go

This repository now only includes a minimal example. The
`TemplateEngine.Process` helper performs simple variable substitution by
parsing templates with the `GoTemplateLexer` and `GoTemplateParser`.

Supported built-in functions include `eq`, `ne`, numeric comparisons `lt`, `le`,
`gt`, `ge`, logical operations `and`, `or` and negation `not`.

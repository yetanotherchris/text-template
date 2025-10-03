# text/template

[![NuGet](https://img.shields.io/nuget/v/go-text-template.svg)](https://www.nuget.org/packages/go-text-template/)

text/template is a C# implementation of Go's template engine using ANTLR for parsing. It began as an experiment to see whether OpenAI Codex could port the Go implementation to .NET. Claude AI helped with explanations and refinements along the way, but the prompts were from my own knowledge of Antlr and grammars, and obviously C#.  

It ported the Go code to begin with, but then I realised Antlr would probably be a safer and more readable alternative than a straight Go ---> C# port.

This README itself was also largely authored using Codex. Internally the engine uses an ANTLR-generated lexer and parser.

The original Go package can be found here:

- https://pkg.go.dev/text/template#pkg-overview
- https://cs.opensource.google/go/go/+/refs/tags/go1.24.4:src/text/template/template.go

This library now contains virtually all functionality from the original Go text/template package. 

## Usage

```csharp
var tmpl = Template.New("hello").Parse("Hello {{ .Name }}!");
var result = tmpl.Execute(new { Name = "World" });
Console.WriteLine(result); // Hello World!
```


## Features

- Replace `{{ variable }}` placeholders with values from dictionaries or model
  objects.
- Conditional blocks with `if`, `else if` and `else` clauses.
- `for` loops and Go-style `range` loops over arrays, collections and maps.
- Built-in functions: `eq`, `ne`, numeric comparisons (`lt`, `le`, `gt`, `ge`),
  logical operators (`and`, `or`, `not`) supporting multiple arguments.
- Basic pipelines with the `lower` function for transforming output, and `call` to invoke registered functions.
- Declare variables with `{{ $name := value }}` and reference them later using `$name`.
- Access nested properties, map keys and indexes, including dynamic indexing via
  variables.
- Whitespace trimming with `{{-` and `-}}` and comment syntax `{{/* ... */}}`.
- Support for `with`, `define`, `template` and `block` directives.

## Template reference

```
// -- 1. Variable Interpolation
// Access properties
{{ .Property }}

// Nested property access
{{ .User.Name }}

// Index arrays or slices
{{ .Items[0] }}

// Access map entries
{{ .Data.key }}

// Control whitespace
{{- .Name -}}
// Declare and use a variable
{{ $name := "Hi there" }}{{ $name }}

// -- 2. Conditional Statements
// Basic if blocks
{{ if condition }}...{{ end }}

// if/else blocks
{{ if condition }}...{{ else }}...{{ end }}

// else if chains
{{ if condition }}...{{ else if other }}...{{ end }}

// Supported conditions include
{{ if .IsActive }}
{{ if eq .Status "active" }}
{{ if .User }}

// -- 3. Loop Statements
// Iterate slices or arrays
{{ range .Items }}...{{ end }}

// Capture index/value
{{ range $i, $v := .Items }}...{{ end }}
// Range with index/item variables
{{ range $index, $item := .Items }}{{ $index }}: {{ $item }}{{ end }}

// Iterate maps
{{ range .Map }}...{{ end }}

// Map key/value variables
{{ range $k, $v := .Map }}...{{ end }}

// Handle empty collections
{{ range .Items }}...{{ else }}...{{ end }}

// -- 4. Built-in Functions
// Equality and inequality
// eq, ne

// Numeric comparisons
// lt, le, gt, ge

// Logical operators
// and, or, not

// Registered functions can be invoked via call
{{ call "Add" 1 2 }}

const string template = "{{ call \"Add\" 1 2 }}";
Template.RegisterFunction("Add", new Func<int, int, int>((a, b) => a + b));
var result = Template.New("calc").Parse(template).Execute(new {});
// result == "3"

// -- 5. Comments
// Embedding comments
{{/* comment */}}

// -- 6. Pipelines
// Chaining functions with |
{{ .Name | lower }}

// Available pipeline helpers include
// lower - convert to lowercase
// print - concatenate values using default formatting
// printf - printf-style formatting using SprintfFormatter
// html - HTML escape the value
// js - JavaScript escape the value
// urlquery - escape for URL query parameters
// len - length of a collection or string
// index - retrieve an element by index or key
// slice - slice strings or lists
// call - invoke a function value
```

### Example template usage

```csharp
// Define a named template using conditionals and a range loop
string tmpl = @"
{{ define \"letter\" }}
Dear {{ .Name }},
{{ if .Attended }}
It was a pleasure to see you.
{{ else }}
Sorry you couldn't make it.
{{ end }}
You brought:
{{ range .Items }}- {{ . }}
{{ end }}
Thank you for the lovely {{ .Gift }}.
{{ end }}
{{ template \"letter\" . }}";

// Execute the template with a model
var output = Template.New("letter").Parse(tmpl).Execute(new
{
    Name = "Bob",
    Gift = "toaster",
    Attended = false,
    Items = new[] { "book", "pen" }
});

// Example output:
// Dear Bob,
// Sorry you couldn't make it.
// You brought:
// - book
// - pen
// Thank you for the lovely toaster.
Console.WriteLine(output);
```

### Template Definitions

```csharp
string tmpl = @"
{{ define \"user\" }}
Name: {{ .Name }}
Age: {{ .Age }}
{{ end }}
{{ template \"user\" . }}";
var userResult = Template.New("user").Parse(tmpl).Execute(new { Name = "Jane", Age = 42 });
// userResult == "Name: Jane\nAge: 42\n"
```

### Calling Functions with `call`

```csharp
Template.RegisterFunction("Add", new Func<int, int, int>((a, b) => a + b));
string callTmpl = "{{ call \"Add\" 2 3 }}";
string callResult = Template.New("calc").Parse(callTmpl).Execute(new {});
// callResult == "5"
```
See the unit tests for more examples covering loops, conditionals and range expressions. The `YmlTemplateFileTest` shows how to render a full Kubernetes manifest from `tests/TestData/template.yml` with the expected output in `tests/TestData/expected.yml`.

## Not Implemented Yet

- Custom delimiter support.

## Benchmark Results

The following benchmarks were run using [BenchmarkDotNet](https://benchmarkdotnet.org/) on .NET 9.0, `cpu: AMD Ryzen 7 5700X 8-Core Processor`. Each benchmark renders the same short template:

```text
Hello {{ .Name }}! {{ range .Items }}{{ . }} {{ end }}
```

```bash
dotnet run -c Release --project benchmarks/TextTemplate.Benchmarks -- --filter "TemplateBenchmarks*"
```

The model contains five strings in the `Items` list so every engine performs a small loop. BenchmarkDotNet ran each test using its default configuration which executes a warm‑up phase followed by enough iterations (13–96 in our runs) to collect roughly one second of timing data. The Go implementation was benchmarked with `go test -bench .` using the equivalent template and data.

| Method         | Mean        | Error     | StdDev    |
|--------------- |------------:|----------:|----------:|
| GoTextTemplate |    15.28 us |  0.238 us |  0.222 us |
| Handlebars.net | 1,721.10 us | 29.683 us | 26.313 us |
| Scriban        |    15.36 us |  0.304 us |  0.482 us |
| DotLiquid      |    12.98 us |  0.162 us |  0.151 us |
| Go text/template | 2,167 ns  | (505,638 iterations ) |

### Advanced Scenario Benchmarks

The benchmark suite also includes `ComplexNestedTemplateBenchmarks`. This test
loads the Kubernetes-style YAML templates found under `tests/TestData` and
executes them as a single nested template. Run the .NET benchmarks with:

```bash
dotnet run -c Release --project benchmarks/TextTemplate.Benchmarks -- --filter "ComplexNestedTemplateBenchmarks*"
```

The Go implementation can be benchmarked separately with:

```bash
go test -bench BenchmarkGoComplexTemplate ./benchmarks/go -benchmem
```

Example results on a small container:

| Method             | Mean        | Error     | StdDev    |
|------------------- |------------:|----------:|----------:|
| GoTextTemplate_NET |    172.0 us |   3.19 us |   2.98 us |
| Handlebars.net     | 47,411.7 us | 679.98 us | 602.78 us |
| Scriban            |    195.4 us |   3.07 us |   3.01 us |
| DotLiquid          |    417.5 us |   6.07 us |   5.38 us |
| Go text/template | 2,167 ns  | (1,218,702 iterations ) |

## Claude's suggestions
https://gist.github.com/yetanotherchris/c80d0fadb5a2ee5b4beb0a4384020dbf

## License

This project is released under the MIT license. Source code was produced by
OpenAI Codex with assistance from Claude.AI.
This README was written using OpenAI Codex.

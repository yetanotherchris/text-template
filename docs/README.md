# text/template

[![NuGet](https://img.shields.io/nuget/v/go-text-template.svg)](https://www.nuget.org/packages/go-text-template/)

text/template is a C# implementation of Go's template engine using ANTLR for parsing. 

The library began as an experiment to see whether OpenAI Codex could port the Go implementation to .NET. Claude AI helped with explanations and refinements along the way, but the prompts were from my own knowledge of Antlr and grammars, and obviously C#. The port was originally done using OpenAI Codex (ChatGPT 4x) and Claude (4.x), and big optimisations using Gemini 4 Neon.

The library is now 8-9x faster than Go's text/template, and lower memory usage.

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

The following benchmarks were run using [BenchmarkDotNet](https://benchmarkdotnet.org/) on .NET 11.0, `cpu: AMD Ryzen 7 5700X 8-Core Processor`. Each benchmark renders the same short template:

```text
Hello {{ .Name }}! {{ range .Items }}{{ . }} {{ end }}
```

```bash
dotnet run -c Release --project benchmarks/TextTemplate.Benchmarks -- --filter "TemplateBenchmarks*"
```

The model contains five strings in the `Items` list so every engine performs a small loop. Each engine parses/compiles its template once in `[GlobalSetup]` so the benchmarks measure rendering performance. BenchmarkDotNet ran each test using its default configuration, which executes a warm-up phase followed by enough iterations to collect roughly one second of timing data. The Go implementation can be benchmarked with `go test -bench .` using the equivalent template and data.

*Note:* the libraries being compared don't have all the functionality of text/template, handlebars for example doesn't allow logic in the templates.

| Method           | Mean       | Error     | StdDev    | Allocated |
|----------------- |-----------:|----------:|----------:|----------:|
| this library     |   122.6 ns |   1.04 ns |   0.92 ns |     424 B |
| Handlebars.Net   |   471.2 ns |   1.09 ns |   1.02 ns |      96 B |
| Go text/template | 1,144.5 ns |       n/a |       n/a |     464 B |
| DotLiquid        | 3,318.3 ns |  18.39 ns |  17.20 ns |  12,768 B |
| Scriban          | 7,878.2 ns | 155.91 ns | 145.84 ns |  34,350 B |

### Advanced Scenario Benchmarks

The benchmark suite also includes `ComplexNestedTemplateBenchmarks`. This test
loads the Kubernetes-style YAML templates found under `tests/TestData` and
executes them as a single nested template. Run the .NET benchmarks with:

```bash
dotnet run -c Release --project benchmarks/TextTemplate.Benchmarks -- --filter "ComplexNestedTemplateBenchmarks*"
```

The Go implementation can be benchmarked separately with:

```bash
go test -bench BenchmarkGoComplexTemplate ./benchmarks/go -benchmem -count=10
```

Results on the same machine (all engines pre-parsed/compiled in `[GlobalSetup]` / `init()`, `[MemoryDiagnoser]` enabled, Go median of 10 runs):

| Method           | Mean      | Error     | StdDev    | Allocated |
|----------------- |----------:|----------:|----------:|----------:|
| this library     |  5.448 us | 0.0206 us | 0.0182 us |  18.89 KB |
| Handlebars.Net   | 12.191 us | 0.0332 us | 0.0311 us |   7.64 KB |
| Scriban          | 40.216 us | 0.7686 us | 1.0261 us |  74.01 KB |
| Go text/template | 43.090 us |       n/a |       n/a |  22.05 KB |
| DotLiquid        | 94.172 us | 0.5013 us | 0.4689 us | 223.62 KB |

### Porting notes

The Go code was ported to begin with, but then I realised Antlr would probably be a safer and more readable alternative than a direct Go to C# AST port. Internally the engine uses an ANTLR-generated lexer and parser, with optimisations using a lightweight custom AST by Gemini 4 Neon.

The original Go package can be found here:

- https://pkg.go.dev/text/template#pkg-overview
- https://cs.opensource.google/go/go/+/refs/tags/go1.24.4:src/text/template/template.go

The library contains virtually all functionality from the original Go text/template package. 

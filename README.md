# Jackson

A .NET library for JSON parsing built on top of System.Text.Json, offering advanced type discrimination.

## Motivation

Handling objects with complex discrimination requirements in JSON deserialization can be difficult. While
System.Text.Json supports basic deserialization well, it often requires cumbersome and error-prone custom converters for
more complex logic.

This library simplifies the process by providing an easy solution for deserializing JSON data into objects with complex
discrimination needs. By extending System.Text.Json's capabilities, it allows developers to seamlessly deserialize JSON,
even with intricate discrimination requirements.

With this library, developers can map JSON data to their object models efficiently, without needing extensive custom
converter implementations. This results in a simpler and more maintainable deserialization process.

## Get Started

Install package from NuGet:
```shell
dotnet add package Jackson
```

Build a parser to map JSON data to .NET types:
```csharp
using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using Jackson;

public class Type1;
public class Type2;

var json =
    """
    [
        { "f1": "type", "f2": "1" },
        { "f1": "type", "f2": "2" },
        { "f1": "1", "f2": "type" },
        { "f1": "2", "f2": "type" },
        { "f1": "dynamic", "f2": [1, 2, 3], "f3": { "f1": "type", "f2": "1" } }
    ]
    """;

var parser = ParserBuilder
    .Create(JsonSerializerOptions.Default, "f1", "f2")
    .Map<Type1>("type", "1")
    .Map<Type2>("type", "2")
    .Or(
        ParserBuilder
            .Create(JsonSerializerOptions.Default, "f1", "f2")
            .Map<Type1>("1", "type")
            .Map<Type2>("2", "type")
            .Build()
    )
    .Build();

var node = JsonSerializer.Deserialize<JsonNode>(json);

if (parser.Parse(node) is IEnumerable<object> seq)
{
    foreach (object item in seq)
    {
        Console.WriteLine(item);
    }
}
```

## License

This project is licensed under the [MIT License](LICENSE).

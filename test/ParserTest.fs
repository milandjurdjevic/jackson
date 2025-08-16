module Jackson.Tests.ParserTest

open System.Diagnostics.CodeAnalysis
open System.Text.Json
open System.Text.Json.Nodes
open VerifyXunit
open Xunit
open Jackson

type Type1() =
    member _.Id = nameof Type1

type Type2() =
    member _.Id = nameof Type2

[<StringSyntax("json")>]
let json =
    """
[
    {
        "f1": "type",
        "f2": "1"
    },
    {
        "f1": "type",
        "f2": "2"
    },
    {
        "f1": "1",
        "f2": "type"
    },
    {
        "f1": "2",
        "f2": "type"
    },
    {
        "f1": "dynamic",
        "f2": [1, 2, 3],
        "f3": {
            "f1": "type",
            "f2": "1"
        }
    }
]
    """

[<Fact>]
let ``parse JSON node to mapped or dynamic type`` () =
    let parser =
        Parser
            .Create(JsonSerializerOptions.Default, "f1", "f2")
            .Map<Type1>("type", "1")
            .Map<Type2>("type", "2")
            .Chain(
                Parser
                    .Create(JsonSerializerOptions.Default, "f1", "f2")
                    .Map<Type1>("1", "type")
                    .Map<Type2>("2", "type")
                    .Build()
            )
            .Build()

    json
    |> JsonSerializer.Deserialize<JsonNode>
    |> parser.Parse
    |> Verifier.Verify
    |> _.ToTask()
    |> Async.AwaitTask

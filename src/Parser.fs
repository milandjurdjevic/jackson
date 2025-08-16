namespace Jackson

open System
open System.Text.Json
open System.Text.Json.Nodes

type IParser =
    abstract member Parse: node: JsonNode -> objnull

module internal Parser =
    let create parser =
        { new IParser with
            member this.Parse(node) = parser node }

    let (|Object|Array|Int|Double|String|Boolean|Null|) (node: JsonNode) =
        match node with
        | :? JsonObject as obj -> Object obj
        | :? JsonArray as arr -> Array arr
        | :? JsonValue as value ->
            match value.GetValueKind() with
            | JsonValueKind.Number ->
                match (value.TryGetValue<int>(), value.TryGetValue<double>()) with
                | (true, int), _ -> Int int
                | _, (true, double) -> Double double
                | (false, _), (false, _) -> failwith $"Unable to parse {value.GetValue<string>()} as number."
            | JsonValueKind.String -> String(value.GetValue<string>())
            | JsonValueKind.True -> Boolean true
            | JsonValueKind.False -> Boolean false
            | JsonValueKind.Null -> Null
            | JsonValueKind.Undefined -> Null
            | unsupported -> failwith $"Unsupported value kind '{unsupported.ToString()}'."
        | null -> Null
        | unsupported -> failwith $"Unsupported node type '{unsupported.GetType().Name}'."

    let unstructured: IParser =
        let rec parse =
            function
            | Object obj -> obj |> Seq.map (fun node -> node.Key, parse node.Value) |> readOnlyDict |> box
            | Array arr -> arr |> Seq.map parse |> Seq.toArray |> box
            | Int num -> box num
            | Double num -> box num
            | String str -> box str
            | Boolean bool -> box bool
            | Null -> null

        create parse

    let structured (keys: string array) (map: Map<string, Type>) (opt: JsonSerializerOptions) (nxt: IParser) : IParser =
        let rec parse =
            function
            | Object obj ->
                keys
                |> Seq.map (fun key ->
                    obj
                    |> Seq.tryFind _.Key.Equals(key, StringComparison.OrdinalIgnoreCase)
                    |> Option.map _.Value.ToString())
                |> Seq.choose id
                |> String.concat String.Empty
                |> map.TryFind
                |> Option.map (fun t -> obj.Deserialize(t, opt))
                |> Option.defaultValue (nxt.Parse obj)
            | Array arr -> arr |> Seq.map parse |> Seq.toArray |> box
            | Int num -> box num
            | Double num -> box num
            | String str -> box str
            | Boolean bool -> box bool
            | Null -> null

        create parse


type Parser
    private (keys: string array, map: Map<string, Type>, serializer: JsonSerializerOptions, next: IParser option) =

    static member Create(options, [<ParamArray>] keys: string array) = Parser(keys, Map.empty, options, None)

    member _.Map<'T>([<ParamArray>] values: string array) =
        Parser(keys, map.Add((String.Join(String.Empty, values), typeof<'T>)), serializer, next)

    member _.Chain(next: IParser) =
        Parser(keys, map, serializer, Some next)

    member _.Build() : IParser =
        let next =
            next |> Option.map _.Parse |> Option.defaultValue Parser.unstructured.Parse

        Parser.structured keys map serializer (Parser.create next)

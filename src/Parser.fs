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
        let rec parse (node: JsonNode) (cps: obj -> obj) : obj =
            match node with
            | Object obj ->
                let enumerator = obj.GetEnumerator()

                let rec loop (acc: (string * obj) list) : obj =
                    if enumerator.MoveNext() then
                        let current = enumerator.Current
                        parse current.Value (fun v -> loop ((current.Key, v) :: acc))
                    else
                        acc |> List.rev |> readOnlyDict |> box |> cps

                loop []
            | Array arr ->
                let enumerator = arr.GetEnumerator()

                let rec loop (acc: obj list) : obj =
                    if enumerator.MoveNext() then
                        parse enumerator.Current (fun v -> loop (v :: acc))
                    else
                        acc |> List.rev |> List.toArray |> box |> cps

                loop []
            | Int num -> cps (box num)
            | Double num -> cps (box num)
            | String str -> cps (box str)
            | Boolean bool -> cps (box bool)
            | Null -> cps null

        create (fun n -> parse n id)

    let structured (keys: string array) (map: Map<string, Type>) (opt: JsonSerializerOptions) (nxt: IParser) : IParser =
        let rec parse (json: JsonNode) (cps: obj -> obj) : obj =
            match json with
            | Object obj ->
                let keyStr =
                    keys
                    |> Seq.map (fun key ->
                        obj
                        |> Seq.tryFind _.Key.Equals(key, StringComparison.OrdinalIgnoreCase)
                        |> Option.map _.Value.ToString())
                    |> Seq.choose id
                    |> String.concat String.Empty

                match map.TryFind keyStr with
                | Some t -> obj.Deserialize(t, opt) |> cps
                | None -> nxt.Parse obj |> cps
            | Array arr ->
                let enumerator = arr.GetEnumerator()

                let rec loop (acc: obj list) : obj =
                    if enumerator.MoveNext() then
                        parse enumerator.Current (fun v -> loop (v :: acc))
                    else
                        acc |> List.rev |> List.toArray |> box |> cps

                loop []
            | Int num -> cps (box num)
            | Double num -> cps (box num)
            | String str -> cps (box str)
            | Boolean bool -> cps (box bool)
            | Null -> cps null

        create (fun node -> parse node id)

type Parser private (keys: string array, map: Map<string, Type>, opt: JsonSerializerOptions, next: IParser option) =

    static member Create(options, [<ParamArray>] keys: string array) = Parser(keys, Map.empty, options, None)

    member _.Map<'T>([<ParamArray>] values: string array) =
        Parser(keys, map.Add((String.Join(String.Empty, values), typeof<'T>)), opt, next)

    member _.Chain(next: IParser) = Parser(keys, map, opt, Some next)

    member _.Build() : IParser =
        let next =
            next |> Option.map _.Parse |> Option.defaultValue Parser.unstructured.Parse

        Parser.structured keys map opt (Parser.create next)

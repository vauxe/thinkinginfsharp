namespace ThinkingInFSharp.Ch19

open System
open System.Collections.Generic

module NullBoundaries =
    type BoundaryTextError =
        | MissingText
        | BlankText

    let createAbsoluteUri (raw: string) : Uri =
        Uri(raw, UriKind.Absolute)

    let uriHost (uri: Uri) : string =
        uri.Host

    let joinLabels (labels: string array) : string =
        String.Join(" / ", labels)

    let countItems (items: IReadOnlyCollection<'T>) : int =
        items.Count

    let requireText (raw: string | null) : Result<string, BoundaryTextError> =
        match raw with
        | Null -> Error MissingText
        | NonNull value when String.IsNullOrWhiteSpace value -> Error BlankText
        | NonNull value -> Ok(value.Trim())

    let tryResolveType (typeName: string) : Type option =
        Type.GetType(typeName, throwOnError = false)
        |> Option.ofObj

    let nullableIntToOption (value: Nullable<int>) : int option =
        Option.ofNullable value

    let optionToNullableInt (value: int option) : Nullable<int> =
        Option.toNullable value

    let nullableTextToOption (value: string | null) : string option =
        Option.ofObj value

    let optionToNullableText (value: string option) : string | null =
        Option.toObj value

    let someNullText : (string | null) option =
        Some null

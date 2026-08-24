namespace ThinkingInFSharp.Ch19

open System
open System.Collections.Generic

module NullBoundaries =
    // #region boundary-errors
    type BoundaryTextError =
        | MissingText
        | BlankText
    // #endregion boundary-errors

    // #region dotnet-calls
    let createAbsoluteUri (raw: string) : Uri =
        Uri(raw, UriKind.Absolute)

    let uriHost (uri: Uri) : string =
        uri.Host

    let joinLabels (labels: string array) : string =
        String.Join(" / ", labels)

    let countItems (items: IReadOnlyCollection<'T>) : int =
        items.Count
    // #endregion dotnet-calls

    // #region nullable-input
    let requireText (raw: string | null) : Result<string, BoundaryTextError> =
        match raw with
        | Null -> Error MissingText
        | NonNull value when String.IsNullOrWhiteSpace value -> Error BlankText
        | NonNull value -> Ok(value.Trim())
    // #endregion nullable-input

    // #region nullable-return
    let tryResolveType (typeName: string) : Type option =
        Type.GetType(typeName, throwOnError = false)
        |> Option.ofObj
    // #endregion nullable-return

    // #region nullable-value-conversions
    let nullableIntToOption (value: Nullable<int>) : int option =
        Option.ofNullable value

    let optionToNullableInt (value: int option) : Nullable<int> =
        Option.toNullable value
    // #endregion nullable-value-conversions

    // #region nullable-reference-conversions
    let nullableTextToOption (value: string | null) : string option =
        Option.ofObj value

    let optionToNullableText (value: string option) : string | null =
        Option.toObj value
    // #endregion nullable-reference-conversions

    // #region some-null
    let someNullText : (string | null) option =
        Some null
    // #endregion some-null

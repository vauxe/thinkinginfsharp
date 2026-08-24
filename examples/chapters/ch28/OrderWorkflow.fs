namespace ThinkingInFSharp.Ch28

open System
open System.Text.Json
open System.Text.Json.Serialization

// #region domain-values
type CommandError =
    | MissingOrderId
    | BlankOrderId
    | MissingSku
    | BlankSku
    | NonPositiveQuantity of quantity: int

type PlaceOrderCommand =
    private
        { OrderId: string
          Sku: string
          Quantity: int }

module PlaceOrderCommand =
    let create (orderId: string | null) (sku: string | null) quantity =
        match orderId with
        | null -> Error MissingOrderId
        | value when String.IsNullOrWhiteSpace value -> Error BlankOrderId
        | validOrderId ->
            match sku with
            | null -> Error MissingSku
            | value when String.IsNullOrWhiteSpace value -> Error BlankSku
            | _ when quantity <= 0 -> Error(NonPositiveQuantity quantity)
            | validSku ->
                Ok
                    { OrderId = validOrderId.Trim()
                      Sku = validSku.Trim()
                      Quantity = quantity }

    let orderId command = command.OrderId
    let sku command = command.Sku
    let quantity command = command.Quantity

type ProductSnapshot =
    private
        { Sku: string
          UnitPrice: decimal
          Available: int }

module ProductSnapshot =
    let create (sku: string) unitPrice available =
        ArgumentNullException.ThrowIfNull(sku, nameof sku)

        if String.IsNullOrWhiteSpace sku then
            raise (ArgumentException("SKU must not be blank.", nameof sku))

        if unitPrice < 0M then
            raise (
                ArgumentOutOfRangeException(
                    nameof unitPrice,
                    unitPrice,
                    "Unit price must not be negative."
                )
            )

        if available < 0 then
            raise (
                ArgumentOutOfRangeException(
                    nameof available,
                    available,
                    "Available stock must not be negative."
                )
            )

        { Sku = sku.Trim()
          UnitPrice = unitPrice
          Available = available }

type OrderDraft =
    { OrderId: string
      Sku: string
      Quantity: int
      Total: decimal }

type OrderDecisionError =
    | ProductNotFound of sku: string
    | InsufficientStock of requested: int * available: int
// #endregion domain-values

// #region pure-decision
module OrderDecision =
    let decide
        (product: ProductSnapshot option)
        (command: PlaceOrderCommand)
        : Result<OrderDraft, OrderDecisionError> =
        let requestedSku = PlaceOrderCommand.sku command
        let requestedQuantity = PlaceOrderCommand.quantity command

        match product with
        | None -> Error(ProductNotFound requestedSku)
        | Some snapshot
            when not (StringComparer.Ordinal.Equals(snapshot.Sku, requestedSku)) ->
            Error(ProductNotFound requestedSku)
        | Some snapshot when requestedQuantity > snapshot.Available ->
            Error(InsufficientStock(requestedQuantity, snapshot.Available))
        | Some snapshot ->
            Ok
                { OrderId = PlaceOrderCommand.orderId command
                  Sku = requestedSku
                  Quantity = requestedQuantity
                  Total = decimal requestedQuantity * snapshot.UnitPrice }
// #endregion pure-decision

// #region ports-workflow
type PlacedOrder =
    { OrderId: string
      Sku: string
      Quantity: int
      Total: decimal
      PlacedAt: DateTimeOffset }

type OrderPorts =
    { FindProduct: string -> ProductSnapshot option
      GetUtcNow: unit -> DateTimeOffset
      SaveOrder: PlacedOrder -> unit }

module OrderWorkflow =
    let place
        (ports: OrderPorts)
        (command: PlaceOrderCommand)
        : Result<PlacedOrder, OrderDecisionError> =
        let product = command |> PlaceOrderCommand.sku |> ports.FindProduct

        match OrderDecision.decide product command with
        | Error error -> Error error
        | Ok draft ->
            let placed =
                { OrderId = draft.OrderId
                  Sku = draft.Sku
                  Quantity = draft.Quantity
                  Total = draft.Total
                  PlacedAt = ports.GetUtcNow() }

            ports.SaveOrder placed
            Ok placed
// #endregion ports-workflow

// #region json-boundary
[<CLIMutable>]
type PlaceOrderDto =
    { OrderId: string | null
      Sku: string | null
      Quantity: int }

type DtoError =
    | MissingBody
    | InvalidCommand of CommandError

module PlaceOrderDto =
    let toCommand (dto: PlaceOrderDto | null) =
        match dto with
        | null -> Error MissingBody
        | value ->
            PlaceOrderCommand.create value.OrderId value.Sku value.Quantity
            |> Result.mapError InvalidCommand

module PlaceOrderJson =
    let private options =
        let settings = JsonSerializerOptions()
        settings.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
        settings.PropertyNameCaseInsensitive <- false
        settings.UnmappedMemberHandling <- JsonUnmappedMemberHandling.Disallow
        settings

    let serialize (dto: PlaceOrderDto) =
        ArgumentNullException.ThrowIfNull(dto, nameof dto)
        JsonSerializer.Serialize(dto, options)

    let deserialize (json: string) : PlaceOrderDto | null =
        ArgumentNullException.ThrowIfNull(json, nameof json)
        JsonSerializer.Deserialize<PlaceOrderDto>(json, options)
// #endregion json-boundary

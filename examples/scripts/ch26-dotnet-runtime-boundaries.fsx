open System
open System.Collections.Generic

let ensureEqual label expected actual =
    if actual <> expected then
        failwithf "%s: expected %A, got %A" label expected actual

type BookingRequest = { RequestId: string; Seats: int }

// #region runtime-types
let request = { RequestId = "R-26"; Seats = 3 }

let declaredType = typeof<BookingRequest>
let boxedRequest: objnull = box request

let actualType =
    match boxedRequest with
    | null -> failwith "boxing a non-null record unexpectedly produced null"
    | value -> value.GetType()

ensureEqual "runtime type" declaredType actualType
printfn "Runtime type: declared=%s actual=%s" declaredType.Name actualType.Name
// #endregion runtime-types

// #region type-tests
let describeObject (value: objnull) =
    match value with
    | null -> "null"
    | :? string as text -> $"text:{text.ToUpperInvariant()}"
    | :? BookingRequest as booking -> $"request:{booking.RequestId}/{booking.Seats}"
    | :? int as number -> $"int:{number}"
    | _ -> "other"

let descriptions = [ box "lin"; box request; box 42 ] |> List.map describeObject

ensureEqual "pattern casts" [ "text:LIN"; "request:R-26/3"; "int:42" ] descriptions

printfn "Pattern casts: %A" descriptions

let failedDowncast =
    try
        let _: string | null = (box 42 :?> (string | null))
        "no-error"
    with :? InvalidCastException as error ->
        error.GetType().Name

ensureEqual "failed downcast" "InvalidCastException" failedDowncast
printfn "Failed downcast: %s" failedDowncast
// #endregion type-tests

// #region delegates
let add = Func<int, int, int>(fun left right -> left + right)

let labels =
    Array.ConvertAll([| 1; 2; 3 |], Converter<int, string>(fun number -> string (number * 2)))

ensureEqual "delegate invocation" 7 (add.Invoke(3, 4))
ensureEqual "delegate conversion" [| "2"; "4"; "6" |] labels
printfn "Delegates: add=%d labels=%A" (add.Invoke(3, 4)) labels
// #endregion delegates

// #region events
type SeatsChangedEventArgs(previous: int, current: int) =
    inherit EventArgs()

    member _.Previous = previous
    member _.Current = current

type CapacityPublisher(initial: int) =
    let changed = Event<EventHandler<SeatsChangedEventArgs>, SeatsChangedEventArgs>()
    let mutable current = initial

    [<CLIEvent>]
    member _.SeatsChanged = changed.Publish

    member this.SetSeats(next: int) =
        let previous = current
        current <- next
        changed.Trigger(this, SeatsChangedEventArgs(previous, next))

let publisher = CapacityPublisher(4)
let observations = ResizeArray<string>()

let handler =
    EventHandler<SeatsChangedEventArgs>(fun sender args ->
        assert (obj.ReferenceEquals(sender, publisher))
        observations.Add($"{args.Previous}->{args.Current}"))

publisher.SeatsChanged.AddHandler handler
publisher.SetSeats 2
publisher.SeatsChanged.RemoveHandler handler
publisher.SetSeats 1

let observedChanges = observations |> Seq.toList
ensureEqual "removed handler" [ "4->2" ] observedChanges
printfn "Event: observed=%A after-remove=%d" observedChanges observations.Count
// #endregion events

// #region dotnet-collections
let mutableNumbers = ResizeArray<int>([ 1; 2 ])
let liveView: IEnumerable<int> = mutableNumbers
let snapshot = liveView |> Seq.toList
mutableNumbers.Add 3
let liveValues = liveView |> Seq.toList

ensureEqual "live enumerable" [ 1; 2; 3 ] liveValues
ensureEqual "list snapshot" [ 1; 2 ] snapshot
printfn ".NET list: live=%A snapshot=%A" liveValues snapshot

let bookingByEmail = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
bookingByEmail["lin@example.com"] <- "first"
bookingByEmail["LIN@EXAMPLE.COM"] <- "second"
let found, emailValue = bookingByEmail.TryGetValue "Lin@Example.com"

ensureEqual "case-insensitive key count" 1 bookingByEmail.Count
ensureEqual "case-insensitive lookup" (true, "second") (found, emailValue)

printfn "String comparer: count=%d found=%b value=%s" bookingByEmail.Count found emailValue
// #endregion dotnet-collections

// #region identity-hash-keys
type Customer(customerId: string) =
    member _.CustomerId = customerId

let customerIdComparer: IEqualityComparer<Customer> =
    HashIdentity.FromFunctions
        (fun customer -> StringComparer.Ordinal.GetHashCode(customer.CustomerId))
        (fun left right -> StringComparer.Ordinal.Equals(left.CustomerId, right.CustomerId))

let firstCustomer = Customer("C-26")
let secondCustomer = Customer("C-26")
let sameReference = obj.ReferenceEquals(firstCustomer, secondCustomer)

let defaultKeys = Dictionary<Customer, string>()
defaultKeys[firstCustomer] <- "first"
defaultKeys[secondCustomer] <- "second"

let domainKeys = Dictionary<Customer, string>(customerIdComparer)
domainKeys[firstCustomer] <- "first"
domainKeys[secondCustomer] <- "second"

ensureEqual "separate references" false sameReference
ensureEqual "default class keys" 2 defaultKeys.Count
ensureEqual "domain class keys" 1 domainKeys.Count
ensureEqual "domain lookup" "second" domainKeys[firstCustomer]

printfn
    "Class keys: same-reference=%b default=%d domain=%d value=%s"
    sameReference
    defaultKeys.Count
    domainKeys.Count
    domainKeys[firstCustomer]
// #endregion identity-hash-keys

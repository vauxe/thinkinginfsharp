namespace ThinkingInFSharp.ContractTests

open System
open System.IO
open System.Text
open System.Threading
open Booking.Contracts
open Booking.Domain
open Booking.Infrastructure
open Xunit

module BookingStoreContractTests =
    type private TemporaryDirectory() =
        let directoryPath =
            Path.Combine(Path.GetTempPath(), "thinking-in-fsharp", Guid.NewGuid().ToString("N"))

        do Directory.CreateDirectory(directoryPath) |> ignore

        member _.Path = directoryPath

        interface IDisposable with
            member _.Dispose() =
                if Directory.Exists directoryPath then
                    Directory.Delete(directoryPath, true)

    let private expectOk result =
        match result with
        | Ok value -> value
        | Error error -> failwithf "Expected Ok, received Error %A" error

    let private createPending requestId =
        let eventId = EventId.create "EVT-K08B" |> expectOk
        let capacity = Capacity.create 8 |> expectOk
        let seats = SeatCount.create 3 |> expectOk
        let validRequestId = RequestId.create requestId |> expectOk
        Booking.create (Event.create eventId capacity) validRequestId seats |> expectOk

    let private confirm booking =
        let code = ConfirmationCode.create "CONF-K08B" |> expectOk
        Booking.confirm code booking |> expectOk

    let private configuration path =
        BookingStoreConfiguration.create path |> expectOk

    let private save (store: FileBookingStore) booking =
        store.Save(booking, CancellationToken.None).GetAwaiter().GetResult() |> expectOk

    let private load (store: FileBookingStore) =
        store.Load(CancellationToken.None).GetAwaiter().GetResult()

    [<Fact>]
    let ``configuration requires a file path and normalizes it`` () =
        use temporary = new TemporaryDirectory()
        let snapshotPath = Path.Combine(temporary.Path, "state", "bookings.json")

        Assert.Equal(Error BookingStoreConfigurationError.MissingSnapshotPath, BookingStoreConfiguration.create null)
        Assert.Equal(Error BookingStoreConfigurationError.MissingSnapshotPath, BookingStoreConfiguration.create "  ")

        Assert.Equal(
            Error BookingStoreConfigurationError.InvalidSnapshotPath,
            BookingStoreConfiguration.create temporary.Path
        )

        let configured = configuration snapshotPath
        Assert.Equal(Path.GetFullPath snapshotPath, BookingStoreConfiguration.snapshotPath configured)
        Assert.Equal("BOOKING_STORE_PATH", BookingStoreConfiguration.PathEnvironmentVariable)

    [<Fact>]
    let ``a saved dto is real json and restores the protected booking`` () =
        use temporary = new TemporaryDirectory()
        let snapshotPath = Path.Combine(temporary.Path, "nested", "booking.json")
        let store = FileBookingStore(configuration snapshotPath)
        let booking = createPending "REQ-SAVED"

        save store booking

        Assert.True(File.Exists snapshotPath)
        let json = File.ReadAllText(snapshotPath, Encoding.UTF8)
        let dto = BookingJson.deserializeBooking json
        let expectedDto: BookingDto | null = BookingMapping.ofDomain booking
        Assert.Equal(expectedDto, dto)
        Assert.Equal(Ok(Some booking), load store)

    [<Fact>]
    let ``a second save replaces the complete snapshot without temporary residue`` () =
        use temporary = new TemporaryDirectory()
        let snapshotPath = Path.Combine(temporary.Path, "booking.json")
        let store = FileBookingStore(configuration snapshotPath)
        let pending = createPending "REQ-REPLACED"
        let confirmed = confirm pending

        save store pending
        save store confirmed

        Assert.Equal(Ok(Some confirmed), load store)
        Assert.Empty(Directory.GetFiles(temporary.Path, "*.tmp"))

        let persisted = File.ReadAllText(snapshotPath, Encoding.UTF8)
        Assert.Contains("\"status\":\"confirmed\"", persisted)
        Assert.DoesNotContain("\"status\":\"pending\"", persisted)

    [<Fact>]
    let ``a missing snapshot is an empty store`` () =
        use temporary = new TemporaryDirectory()
        let snapshotPath = Path.Combine(temporary.Path, "missing", "booking.json")
        let store = FileBookingStore(configuration snapshotPath)

        Assert.Equal(Ok None, load store)
        Assert.False(Directory.Exists(Path.GetDirectoryName snapshotPath))

    [<Fact>]
    let ``invalid json and invalid utf8 are distinct corrupt snapshots`` () =
        use temporary = new TemporaryDirectory()
        let snapshotPath = Path.Combine(temporary.Path, "booking.json")
        let store = FileBookingStore(configuration snapshotPath)

        File.WriteAllText(snapshotPath, "{not-json", Encoding.UTF8)

        Assert.Equal(Error(BookingStoreError.CorruptSnapshot SnapshotCorruption.InvalidJson), load store)

        File.WriteAllBytes(snapshotPath, [| 0xC3uy; 0x28uy |])

        Assert.Equal(Error(BookingStoreError.CorruptSnapshot SnapshotCorruption.InvalidUtf8), load store)

    [<Fact>]
    let ``valid json with impossible domain data is a corrupt snapshot`` () =
        use temporary = new TemporaryDirectory()
        let snapshotPath = Path.Combine(temporary.Path, "booking.json")
        let store = FileBookingStore(configuration snapshotPath)

        File.WriteAllText(
            snapshotPath,
            """{"schemaVersion":1,"requestId":"REQ-CORRUPT","eventId":"EVT-K08B","seats":0,"status":"pending"}""",
            Encoding.UTF8
        )

        Assert.Equal(
            Error(
                BookingStoreError.CorruptSnapshot(
                    SnapshotCorruption.InvalidDomainData(DtoMappingError.InvalidSeatCount(NonPositiveSeatCount 0))
                )
            ),
            load store
        )

    [<Fact>]
    let ``oversized snapshots are rejected before json parsing`` () =
        use temporary = new TemporaryDirectory()
        let snapshotPath = Path.Combine(temporary.Path, "booking.json")
        let store = FileBookingStore(configuration snapshotPath)
        let tooLarge = Array.create (FileBookingStore.MaxSnapshotBytes + 1) (byte 'x')

        File.WriteAllBytes(snapshotPath, tooLarge)

        Assert.Equal(Error(BookingStoreError.SnapshotTooLarge FileBookingStore.MaxSnapshotBytes), load store)

    [<Fact>]
    let ``cancellation before save preserves the previous complete snapshot`` () =
        use temporary = new TemporaryDirectory()
        let snapshotPath = Path.Combine(temporary.Path, "booking.json")
        let store = FileBookingStore(configuration snapshotPath)
        let pending = createPending "REQ-CANCELLED-WRITE"
        let confirmed = confirm pending
        save store pending

        use owner = new CancellationTokenSource()
        owner.Cancel()

        Assert.ThrowsAny<OperationCanceledException>(fun () ->
            store.Save(confirmed, owner.Token).GetAwaiter().GetResult() |> ignore)
        |> ignore

        Assert.Equal(Ok(Some pending), load store)
        Assert.Empty(Directory.GetFiles(temporary.Path, "*.tmp"))

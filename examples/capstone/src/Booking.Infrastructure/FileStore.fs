namespace Booking.Infrastructure

open System
open System.IO
open System.Text
open System.Text.Json
open System.Threading
open System.Threading.Tasks
open Booking.Contracts
open Booking.Domain

[<RequireQualifiedAccess>]
type SnapshotCorruption =
    | InvalidUtf8
    | InvalidJson
    | InvalidDomainData of DtoMappingError
    | UnsupportedSchemaVersion of actual: int
    | InconsistentData

[<RequireQualifiedAccess>]
type BookingStoreError =
    | SnapshotTooLarge of maxBytes: int
    | CorruptSnapshot of SnapshotCorruption
    | CannotReadSnapshot
    | CannotWriteTemporarySnapshot
    | CannotReplaceSnapshot
    | SnapshotActivityMismatch

module internal FileStoreImplementation =
    [<Literal>]
    let MaxSnapshotBytes = 64 * 1024

    let private strictUtf8 = UTF8Encoding(false, true)

    let decode (bytes: byte array) =
        try
            let decoded = strictUtf8.GetString bytes

            if decoded.Length > 0 && decoded[0] = '\uFEFF' then
                decoded.Substring 1 |> Ok
            else
                Ok decoded
        with :? DecoderFallbackException ->
            Error(BookingStoreError.CorruptSnapshot SnapshotCorruption.InvalidUtf8)

    let readBounded maxSnapshotBytes (path: string) (cancellationToken: CancellationToken) =
        task {
            try
                let options = FileStreamOptions()
                options.Mode <- FileMode.Open
                options.Access <- FileAccess.Read
                options.Share <- FileShare.Read
                options.Options <- FileOptions.Asynchronous ||| FileOptions.SequentialScan
                options.BufferSize <- 4096

                use stream = new FileStream(path, options)
                let buffer = Array.zeroCreate<byte> (maxSnapshotBytes + 1)
                let mutable total = 0
                let mutable reachedEnd = false

                while not reachedEnd && total < buffer.Length do
                    let! count = stream.ReadAsync(buffer.AsMemory(total, buffer.Length - total), cancellationToken)

                    if count = 0 then
                        reachedEnd <- true
                    else
                        total <- total + count

                if total > maxSnapshotBytes then
                    return Error(BookingStoreError.SnapshotTooLarge maxSnapshotBytes)
                else
                    return Ok(Some(buffer.AsSpan(0, total).ToArray()))
            with
            | :? FileNotFoundException
            | :? DirectoryNotFoundException -> return Ok None
            | :? IOException
            | :? UnauthorizedAccessException -> return Error BookingStoreError.CannotReadSnapshot
        }

    let writeTemporary (path: string) (bytes: byte array) (cancellationToken: CancellationToken) =
        task {
            try
                let options = FileStreamOptions()
                options.Mode <- FileMode.CreateNew
                options.Access <- FileAccess.Write
                options.Share <- FileShare.None
                options.Options <- FileOptions.Asynchronous ||| FileOptions.WriteThrough
                options.BufferSize <- 4096

                use stream = new FileStream(path, options)
                do! stream.WriteAsync(bytes.AsMemory(), cancellationToken)

                // Flush(true) also asks the operating system to flush intermediate file buffers:
                // https://learn.microsoft.com/dotnet/api/system.io.filestream.flush#system-io-filestream-flush(system-boolean)
                stream.Flush(true)
                return Ok()
            with
            | :? IOException
            | :? UnauthorizedAccessException -> return Error BookingStoreError.CannotWriteTemporarySnapshot
        }

    let replace temporaryPath snapshotPath =
        try
            // The temporary file is in the destination directory, so Move does not cross volumes.
            // https://learn.microsoft.com/dotnet/api/system.io.file.move#system-io-file-move(system-string-system-string-system-boolean)
            File.Move(temporaryPath, snapshotPath, true)
            Ok()
        with
        | :? IOException
        | :? UnauthorizedAccessException -> Error BookingStoreError.CannotReplaceSnapshot

    let cleanup temporaryPath =
        try
            File.Delete temporaryPath
        with
        | :? IOException
        | :? UnauthorizedAccessException -> ()

// #region file-booking-store
type FileBookingStore(configuration: BookingStoreConfiguration) =
    let snapshotPath = BookingStoreConfiguration.snapshotPath configuration
    let directoryPath = BookingStoreConfiguration.directoryPath configuration

    static member MaxSnapshotBytes = FileStoreImplementation.MaxSnapshotBytes

    member _.Load(cancellationToken: CancellationToken) : Task<Result<Booking option, BookingStoreError>> =
        task {
            cancellationToken.ThrowIfCancellationRequested()

            let! bytesResult =
                FileStoreImplementation.readBounded
                    FileStoreImplementation.MaxSnapshotBytes
                    snapshotPath
                    cancellationToken

            match bytesResult with
            | Error error -> return Error error
            | Ok None -> return Ok None
            | Ok(Some bytes) ->
                match FileStoreImplementation.decode bytes with
                | Error error -> return Error error
                | Ok json ->
                    try
                        return
                            BookingJson.deserializeBooking json
                            |> BookingMapping.toDomain
                            |> Result.map Some
                            |> Result.mapError (
                                SnapshotCorruption.InvalidDomainData >> BookingStoreError.CorruptSnapshot
                            )
                    with :? JsonException ->
                        return Error(BookingStoreError.CorruptSnapshot SnapshotCorruption.InvalidJson)
        }

    member _.Save(booking: Booking, cancellationToken: CancellationToken) : Task<Result<unit, BookingStoreError>> =
        task {
            cancellationToken.ThrowIfCancellationRequested()

            let bytes =
                booking
                |> BookingMapping.ofDomain
                |> BookingJson.serializeBooking
                |> Encoding.UTF8.GetBytes

            if bytes.Length > FileStoreImplementation.MaxSnapshotBytes then
                return Error(BookingStoreError.SnapshotTooLarge FileStoreImplementation.MaxSnapshotBytes)
            else
                let temporaryPath =
                    Path.Combine(directoryPath, $".{Path.GetFileName(snapshotPath)}.{Guid.NewGuid():N}.tmp")

                try
                    let directoryResult =
                        try
                            Directory.CreateDirectory directoryPath |> ignore
                            Ok()
                        with
                        | :? IOException
                        | :? UnauthorizedAccessException -> Error BookingStoreError.CannotWriteTemporarySnapshot

                    match directoryResult with
                    | Error error -> return Error error
                    | Ok() ->
                        let! writeResult = FileStoreImplementation.writeTemporary temporaryPath bytes cancellationToken

                        match writeResult with
                        | Error error -> return Error error
                        | Ok() ->
                            cancellationToken.ThrowIfCancellationRequested()
                            return FileStoreImplementation.replace temporaryPath snapshotPath
                finally
                    FileStoreImplementation.cleanup temporaryPath
        }
// #endregion file-booking-store

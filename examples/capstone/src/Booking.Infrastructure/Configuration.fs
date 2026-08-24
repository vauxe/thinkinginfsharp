namespace Booking.Infrastructure

open System
open System.IO

[<RequireQualifiedAccess>]
type BookingStoreConfigurationError =
    | MissingSnapshotPath
    | InvalidSnapshotPath

type BookingStoreConfiguration =
    private
        { SnapshotPath: string
          DirectoryPath: string }

// #region store-configuration
[<RequireQualifiedAccess>]
module BookingStoreConfiguration =
    [<Literal>]
    let PathEnvironmentVariable = "BOOKING_STORE_PATH"

    let create (configuredPath: string | null) =
        match configuredPath with
        | null -> Error BookingStoreConfigurationError.MissingSnapshotPath
        | raw when String.IsNullOrWhiteSpace raw -> Error BookingStoreConfigurationError.MissingSnapshotPath
        | raw ->
            try
                let fullPath = raw.Trim() |> Path.GetFullPath
                let fileName = Path.GetFileName fullPath
                let directory = Path.GetDirectoryName fullPath

                match directory with
                | null -> Error BookingStoreConfigurationError.InvalidSnapshotPath
                | value when String.IsNullOrWhiteSpace fileName || Directory.Exists fullPath ->
                    Error BookingStoreConfigurationError.InvalidSnapshotPath
                | value ->
                    Ok
                        { SnapshotPath = fullPath
                          DirectoryPath = value }
            with
            | :? ArgumentException
            | :? NotSupportedException
            | :? PathTooLongException -> Error BookingStoreConfigurationError.InvalidSnapshotPath

    // Environment variables override file settings in the default .NET configuration stack:
    // https://learn.microsoft.com/dotnet/core/extensions/configuration-providers#environment-variable-configuration-provider
    let fromEnvironment () =
        Environment.GetEnvironmentVariable PathEnvironmentVariable |> create

    let snapshotPath configuration = configuration.SnapshotPath

    let internal directoryPath configuration = configuration.DirectoryPath
// #endregion store-configuration

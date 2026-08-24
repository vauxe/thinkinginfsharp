namespace ThinkingInFSharp.ExampleTests

open System
open System.Diagnostics
open System.IO
open Xunit

module CapstonePart01Tests =
    let rec private findRepositoryRoot (directory: DirectoryInfo) =
        if File.Exists(Path.Combine(directory.FullName, "ThinkingInFSharp.slnx")) then
            directory.FullName
        else
            match directory.Parent with
            | null -> failwith "Could not locate the repository root."
            | parent -> findRepositoryRoot parent

    [<Fact>]
    let ``booking basics parses transforms and respects capacity`` () =
        let repositoryRoot = findRepositoryRoot (DirectoryInfo(AppContext.BaseDirectory))
        let scriptPath = Path.Combine(repositoryRoot, "examples/capstone/part-01/BookingBasics.fsx")
        let startInfo = ProcessStartInfo("dotnet")
        startInfo.ArgumentList.Add("fsi")
        startInfo.ArgumentList.Add("--exec")
        startInfo.ArgumentList.Add(scriptPath)
        startInfo.WorkingDirectory <- repositoryRoot
        startInfo.UseShellExecute <- false
        startInfo.RedirectStandardOutput <- true
        startInfo.RedirectStandardError <- true

        use fsiProcess = new Process(StartInfo = startInfo)

        Assert.True(fsiProcess.Start(), "Failed to start dotnet fsi.")

        let outputTask = fsiProcess.StandardOutput.ReadToEndAsync()
        let errorTask = fsiProcess.StandardError.ReadToEndAsync()

        if not (fsiProcess.WaitForExit(30_000)) then
            fsiProcess.Kill(true)
            failwith "BookingBasics.fsx did not finish within 30 seconds."

        let output = outputTask.GetAwaiter().GetResult()
        let error = errorTask.GetAwaiter().GetResult()

        Assert.True(fsiProcess.ExitCode = 0, error)

        let expected =
            [ "Rows: valid=4 invalid=2"
              "Labels: [\"B-101:Lin:3\"; \"B-102:Ada:2\"; \"B-103:Sam:4\"; \"B-104:Mira:2\"]"
              "Accepted IDs: [\"B-101\"; \"B-102\"; \"B-104\"]"
              "Rejected IDs: [\"B-103\"]"
              "Capacity: booked=7 remaining=1" ]

        let actual =
            output.Replace("\r\n", "\n").Trim().Split('\n') |> Array.toList

        Assert.Equal<string list>(expected, actual)

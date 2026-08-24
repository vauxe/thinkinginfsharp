namespace ThinkingInFSharp.AvaloniaSample

open System
open Avalonia
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Markup.Xaml

type App() =
    inherit Application()

    override this.Initialize() = AvaloniaXamlLoader.Load(this)

    override this.OnFrameworkInitializationCompleted() =
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktop -> desktop.MainWindow <- MainWindow()
        | _ -> ()

        base.OnFrameworkInitializationCompleted()

module Program =
    [<CompiledName("BuildAvaloniaApp")>]
    let buildAvaloniaApp () =
        AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace(areas = Array.empty)

    [<EntryPoint; STAThread>]
    let main args =
        buildAvaloniaApp().StartWithClassicDesktopLifetime(args)

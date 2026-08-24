namespace ThinkingInFSharp.AvaloniaSample

open Avalonia.Controls
open Avalonia.Markup.Xaml

type Model = { Seats: int }

type Message =
    | AddSeat
    | RemoveSeat
    | Reset

[<RequireQualifiedAccess>]
module Counter =
    let initial = { Seats = 0 }

    let update message model =
        match message with
        | AddSeat -> { model with Seats = model.Seats + 1 }
        | RemoveSeat ->
            { model with
                Seats = max 0 (model.Seats - 1) }
        | Reset -> initial

type MainWindow() as this =
    inherit Window()

    do
        AvaloniaXamlLoader.Load(this)

        let countText = this.GetControl<TextBlock>("CountText")
        let statusText = this.GetControl<TextBlock>("StatusText")
        let removeButton = this.GetControl<Button>("RemoveButton")
        let mutable model = Counter.initial

        let render state =
            countText.Text <- string state.Seats

            statusText.Text <-
                if state.Seats = 0 then "No seats selected"
                elif state.Seats = 1 then "1 seat selected"
                else $"{state.Seats} seats selected"

            removeButton.IsEnabled <- state.Seats > 0

        let dispatch message =
            model <- Counter.update message model
            render model

        this.GetControl<Button>("AddButton").Click.Add(fun _ -> dispatch AddSeat)
        removeButton.Click.Add(fun _ -> dispatch RemoveSeat)
        this.GetControl<Button>("ResetButton").Click.Add(fun _ -> dispatch Reset)
        render model

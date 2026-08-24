module FableSample.App

open Browser.Dom

type Model = { Count: int }

type Message =
    | Increment
    | Reset

let initialModel = { Count = 0 }

let update message model =
    match message with
    | Increment -> { model with Count = model.Count + 1 }
    | Reset -> initialModel

let private elementById id =
    match document.getElementById id with
    | null -> failwith $"Required element #{id} was not found."
    | element -> element

let private countOutput = elementById "count"
let private incrementButton = elementById "increment"
let private resetButton = elementById "reset"
let mutable private model = initialModel

let private render () =
    countOutput.textContent <- $"Count: {model.Count}"

    if model.Count = 0 then
        resetButton.setAttribute ("disabled", "")
    else
        resetButton.removeAttribute "disabled"

let private dispatch message =
    model <- update message model
    render ()

incrementButton.addEventListener ("click", fun _ -> dispatch Increment)
resetButton.addEventListener ("click", fun _ -> dispatch Reset)

render ()
document.documentElement.setAttribute ("data-fable-ready", "true")

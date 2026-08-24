let requests =
    [ "Lin", 3
      "Ada", 0
      "Sam", 2
      "Mina", -1 ]

let isValidRequest (_, seats) = seats > 0
let formatRequest (guest, seats) = $"{guest}:{seats}"

// #region filter-map-pipeline
let pipelineLabels =
    requests
    |> List.filter isValidRequest
    |> List.map formatRequest

printfn "Pipeline labels: %A" pipelineLabels
// #endregion filter-map-pipeline

// #region choose-pipeline
let tryFormatRequest request =
    if isValidRequest request then
        Some (formatRequest request)
    else
        None

let chosenLabels = requests |> List.choose tryFormatRequest

printfn "Chosen labels: %A" chosenLabels
// #endregion choose-pipeline

// #region for-loop
let labelsWithFor source =
    let mutable reversedLabels = []

    for request in source do
        match tryFormatRequest request with
        | Some label -> reversedLabels <- label :: reversedLabels
        | None -> ()

    List.rev reversedLabels
// #endregion for-loop

// #region while-loop
let labelsWithWhile source =
    let mutable remaining = source
    let mutable reversedLabels = []

    while not (List.isEmpty remaining) do
        match remaining with
        | request :: tail ->
            remaining <- tail

            match tryFormatRequest request with
            | Some label -> reversedLabels <- label :: reversedLabels
            | None -> ()
        | [] -> ()

    List.rev reversedLabels
// #endregion while-loop

let forLabels = labelsWithFor requests
let whileLabels = labelsWithWhile requests

printfn "For/while agree: %b" (pipelineLabels = forLabels && forLabels = whileLabels)

// #region list-iteration
printf "Iteration order:"
for label in pipelineLabels do
    printf " %s" label
printfn ""
// #endregion list-iteration

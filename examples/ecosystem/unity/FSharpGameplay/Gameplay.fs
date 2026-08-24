namespace ThinkingInFSharp.UnitySample

open System

module private Guard =
    let finite parameterName (value: single) =
        if Single.IsNaN value || Single.IsInfinity value then
            invalidArg parameterName "Value must be finite."

    let nonNegative parameterName value =
        finite parameterName value

        if value < 0.0f then
            invalidArg parameterName "Value must be non-negative."

[<Sealed>]
type MotionState internal (positionX: single, velocityX: single) =
    member _.PositionX = positionX
    member _.VelocityX = velocityX

[<AbstractClass; Sealed>]
type Gameplay private () =
    static member Create(positionX: single) =
        Guard.finite (nameof positionX) positionX
        MotionState(positionX, 0.0f)

    static member Step(state: MotionState, horizontal: single, speed: single, deltaTime: single) =
        if Object.ReferenceEquals(state, null) then
            nullArg (nameof state)

        Guard.finite (nameof horizontal) horizontal
        Guard.nonNegative (nameof speed) speed
        Guard.nonNegative (nameof deltaTime) deltaTime

        let normalizedInput = max -1.0f (min 1.0f horizontal)
        let velocityX = normalizedInput * speed
        let positionX = state.PositionX + velocityX * deltaTime

        Guard.finite "resultingVelocity" velocityX
        Guard.finite "resultingPosition" positionX
        MotionState(positionX, velocityX)

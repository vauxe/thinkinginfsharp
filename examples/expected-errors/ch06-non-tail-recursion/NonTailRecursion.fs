namespace ThinkingInFSharp.ExpectedErrors

module NonTailRecursion =
    // #region non-tail-recursion
    [<TailCall>]
    let rec fibonacci n =
        match n with
        | 0
        | 1 -> n
        | value -> fibonacci (value - 1) + fibonacci (value - 2)
    // #endregion non-tail-recursion

// #region recursive-type
type BookingTree<'T> =
    | Empty
    | Leaf of 'T
    | Branch of left: BookingTree<'T> * right: BookingTree<'T>

let emptyTree: BookingTree<int> = Empty
let leafTree = Leaf 2

let branchTree =
    Branch(Leaf 2, Branch(Leaf 3, Leaf 4))
// #endregion recursive-type

// #region structural-traversal
let rec countLeaves tree =
    match tree with
    | Empty -> 0
    | Leaf _ -> 1
    | Branch(left, right) ->
        countLeaves left + countLeaves right

let rec totalSeats tree =
    match tree with
    | Empty -> 0
    | Leaf seats -> seats
    | Branch(left, right) ->
        totalSeats left + totalSeats right

printfn
    "Counts: empty=%d leaf=%d branch=%d"
    (countLeaves emptyTree)
    (countLeaves leafTree)
    (countLeaves branchTree)

printfn
    "Totals: empty=%d leaf=%d branch=%d"
    (totalSeats emptyTree)
    (totalSeats leafTree)
    (totalSeats branchTree)
// #endregion structural-traversal

// #region tree-map
let rec mapTree mapping tree =
    match tree with
    | Empty -> Empty
    | Leaf value -> Leaf(mapping value)
    | Branch(left, right) ->
        Branch(mapTree mapping left, mapTree mapping right)

let rec renderTree formatValue tree =
    match tree with
    | Empty -> "Empty"
    | Leaf value -> $"Leaf({formatValue value})"
    | Branch(left, right) ->
        $"Branch({renderTree formatValue left},{renderTree formatValue right})"

let labeledTree =
    branchTree
    |> mapTree (fun seats -> $"{seats} seats")

printfn "Mapped: %s" (renderTree id labeledTree)
// #endregion tree-map

// #region tree-fold
let rec foldTree onEmpty onLeaf onBranch tree =
    match tree with
    | Empty -> onEmpty
    | Leaf value -> onLeaf value
    | Branch(left, right) ->
        let leftResult = foldTree onEmpty onLeaf onBranch left
        let rightResult = foldTree onEmpty onLeaf onBranch right
        onBranch leftResult rightResult

let countWithFold =
    foldTree 0 (fun _ -> 1) (+)

let totalWithFold =
    foldTree 0 id (+)

printfn
    "Fold agrees: count=%b total=%b"
    (countWithFold branchTree = countLeaves branchTree)
    (totalWithFold branchTree = totalSeats branchTree)
// #endregion tree-fold

// #region tree-depth
let rec height tree =
    match tree with
    | Empty -> 0
    | Leaf _ -> 1
    | Branch(left, right) ->
        1 + max (height left) (height right)

printfn
    "Heights: empty=%d leaf=%d branch=%d"
    (height emptyTree)
    (height leafTree)
    (height branchTree)

printfn
    "Shape preserved: before=%d after=%d"
    (countLeaves branchTree)
    (countLeaves labeledTree)
// #endregion tree-depth

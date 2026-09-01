// F# 10 单文件导览
// 运行（需要 .NET 10 SDK）：dotnet fsi --nologo --warnaserror+ --checknulls+ --exec examples/learn-fsharp.fsx

open System
open System.Linq

// 不用 assert：未定义 DEBUG 时它可能不执行。结果不符就直接抛出异常。
let check name expected actual =
    if actual <> expected then
        failwithf "%s\nexpected: %A\nactual:   %A" name expected actual

// 1. 值、类型与表达式

// let 绑定默认不可变。类型通常由编译器推断，只在边界或有歧义时标注。
let customerName = "Lin" // string
let seatCount: int = 2 // 显式类型标注
let unitPrice = 49.5m // decimal；m 是字面量后缀
let requestId = 42L // int64；L 是字面量后缀
let totalPrice = decimal seatCount * unitPrice

// 数值类型不会隐式转换；decimal seatCount 是显式转换。
// 表达式的最后一个值就是结果，不写 return。
let priceLabel =
    if totalPrice < 100m then "普通" else "大额"

// 元组适合临时组合少量值；解构直接取出各部分。
let point = 10, 20
let x, y = point

// unit 只有一个值 ()，表示“只产生效果，没有有用结果”。
let writeMessage message: unit = printfn "%s" message

check "decimal calculation" 99.0m totalPrice

// 2. 函数、柯里化与组合

// 参数用空格分隔。int -> int -> int 表示：先接收一个 int，再返回等待第二个 int 的函数。
let add left right = left + right
let square number = number * number

// 少给参数会得到新函数，称为部分应用。
let addThree = add 3

// 高阶函数接收或返回函数。这里 transform 的类型由它的用法推断。
let applyTwice transform value =
    value |> transform |> transform

// x |> f 等于 f x；f >> g 生成一个先调用 f、再调用 g 的函数。
let transformedWithPipe = 4 |> addThree |> square
let transformedWithComposition = (addThree >> square) 4
let increasedTwice = applyTwice ((+) 1) 10

// fun 创建匿名函数，适合短小且只使用一次的逻辑。
let doubled = [ 1; 2; 3 ] |> List.map (fun value -> value * 2)

check "function composition" 49 transformedWithComposition

// 3. 模块与可见性

// 模块组织值、函数和类型。private 把实现细节限制在模块内。
module Pricing =
    let private vatRate = 0.13m

    let subtotal quantity price = decimal quantity * price

    let total quantity price =
        let beforeTax = subtotal quantity price
        beforeTax * (1m + vatRate)

check "module function" 113.00m (Pricing.total 2 50m)

// .fs 项目通常用 namespace 划分程序集，用 module 组织功能。
// 同一项目按 .fsproj 中的文件顺序编译；后面的文件才能引用前面的文件。
// .fsi 签名文件列出模块对外公开的类型和函数；/// 注释会显示在编辑器中。

// 4. 条件、循环与递归

// if 的各分支必须返回兼容的类型；match 从上到下选择第一个匹配分支。
let describeNumber number =
    match number with
    | 0 -> "零"
    | value when value < 0 -> "负数"
    | 1 | 2 | 3 -> "较小的正数"
    | _ -> "较大的正数"

// 数据转换通常用集合函数；需要逐步更新局部状态时才用 for 或 while。
// let ... = ... 创建绑定；= 比较是否相等；mutable 值用 <- 更新。
let mutable loopTotal = 0
for number in 1 .. 4 do
    loopTotal <- loopTotal + number

// 尾递归把累计结果作为参数传给下一次调用。
// [<TailCall>] 会在递归调用不再位于函数末尾时给出警告。
[<TailCall>]
let rec private sumLoop total remaining =
    match remaining with
    | [] -> total
    | head :: tail -> sumLoop (total + head) tail

let sum values = sumLoop 0 values

// 列表模式可以分别处理空列表、单项列表和“首项 + 剩余项”。
let describeList values =
    match values with
    | [] -> "空"
    | [ only ] -> $"一个：{only}"
    | first :: _ as all -> $"共 {List.length all} 个，第一个是 {first}"

check "guard and or-pattern" "较小的正数" (describeNumber 2)
check "tail recursion" 15 (sum [ 1 .. 5 ])

// 5. 集合与转换

// list：不可变链表，适合递归、顺序遍历和从头部添加。
let numbers = [ 1 .. 10 ]

let evenSquareSum =
    numbers
    |> List.filter (fun number -> number % 2 = 0)
    |> List.map square
    |> List.sum

// choose 要求每项返回 option：保留 Some 中的值，丢弃 None。
let parsedNumbers =
    [ "10"; "x"; "30" ]
    |> List.choose (fun text ->
        match Int32.TryParse text with
        | true, value -> Some value
        | false, _ -> None)

// fold 从初始状态开始，逐项更新状态，最后得到一个值。
let product = [ 2; 3; 4 ] |> List.fold (fun state value -> state * value) 1

// 集合表达式按条件生成元素；yield! 把另一个集合的元素接到当前位置。
let combined =
    [ yield 0
      yield! [ 1 .. 3 ]
      for number in 4 .. 6 do
          if number % 2 = 0 then
              yield number ]

check "list pipeline" 220 evenSquareSum

// array 支持按索引访问和原地修改。需要独立副本时使用 Array.copy。
let mutableArray = [| 3; 1; 2 |]
mutableArray[0] <- 4
Array.sortInPlace mutableArray

// seq<'T> 按需生成元素。每次枚举都会重新执行生成代码；重复使用时可用 Seq.cache 缓存。
let cachedSquares =
    seq {
        for number in 1 .. 4 do
            yield square number
    }
    |> Seq.cache

let firstTwoSquares = cachedSquares |> Seq.take 2 |> Seq.toList

check "mutable array" [| 1; 2; 4 |] mutableArray

// Map 和 Set 不可变且有序。Map.tryFind 在键不存在时返回 None。
let prices = Map.ofList [ "tea", 12m; "coffee", 18m ]
let availableProducts = prices |> Map.keys |> Set.ofSeq
let coffeePrice = prices |> Map.tryFind "coffee"

// ResizeArray 和 Dictionary 是 .NET 可变集合，适合局部构建或互操作。
let buffer = ResizeArray<string>()
buffer.Add "A"
buffer.Add "B"

let lookup = Collections.Generic.Dictionary<string, int>()
lookup["A"] <- 1

check "Map.tryFind" (Some 18m) coffeePrice

// 6. 用类型描述领域

// 单分支联合会创建真正的新类型；private 迫使调用者通过 BookingId.create 构造它。
module BookingDomain =
    type BookingId = private BookingId of string

    module BookingId =
        let create text =
            if String.IsNullOrWhiteSpace text then Error "预订编号不能为空" else Ok (BookingId text)

        let value (BookingId text) = text

    [<RequireQualifiedAccess>]
    type BookingStatus =
        | Pending
        | Confirmed of confirmationCode: string
        | Cancelled of reason: string

    [<RequireQualifiedAccess>]
    type BookingError =
        | MissingCustomer
        | NonPositiveSeats of actual: int
        | MissingConfirmationCode
        | MissingCancellationReason
        | CannotResizeFrom of currentStatus: BookingStatus
        | CannotConfirmFrom of currentStatus: BookingStatus
        | CannotCancelFrom of currentStatus: BookingStatus

    // 记录构造器是私有的，调用者只能通过 create 和状态转换函数得到 Booking。
    type Booking =
        private
            { Id: BookingId
              Customer: string
              Seats: int
              Status: BookingStatus }

    module Booking =
        let create id customer seats =
            if String.IsNullOrWhiteSpace customer then
                Error BookingError.MissingCustomer
            elif seats <= 0 then
                Error (BookingError.NonPositiveSeats seats)
            else
                Ok
                    { Id = id
                      Customer = customer
                      Seats = seats
                      Status = BookingStatus.Pending }

        let id booking = booking.Id
        let seats booking = booking.Seats
        let status booking = booking.Status
        let label { Customer = customer; Seats = seats } = $"{customer} / {seats}"

        let isActive booking =
            match booking.Status with
            | BookingStatus.Cancelled _ -> false
            | BookingStatus.Pending
            | BookingStatus.Confirmed _ -> true

        // with 创建修改后的记录，原值不变。
        let resize seats booking =
            if seats <= 0 then
                Error (BookingError.NonPositiveSeats seats)
            else
                match booking.Status with
                | BookingStatus.Pending -> Ok { booking with Seats = seats }
                | currentStatus -> Error (BookingError.CannotResizeFrom currentStatus)

        let confirm confirmationCode booking =
            match booking.Status with
            | BookingStatus.Pending when String.IsNullOrWhiteSpace confirmationCode ->
                Error BookingError.MissingConfirmationCode
            | BookingStatus.Pending ->
                Ok
                    { booking with
                        Status = BookingStatus.Confirmed confirmationCode }
            | currentStatus -> Error (BookingError.CannotConfirmFrom currentStatus)

        let cancel reason booking =
            if String.IsNullOrWhiteSpace reason then
                Error BookingError.MissingCancellationReason
            else
                match booking.Status with
                | BookingStatus.Pending
                | BookingStatus.Confirmed _ ->
                    Ok
                        { booking with
                            Status = BookingStatus.Cancelled reason }
                | BookingStatus.Cancelled _ as currentStatus ->
                    Error (BookingError.CannotCancelFrom currentStatus)

open BookingDomain

let private unwrap = function
    | Ok value -> value
    | Error error -> failwithf "unexpected Error: %A" error

let bookingId = BookingId.create "B-1" |> unwrap
let pendingBooking = Booking.create bookingId "Lin" 2 |> unwrap
let resizedBooking = pendingBooking |> Booking.resize 3 |> unwrap
let cancelledBooking = pendingBooking |> Booking.cancel "客户请求" |> unwrap
let resizeAfterCancellation = cancelledBooking |> Booking.resize 4
let confirmedBookingResult = pendingBooking |> Booking.confirm "C-42"

let repeatedConfirmation =
    confirmedBookingResult |> Result.bind (Booking.confirm "C-43")

let describeStatus status =
    match status with
    | BookingStatus.Pending -> "待处理"
    | BookingStatus.Confirmed code -> $"已确认：{code}"
    | BookingStatus.Cancelled reason -> $"已取消：{reason}"

// 匿名记录适合一次性的局部结果；需要重复使用或对外暴露时定义具名记录。
let bookingSummary =
    confirmedBookingResult
    |> Result.map (fun booking ->
        {| Id = booking |> Booking.id |> BookingId.value
           Active = Booking.isActive booking |})

check "record copy" (2, 3) (Booking.seats pendingBooking, Booking.seats resizedBooking)

check
    "typed state transition"
    (Ok (BookingStatus.Confirmed "C-42"))
    (confirmedBookingResult |> Result.map Booking.status)

check
    "invalid state transition"
    (Error (BookingError.CannotConfirmFrom(BookingStatus.Confirmed "C-42")))
    repeatedConfirmation

check "booking rejects blank customer" (Error BookingError.MissingCustomer) (Booking.create bookingId "" 2)

check
    "cancelled booking cannot resize"
    (Error (BookingError.CannotResizeFrom(BookingStatus.Cancelled "客户请求")))
    resizeAfterCancellation

// enum 适合 .NET 互操作。外部整数不一定对应已命名分支，所以 match 仍需要兜底分支。
type ExternalState =
    | Unknown = 0
    | Open = 1
    | Closed = 2

let describeExternalState state =
    match state with
    | ExternalState.Open -> "open"
    | ExternalState.Closed -> "closed"
    | _ -> "unknown"

check "unnamed enum value" "unknown" (enum<ExternalState> 99 |> describeExternalState)

// 7. option、voption 与 null

let customerByBooking = Map.ofList [ ("B-1", "Lin") ]
let knownCustomerOption = Map.tryFind "B-1" customerByBooking
let missingCustomerOption = Map.tryFind "B-9" customerByBooking

// defaultValue 遇到 Some 时取内部值，遇到 None 时使用备用值。
// 管道会把 option 作为最后一个参数，因此先写备用值“未知”。
let knownCustomer = knownCustomerOption |> Option.defaultValue "未知"
let missingCustomer = missingCustomerOption |> Option.defaultValue "未知"

// map 转换 Some 中的值；bind 串联可能返回 None 的下一步，避免嵌套 option。
let tryPositive value = if value > 0 then Some value else None
let requestedSeats = Some 3 |> Option.bind tryPositive |> Option.map ((*) 2)

// voption 是值类型表示，可能减少分配。只有性能测量证明有需要时才使用。
let tryHeadValueOption values =
    match values with
    | head :: _ -> ValueSome head
    | [] -> ValueNone

// .NET API 仍可能返回 null。在类型上标出 null，并在边界立即转为 option。
let textFromDotNet: string | null = null
let safeLength = textFromDotNet |> Option.ofObj |> Option.map String.length

// Some 仍然可以包住 null；应在创建 option 前处理 .NET 边界上的 null。
check "Option.bind" (Some 6) requestedSeats
check "value option" (ValueSome 8) (tryHeadValueOption [ 8; 9 ])
check "null boundary" None safeLength

// 8. Result 与计算表达式

// option 只区分“有值”和“没有”；Result 还会保留失败原因。
[<RequireQualifiedAccess>]
type SeatError =
    | InvalidNumber of input: string
    | NonPositive of actual: int
    | ExceedsCapacity of requested: int * available: int
    | MissingCustomer

let parseSeatCount (text: string) =
    match Int32.TryParse text with
    | true, value -> Ok value
    | false, _ -> Error (SeatError.InvalidNumber text)

let validateSeatCount available requested =
    if requested <= 0 then
        Error (SeatError.NonPositive requested)
    elif requested > available then
        Error (SeatError.ExceedsCapacity(requested, available))
    else
        Ok requested

let describeSeatError error =
    match error with
    | SeatError.InvalidNumber input -> $"不是整数：{input}"
    | SeatError.NonPositive actual -> $"必须为正数：{actual}"
    | SeatError.ExceedsCapacity(requested, available) -> $"申请 {requested}，剩余 {available}"
    | SeatError.MissingCustomer -> "客户名不能为空"

// map 转换 Ok 中的值；bind 串联下一个 Result；mapError 只转换 Error。
let validatedSeatCount =
    parseSeatCount "3"
    |> Result.bind (validateSeatCount 5)
    |> Result.map ((*) 2)

let rejectedSeatMessage =
    parseSeatCount "8"
    |> Result.bind (validateSeatCount 5)
    |> Result.mapError describeSeatError

// 逐项转换列表：遇到第一个 Error 就停止，否则返回全部结果。
let traverseResult transform values =
    let rec loop collected remaining =
        match remaining with
        | [] -> Ok(List.rev collected)
        | head :: tail ->
            match transform head with
            | Ok value -> loop (value :: collected) tail
            | Error error -> Error error

    loop [] values

let parsedSeatBatch = [ "1"; "2"; "3" ] |> traverseResult parseSeatCount
let rejectedSeatBatch = [ "1"; "x"; "3" ] |> traverseResult parseSeatCount

// 这个最小 Result builder 只支持 let!、return 和 return!。let! 取出 Ok，并保留第一个 Error。
type ResultBuilder() =
    member _.Return value = Ok value
    member _.ReturnFrom value = value
    member _.Bind(value, next) = Result.bind next value

let resultWorkflow = ResultBuilder()

let createSeatRequest available customer seatText =
    resultWorkflow {
        let! parsed = parseSeatCount seatText
        let! seats = validateSeatCount available parsed

        if String.IsNullOrWhiteSpace customer then
            return! Error SeatError.MissingCustomer
        else
            return {| Customer = customer; Seats = seats |}
    }

// 独立字段可以全部验证后一次返回所有错误。
let validateIndependentFields customer requested =
    let errors =
        [ if String.IsNullOrWhiteSpace customer then
              yield SeatError.MissingCustomer

          if requested <= 0 then
              yield SeatError.NonPositive requested ]

    match errors with
    | [] -> Ok {| Customer = customer; Seats = requested |}
    | found -> Error found

check "Result.bind and map" (Ok 6) validatedSeatCount

check "batch stops on Error" (Error (SeatError.InvalidNumber "x")) rejectedSeatBatch

check
    "result computation expression"
    (Ok {| Customer = "Lin"; Seats = 2 |})
    (createSeatRequest 5 "Lin" "2")

check
    "independent validation accumulates errors"
    (Error [ SeatError.MissingCustomer; SeatError.NonPositive 0 ])
    (validateIndependentFields "" 0)

// 9. 异常与资源

// 可预期的业务失败用 Result；当前层无法正常处理的运行时故障才用异常。
exception StartupConfigurationError of settingName: string

let requireSetting name value =
    match value with
    | Some setting -> setting
    | None -> raise (StartupConfigurationError name)

// 这里只处理 StartupConfigurationError；其他异常会自动继续向上传播。
let missingSettingMessage =
    try
        requireSetting "DATABASE_URL" (None: string option) |> ignore
        "配置存在"
    with
    | StartupConfigurationError name -> $"缺少配置：{name}"

// use 会在离开作用域时调用 Dispose，即使中途抛出异常也一样。
let readFirstLine text =
    use reader = new IO.StringReader(text)
    reader.ReadLine() |> Option.ofObj

check "typed exception" "缺少配置：DATABASE_URL" missingSettingMessage
check "read first line" (Some "first") (readFirstLine "first\nsecond")

// 10. .NET 互操作与对象模型

// F# 函数通常用空格传参；.NET 方法、构造器和重载通常保留圆括号。
let sampleDate = DateOnly.FromDateTime(DateTime(2026, 9, 1))
let parts = "A,,B".Split(',', StringSplitOptions.RemoveEmptyEntries)
let commandMatches = String.Equals("START", "start", StringComparison.OrdinalIgnoreCase)

// 接口用来定义框架边界。对象表达式可就地实现接口，无需再声明一个类。
type IClock =
    abstract member UtcNow: DateTimeOffset

let fixedClock =
    { new IClock with
        member _.UtcNow = DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero) }

let isExpiredAt now expiresAt = now >= expiresAt
let isExpired (clock: IClock) expiresAt = isExpiredAt clock.UtcNow expiresAt

// 对象需要封装可变状态或匹配 .NET 框架时使用类；只表示数据时优先记录和联合。
type Counter(initialValue: int) =
    let mutable value = initialValue

    member _.Value = value

    member _.Increment(?step: int) =
        value <- value + defaultArg step 1
        value

let counter = Counter(10)
let afterOne = counter.Increment()
let afterThree = counter.Increment(step = 2)

// 扩展成员给现有类型增加 .NET 风格的调用方式；业务规则仍优先写成普通函数。
module StringExtensions =
    type String with
        member this.IsBlank = String.IsNullOrWhiteSpace this

open StringExtensions

// :? 在运行时检查 .NET 类型，并绑定转换后的值。业务代码通常应依靠编译期类型。
let describeObject (value: obj | null) =
    match value with
    | null -> "null"
    | :? int as number -> $"int:{number}"
    | :? string as text -> $"string:{text}"
    | other -> other.GetType().Name

// Event.Publish 提供可订阅的事件流；use 会在函数结束时取消订阅。
let captureEvents () =
    let source = Event<int>()
    let seen = ResizeArray<int>()
    use subscription = source.Publish.Subscribe(seen.Add)
    source.Trigger 2
    source.Trigger 4
    List.ofSeq seen

check
    "object expression"
    true
    (isExpired fixedClock (DateTimeOffset(2026, 9, 1, 7, 0, 0, TimeSpan.Zero)))

check "class optional arguments" (11, 13) (afterOne, afterThree)
check "extension member" true "   ".IsBlank
check "type-test pattern" "int:5" (describeObject (box 5))
check "event subscription" [ 2; 4 ] (captureEvents ())

// 11. 可变状态、值类型与延迟计算

// 把可变状态限制在函数或对象内部，调用者只看到普通的输入和返回值。
let distinctCount values =
    let seen = Collections.Generic.HashSet<_>()

    for value in values do
        seen.Add value |> ignore

    seen.Count

// struct 记录按值存储，可能减少分配，但复制和装箱也有成本。先测量再使用。
[<Struct>]
type Pixel =
    { X: int
      Y: int }

let pixel = { X = 3; Y = 4 }

// Span 是不复制数据的内存视图；编译器会阻止它离开底层内存的有效范围。
let countCommas (text: string) =
    let span = text.AsSpan()
    let mutable count = 0

    for character in span do
        if character = ',' then
            count <- count + 1

    count

// lazy 只在第一次请求 Value 时计算，并缓存结果。
let lazySquare = lazy (square 12)

check "encapsulated mutation" 3 (distinctCount [ 1; 1; 2; 3; 3 ])
check "ReadOnlySpan" 2 (countCommas "A,B,C")
check "lazy value" 144 lazySquare.Value

// 12. task、async 与并发状态

// 调用基于 Task 的 .NET API 时使用 task。把取消令牌传给每个支持取消的操作。
let loadSubtotal (cancellationToken: Threading.CancellationToken) =
    task {
        cancellationToken.ThrowIfCancellationRequested()
        do! Threading.Tasks.Task.Delay(1, cancellationToken)
        return 100m
    }

let loadDiscount (cancellationToken: Threading.CancellationToken) =
    task {
        cancellationToken.ThrowIfCancellationRequested()
        do! Threading.Tasks.Task.Delay(1, cancellationToken)
        return 15m
    }

let calculateNetTotal (cancellationToken: Threading.CancellationToken) =
    task {
        // let! 等待一个结果；and! 先启动彼此独立的操作，再一起等待。
        let! subtotal = loadSubtotal cancellationToken
        and! discount = loadDiscount cancellationToken
        return subtotal - discount
    }

// async 是 F# 自带的异步工作流，可与 Async.Parallel 和 MailboxProcessor 配合。
let delayedSquare number =
    async {
        do! Async.Sleep 1
        return square number
    }

let parallelSquares =
    [ 1; 2; 3 ]
    |> List.map delayedSquare
    |> Async.Parallel
    |> Async.RunSynchronously

// MailboxProcessor 逐条处理消息；只有内部循环能更新 total，避免共享可变状态。
type CounterMessage =
    | Add of int
    | Get of AsyncReplyChannel<int>

let runCounterAgent () =
    use agent =
        MailboxProcessor.Start(fun inbox ->
            let rec loop total =
                async {
                    let! message = inbox.Receive()

                    match message with
                    | Add value -> return! loop (total + value)
                    | Get reply ->
                        reply.Reply total
                        return! loop total
                }

            loop 0)

    agent.Post(Add 2)
    agent.Post(Add 3)
    agent.PostAndReply Get

// 应用代码应保持异步；这个脚本只在最外层同步等待结果。
let awaitTask (pending: Threading.Tasks.Task<'T>) = pending.GetAwaiter().GetResult()
let netTotal = calculateNetTotal Threading.CancellationToken.None |> awaitTask

let cancellationObserved =
    use source = new Threading.CancellationTokenSource()
    source.Cancel()

    try
        calculateNetTotal source.Token |> awaitTask |> ignore
        false
    with :? OperationCanceledException -> true

check "task and!" 85m netTotal
check "task cancellation" true cancellationObserved
check "Async.Parallel" [| 1; 4; 9 |] parallelSquares
check "MailboxProcessor" 5 (runCounterAgent ())

// 13. 泛型、约束与递归类型

// 不依赖具体类型的函数会自动成为泛型函数；'T 代表任意类型。
let firstOr fallback (values: 'T list) =
    values |> List.tryHead |> Option.defaultValue fallback

// comparison 表示值可以比较大小。空列表返回 None，避免 List.max 抛出异常。
let tryLargest<'T when 'T: comparison> (values: 'T list) =
    match values with
    | [] -> None
    | head :: tail -> tail |> List.fold max head |> Some

// Tree<'T> 是递归泛型类型；mapTree 只转换叶子值，不改变树的分支结构。
[<RequireQualifiedAccess>]
type Tree<'T> =
    | Leaf of 'T
    | Branch of Tree<'T> list

let rec mapTree transform tree =
    match tree with
    | Tree.Leaf value -> Tree.Leaf(transform value)
    | Tree.Branch children -> children |> List.map (mapTree transform) |> Tree.Branch

let numberTree = Tree.Branch [ Tree.Leaf 1; Tree.Branch [ Tree.Leaf 2 ] ]
let textTree = numberTree |> mapTree (fun value -> $"N{value}")

// inline 让编译器根据实参类型解析 +，因此 twice 同时支持 int 和 decimal。
// 普通代码优先使用泛型和接口；只有确实需要跨类型运算符时才用这种写法。
let inline twice value = value + value

check "total function handles empty input" None (tryLargest ([]: int list))

check
    "generic recursive union"
    (Tree.Branch [ Tree.Leaf "N1"; Tree.Branch [ Tree.Leaf "N2" ] ])
    textTree

check "statically resolved generics" (6, 5.0m) (twice 3, twice 2.5m)

// 14. 主动模式

// 主动模式把判断逻辑变成可复用的 match 分支；|_| 表示该分支可能匹配失败。
let (|Integer|_|) (text: string) =
    match Int32.TryParse text with
    | true, value -> Some value
    | false, _ -> None

let (|Even|Odd|) value =
    if value % 2 = 0 then Even value else Odd value

let describeInput text =
    match text with
    | Integer (Even value) -> $"偶数：{value}"
    | Integer (Odd value) -> $"奇数：{value}"
    | _ -> "不是整数"

check "partial and multi-case active patterns" "偶数：42" (describeInput "42")

// 15. 度量单位与 IQueryable

// 度量单位在编译期防止把不同单位混用；运行时仍然是普通数值。
[<Measure>]
type km

[<Measure>]
type hour

let distance = 120.0<km>
let duration = 2.0<hour>
let averageSpeed = distance / duration

// 外部无单位数值进入系统时，必须显式附加单位。
let distanceFromDatabase = LanguagePrimitives.FloatWithMeasure<km> 30.0

// IQueryable 把查询记录为表达式树，再由数据提供程序翻译；内存数据直接用 List、Array 或 Seq。
let querySource = [| 1 .. 6 |].AsQueryable()

let evenSquaresQuery: IQueryable<int> =
    query {
        for number in querySource do
        where (number % 2 = 0)
        select (number * number)
    }

let queriedSquares = evenSquaresQuery |> Seq.toList

check "units of measure" 60.0<km/hour> averageSpeed
check "query expression" [ 4; 16; 36 ] queriedSquares

// 16. 框架与跨语言边界

// 只有框架要求基类时才使用继承；普通业务代码用函数、记录和接口更简单。
[<AbstractClass>]
type TextFormatter() =
    abstract member Format: string -> string

type UpperFormatter() =
    inherit TextFormatter()
    override _.Format value = value.ToUpperInvariant()

let formattedText = UpperFormatter().Format "f#"

// .NET 委托可以包装 F# 函数，供只接受 delegate 的 API 使用。
let descendingComparison = Comparison<int>(fun left right -> compare right left)
let sortableNumbers = [| 2; 1; 3 |]
Array.Sort(sortableNumbers, descendingComparison)

// DTO 只描述传输数据的形状；在边界显式转换，不直接暴露领域表示。
type BookingDto =
    { Id: string
      Seats: int }

let toBookingDto (booking: Booking): BookingDto =
    { Id = booking |> Booking.id |> BookingId.value
      Seats = Booking.seats booking }

// System.Text.Json 可直接处理不可变记录。只有框架明确要求无参构造器和 setter 时才加 CLIMutable。
let bookingDto = toBookingDto pendingBooking
let bookingJson = System.Text.Json.JsonSerializer.Serialize bookingDto

let roundTrippedBookingDto =
    System.Text.Json.JsonSerializer.Deserialize<BookingDto> bookingJson
    |> Option.ofObj

check "delegate interop" [| 3; 2; 1 |] sortableNumbers
check "DTO round trip" (Some bookingDto) roundTrippedBookingDto

// 17. 工程组织与依赖方向

// 正式项目会把下面的模块拆到同名 .fs 文件，并在 .fsproj 中保持这个顺序。
module ProjectExample =
    module Domain =
        [<RequireQualifiedAccess>]
        type QuoteError =
            | UnknownProduct of product: string
            | NonPositiveQuantity of actual: int

        let quote quantity unitPrice =
            if quantity <= 0 then
                Error (QuoteError.NonPositiveQuantity quantity)
            else
                Ok (decimal quantity * unitPrice)

    module Application =
        // 应用层接收“查价”函数，不知道价格实际来自 Map 还是数据库。
        type FindPrice = string -> decimal option

        let quote (findPrice: FindPrice) product quantity =
            match findPrice product with
            | None -> Error (Domain.QuoteError.UnknownProduct product)
            | Some price -> Domain.quote quantity price

    module Infrastructure =
        let private catalog = Map.ofList [ "tea", 12m; "coffee", 18m ]

        let findPrice: Application.FindPrice =
            fun product -> Map.tryFind product catalog

    module Program =
        // Program 选择具体的查价实现，再把它交给应用层。
        let quote product quantity =
            Application.quote Infrastructure.findPrice product quantity

check "project composition" (Ok 36m) (ProjectExample.Program.quote "coffee" 2)

check
    "project keeps domain errors"
    (Error (ProjectExample.Domain.QuoteError.UnknownProduct "water"))
    (ProjectExample.Program.quote "water" 2)

check
    "domain rejects non-positive quantity"
    (Error (ProjectExample.Domain.QuoteError.NonPositiveQuantity 0))
    (ProjectExample.Program.quote "coffee" 0)

printfn "F# 10 learning script checks passed."

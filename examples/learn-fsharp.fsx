// F# 10 单文件导览
// 运行（需要 .NET 10 SDK）：dotnet fsi --nologo --warnaserror+ --checknulls+ --exec examples/learn-fsharp.fsx

open System
open System.Linq

// assert 只在定义 DEBUG 时执行。这个检查失败时一定抛出异常。
let check name expected actual =
    if actual <> expected then
        failwithf "%s\nexpected: %A\nactual:   %A" name expected actual

let checkTrue name condition = check name true condition

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
check "if expression" "普通" priceLabel
check "tuple destructuring" 30 (x + y)
check "int64 literal" 42L requestId

// 2. 函数、柯里化与组合

// 参数用空格分隔。int -> int -> int 表示先接收一个 int，再返回一个接收 int 的函数。
let add left right = left + right
let square number = number * number

// 少给参数会得到新函数，称为部分应用。
let addThree = add 3

// 高阶函数把函数作为参数；类型由调用方式推断。
let applyTwice transform value =
    value |> transform |> transform

// |> 把左侧结果传给右侧最后一个参数；>> 从左到右组合函数。
let transformedWithPipe = 4 |> addThree |> square
let transformedWithComposition = (addThree >> square) 4
let increasedTwice = applyTwice ((+) 1) 10

// fun 创建匿名函数，适合短小且只使用一次的逻辑。
let doubled = [ 1; 2; 3 ] |> List.map (fun value -> value * 2)

check "partial application" 49 transformedWithPipe
check "function composition" 49 transformedWithComposition
check "higher-order function" 12 increasedTwice
check "lambda" [ 2; 4; 6 ] doubled

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
// .fsi 固定公开 API；/// XML 注释为公开成员提供编辑器文档。

// 4. 条件、循环与递归

// if 的各分支必须产生兼容类型；match 按顺序检查值的形状。
let describeNumber number =
    match number with
    | 0 -> "零"
    | value when value < 0 -> "负数"
    | 1 | 2 | 3 -> "较小的正数"
    | _ -> "较大的正数"

// 普通数据转换优先使用集合函数；for 和 while 适合明确的命令式边界。
// = 用于绑定或相等比较；mutable 绑定只能用 <- 重新赋值。
let mutable loopTotal = 0
for number in 1 .. 4 do
    loopTotal <- loopTotal + number

check "guard and or-pattern" "较小的正数" (describeNumber 2)
check "for loop" 10 loopTotal

// 尾递归把中间结果放在参数里；TailCall 在尾调用被破坏时产生警告。
[<TailCall>]
let rec private sumLoop total remaining =
    match remaining with
    | [] -> total
    | head :: tail -> sumLoop (total + head) tail

let sum values = sumLoop 0 values

// 列表模式同时判断空列表，并把首项和其余项拆开。
let describeList values =
    match values with
    | [] -> "空"
    | [ only ] -> $"一个：{only}"
    | first :: _ as all -> $"共 {List.length all} 个，第一个是 {first}"

check "tail recursion" 15 (sum [ 1 .. 5 ])
check "list and as-pattern" "共 3 个，第一个是 4" (describeList [ 4; 5; 6 ])

// 5. 集合与转换

// list：不可变链表，适合递归、顺序遍历和从头部添加。
let numbers = [ 1 .. 10 ]

let evenSquareSum =
    numbers
    |> List.filter (fun number -> number % 2 = 0)
    |> List.map square
    |> List.sum

// choose 同时过滤和转换；Some 的值保留，None 丢弃。
let parsedNumbers =
    [ "10"; "x"; "30" ]
    |> List.choose (fun text ->
        match Int32.TryParse text with
        | true, value -> Some value
        | false, _ -> None)

// fold 把集合归约为一个值；初始值和更新规则都显式给出。
let product = [ 2; 3; 4 ] |> List.fold (fun state value -> state * value) 1

// 集合表达式适合生成数据；yield! 把另一个集合展开到当前位置。
let combined =
    [ yield 0
      yield! [ 1 .. 3 ]
      for number in 4 .. 6 do
          if number % 2 = 0 then
              yield number ]

check "list pipeline" 220 evenSquareSum
check "List.choose" [ 10; 30 ] parsedNumbers
check "List.fold" 24 product
check "list expression" [ 0; 1; 2; 3; 4; 6 ] combined

// array：连续内存、按索引访问，可原地修改；复制数组时用 Array.copy。
let mutableArray = [| 3; 1; 2 |]
mutableArray[0] <- 4
Array.sortInPlace mutableArray

// seq<'T> 是 IEnumerable<'T>，按需计算。多次枚举会重复执行来源；需要复用时可缓存。
let cachedSquares =
    seq {
        for number in 1 .. 4 do
            yield square number
    }
    |> Seq.cache

let firstTwoSquares = cachedSquares |> Seq.take 2 |> Seq.toList

check "mutable array" [| 1; 2; 4 |] mutableArray
check "lazy sequence" [ 1; 4 ] firstTwoSquares

// Map 和 Set 不可变且有序；tryFind 用 option 表达“可能没有”。
let prices = Map.ofList [ "tea", 12m; "coffee", 18m ]
let availableProducts = prices |> Map.keys |> Set.ofSeq
let coffeePrice = prices |> Map.tryFind "coffee"

// ResizeArray 和 Dictionary 是 .NET 可变集合，适合局部构建或互操作。
let buffer = ResizeArray<string>()
buffer.Add "A"
buffer.Add "B"

let lookup = Collections.Generic.Dictionary<string, int>()
lookup["A"] <- 1

checkTrue "Set membership" (Set.contains "tea" availableProducts)
check "Map.tryFind" (Some 18m) coffeePrice
check "ResizeArray" [ "A"; "B" ] (List.ofSeq buffer)
check "Dictionary" 1 lookup["A"]

// 6. 用类型描述领域

// 类型缩写只是别名。单分支联合创建新类型，private 把构造规则留在模块内。
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

    // 私有记录阻止调用者绕过 create 和状态转换直接制造非法 Booking。
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
let repeatedCancellation = cancelledBooking |> Booking.cancel "再次请求"
let resizeAfterCancellation = cancelledBooking |> Booking.resize 4
let confirmedBookingResult = pendingBooking |> Booking.confirm "C-42"

let repeatedConfirmation =
    confirmedBookingResult |> Result.bind (Booking.confirm "C-43")

let describeStatus status =
    match status with
    | BookingStatus.Pending -> "待处理"
    | BookingStatus.Confirmed code -> $"已确认：{code}"
    | BookingStatus.Cancelled reason -> $"已取消：{reason}"

// 匿名记录适合局部投影，不代替稳定的领域类型。
let bookingSummary =
    confirmedBookingResult
    |> Result.map (fun booking ->
        {| Id = booking |> Booking.id |> BookingId.value
           Active = Booking.isActive booking |})

check "single-case union" "B-1" (BookingId.value bookingId)
check "record copy keeps original" 2 (Booking.seats pendingBooking)
check "record copy creates new value" 3 (Booking.seats resizedBooking)
check "record pattern" "Lin / 2" (Booking.label pendingBooking)
check "qualified union case" "已确认：C-42" (describeStatus (BookingStatus.Confirmed "C-42"))
checkTrue "cancelled booking is inactive" (cancelledBooking |> Booking.isActive |> not)
check
    "typed state transition"
    (Ok (BookingStatus.Confirmed "C-42"))
    (confirmedBookingResult |> Result.map Booking.status)
check
    "invalid state transition"
    (Error (BookingError.CannotConfirmFrom(BookingStatus.Confirmed "C-42")))
    repeatedConfirmation
check "anonymous record" (Ok {| Id = "B-1"; Active = true |}) bookingSummary
check "booking rejects blank customer" (Error BookingError.MissingCustomer) (Booking.create bookingId "" 2)
check "booking rejects non-positive seats" (Error (BookingError.NonPositiveSeats 0)) (Booking.create bookingId "Lin" 0)
check "resize rejects non-positive seats" (Error (BookingError.NonPositiveSeats 0)) (Booking.resize 0 pendingBooking)
check "confirmation requires a code" (Error BookingError.MissingConfirmationCode) (Booking.confirm "" pendingBooking)
check "cancellation requires a reason" (Error BookingError.MissingCancellationReason) (Booking.cancel "" pendingBooking)
check
    "repeated cancellation is rejected"
    (Error (BookingError.CannotCancelFrom(BookingStatus.Cancelled "客户请求")))
    repeatedCancellation
check
    "cancelled booking cannot resize"
    (Error (BookingError.CannotResizeFrom(BookingStatus.Cancelled "客户请求")))
    resizeAfterCancellation

// enum 用于 .NET 协议。它也能保存未命名整数，所以读取外部值时保留兜底分支。
type ExternalState =
    | Unknown = 0
    | Open = 1
    | Closed = 2

let describeExternalState state =
    match state with
    | ExternalState.Open -> "open"
    | ExternalState.Closed -> "closed"
    | _ -> "unknown"

check "enum interop" ExternalState.Open (enum<ExternalState> 1)
check "unnamed enum value" "unknown" (enum<ExternalState> 99 |> describeExternalState)

// 7. option、voption 与 null

let customerByBooking = Map.ofList [ ("B-1", "Lin") ]
let knownCustomerOption = Map.tryFind "B-1" customerByBooking
let missingCustomerOption = Map.tryFind "B-9" customerByBooking

// defaultValue 接收“备用值”和 option。Some 返回内部值；None 返回备用值。
// 管道把 option 传到最后一个参数，所以备用值写在前面。
let knownCustomer = knownCustomerOption |> Option.defaultValue "未知"
let missingCustomer = missingCustomerOption |> Option.defaultValue "未知"

// map 转换 Some；bind 用于本身也返回 option 的下一步，避免 option<option<_>>。
let tryPositive value = if value > 0 then Some value else None
let requestedSeats = Some 3 |> Option.bind tryPositive |> Option.map ((*) 2)

// voption 的 ValueSome/ValueNone 是值类型表示，只在分析证明分配成本重要时使用。
let tryHeadValueOption values =
    match values with
    | head :: _ -> ValueSome head
    | [] -> ValueNone

// .NET 边界仍可能返回 null；签名显式标注并尽早转为 option。
let textFromDotNet: string | null = null
let safeLength = textFromDotNet |> Option.ofObj |> Option.map String.length

// option 也可能被人为构造为 Some null；不要把它当成自动的 null 清洗器。
check "Option.defaultValue Some" "Lin" knownCustomer
check "Option.defaultValue None" "未知" missingCustomer
check "Option.bind" (Some 6) requestedSeats
check "value option" (ValueSome 8) (tryHeadValueOption [ 8; 9 ])
check "null boundary" None safeLength

// 8. Result 与计算表达式

// option 只表达“没有”；Result<'Value, 'Error> 还保存失败原因。
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

// map 转换 Ok；mapError 转换 Error；bind 串联下一步也可能失败的计算。
let validatedSeatCount =
    parseSeatCount "3"
    |> Result.bind (validateSeatCount 5)
    |> Result.map ((*) 2)

let rejectedSeatMessage =
    parseSeatCount "8"
    |> Result.bind (validateSeatCount 5)
    |> Result.mapError describeSeatError

// 把 'Input -> Result<'Value, 'Error> 提升到列表；遇到首个 Error 就停止。
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

// 最小 Result builder：只支持 let!、return 和 return!，在首个 Error 处停止。
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
check "Result.mapError" (Error "申请 8，剩余 5") rejectedSeatMessage
check "list of Result values" (Ok [ 1; 2; 3 ]) parsedSeatBatch
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

// 可预期的业务失败用 Result。异常用于无法在当前层正常处理的运行时故障。
exception StartupConfigurationError of settingName: string

let requireSetting name value =
    match value with
    | Some setting -> setting
    | None -> raise (StartupConfigurationError name)

// 只捕获能处理的异常；不能处理时用 reraise() 保留原始堆栈。
let missingSettingMessage =
    try
        requireSetting "DATABASE_URL" (None: string option) |> ignore
        "配置存在"
    with
    | StartupConfigurationError name -> $"缺少配置：{name}"

// use 在作用域结束或发生异常时调用 Dispose，等价于可靠的 try/finally。
let readFirstLine text =
    use reader = new IO.StringReader(text)
    reader.ReadLine() |> Option.ofObj

check "typed exception" "缺少配置：DATABASE_URL" missingSettingMessage
check "read first line" (Some "first") (readFirstLine "first\nsecond")
check "end of input" None (readFirstLine "")

// 10. .NET 互操作与对象模型

// F# 函数通常用空格传参；.NET 方法、构造器和重载通常保留圆括号。
let sampleDate = DateOnly.FromDateTime(DateTime(2026, 9, 1))
let parts = "A,,B".Split(',', StringSplitOptions.RemoveEmptyEntries)
let commandMatches = String.Equals("START", "start", StringComparison.OrdinalIgnoreCase)

// 接口适合框架边界。对象表达式可直接实现接口，测试替身无需额外类。
type IClock =
    abstract member UtcNow: DateTimeOffset

let fixedClock =
    { new IClock with
        member _.UtcNow = DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero) }

let isExpiredAt now expiresAt = now >= expiresAt
let isExpired (clock: IClock) expiresAt = isExpiredAt clock.UtcNow expiresAt

// 类适合有身份、封装状态或必须匹配 .NET 框架的对象；数据默认优先记录和联合。
type Counter(initialValue: int) =
    let mutable value = initialValue

    member _.Value = value

    member _.Increment(?step: int) =
        value <- value + defaultArg step 1
        value

let counter = Counter(10)
let afterOne = counter.Increment()
let afterThree = counter.Increment(step = 2)

// 扩展成员适合补充边界 API；核心领域逻辑更适合普通函数。
module StringExtensions =
    type String with
        member this.IsBlank = String.IsNullOrWhiteSpace this

open StringExtensions

// :? 在运行时测试 .NET 类型并安全解构。核心模型应尽量依靠静态类型。
let describeObject (value: obj | null) =
    match value with
    | null -> "null"
    | :? int as number -> $"int:{number}"
    | :? string as text -> $"string:{text}"
    | other -> other.GetType().Name

// source.Publish 可作为 IObservable 订阅；订阅对象必须释放。
let captureEvents () =
    let source = Event<int>()
    let seen = ResizeArray<int>()
    use subscription = source.Publish.Subscribe(seen.Add)
    source.Trigger 2
    source.Trigger 4
    List.ofSeq seen

check "DateOnly interop" (DateOnly(2026, 9, 1)) sampleDate
check "overloaded method" [| "A"; "B" |] parts
checkTrue "explicit string comparison" commandMatches
checkTrue
    "object expression"
    (isExpired fixedClock (DateTimeOffset(2026, 9, 1, 7, 0, 0, TimeSpan.Zero)))
checkTrue
    "pure rule behind object boundary"
    (isExpiredAt fixedClock.UtcNow (DateTimeOffset(2026, 9, 1, 7, 0, 0, TimeSpan.Zero)))
check "class default optional argument" 11 afterOne
check "class named optional argument" 13 afterThree
checkTrue "extension member" "   ".IsBlank
check "type-test pattern" "int:5" (describeObject (box 5))
check "event subscription" [ 2; 4 ] (captureEvents ())

// 11. 可变状态、值类型与延迟计算

// 可变状态尽量限制在函数或对象内部，对调用方保留纯函数接口。
let distinctCount values =
    let seen = Collections.Generic.HashSet<_>()

    for value in values do
        seen.Add value |> ignore

    seen.Count

// 小型 struct 可减少单独的堆分配，但复制和装箱也有成本；先分析再使用。
[<Struct>]
type Pixel =
    { X: int
      Y: int }

let pixel = { X = 3; Y = 4 }

// Span 是 byref-like 内存视图，编译器限制它逃出安全作用域。
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
check "struct record" 7 (pixel.X + pixel.Y)
check "ReadOnlySpan" 2 (countCommas "A,B,C")
check "lazy value" 144 lazySquare.Value

// 12. task、async 与并发状态

// .NET API 以 Task 为主时使用 task。取消令牌必须显式传入依赖和等待操作。
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
        // let! 顺序等待；and! 并发等待彼此独立的操作。
        let! subtotal = loadSubtotal cancellationToken
        and! discount = loadDiscount cancellationToken
        return subtotal - discount
    }

// async 是 F# 工作流，适合组合 Async API、并行工作流和 MailboxProcessor。
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

// MailboxProcessor 串行处理消息，让可变状态只由一个循环拥有。
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

// 生产代码一路 await；脚本只在最外层同步取得结果。
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
checkTrue "task cancellation" cancellationObserved
check "Async.Parallel" [| 1; 4; 9 |] parallelSquares
check "MailboxProcessor" 5 (runCounterAgent ())

// 13. 泛型、约束与递归类型

// 没有依赖具体类型的代码会自动泛化；'T 表示任意类型。
let firstOr fallback (values: 'T list) =
    values |> List.tryHead |> Option.defaultValue fallback

// comparison 约束允许排序和大小比较；空列表进入返回类型，而不是由 List.max 抛异常。
let tryLargest<'T when 'T: comparison> (values: 'T list) =
    match values with
    | [] -> None
    | head :: tail -> tail |> List.fold max head |> Some

// 泛型联合可描述递归结构；mapTree 保留结构，只转换叶子值。
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

// inline 可生成静态解析的成员约束，因此同一 + 能用于多种数值类型。
// 普通代码优先泛型和接口；不要把 inline 当成未经测量的性能开关。
let inline twice value = value + value

check "inferred generic" "fallback" (firstOr "fallback" [])
check "comparison constraint" (Some "pear") (tryLargest [ "apple"; "pear" ])
check "total function handles empty input" None (tryLargest ([]: int list))
check
    "generic recursive union"
    (Tree.Branch [ Tree.Leaf "N1"; Tree.Branch [ Tree.Leaf "N2" ] ])
    textTree
check "SRTP int" 6 (twice 3)
check "SRTP decimal" 5.0m (twice 2.5m)

// 14. 主动模式

// 主动模式把解析或分类包装成可读的 match 分支。|_| 表示匹配可以失败。
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

// 度量单位在编译期防止混用；运行时仍是普通数值，没有额外包装。
[<Measure>]
type km

[<Measure>]
type hour

let distance = 120.0<km>
let duration = 2.0<hour>
let averageSpeed = distance / duration

// 外部无单位数值进入系统时，必须显式附加单位。
let distanceFromDatabase = LanguagePrimitives.FloatWithMeasure<km> 30.0

// IQueryable 查询会生成表达式树交给提供程序；内存集合通常直接用模块函数。
let querySource = [| 1 .. 6 |].AsQueryable()

let evenSquaresQuery: IQueryable<int> =
    query {
        for number in querySource do
        where (number % 2 = 0)
        select (number * number)
    }

let queriedSquares = evenSquaresQuery |> Seq.toList

check "units of measure" 60.0<km/hour> averageSpeed
check "unit attached at boundary" 30.0<km> distanceFromDatabase
check "query expression" [ 4; 16; 36 ] queriedSquares

// 16. 框架与跨语言边界

// 只有框架要求基类时才使用继承；普通业务组合函数、记录和接口更简单。
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

// 传输类型与领域类型分开，边界负责显式映射。
type BookingDto =
    { Id: string
      Seats: int }

let toBookingDto (booking: Booking): BookingDto =
    { Id = booking |> Booking.id |> BookingId.value
      Seats = Booking.seats booking }

// System.Text.Json 支持不可变记录；只有具体框架要求无参构造器和 setter 时才加 CLIMutable。
let bookingDto = toBookingDto pendingBooking
let bookingJson = System.Text.Json.JsonSerializer.Serialize bookingDto

let roundTrippedBookingDto =
    System.Text.Json.JsonSerializer.Deserialize<BookingDto> bookingJson
    |> Option.ofObj

check "abstract class override" "F#" formattedText
check "delegate interop" [| 3; 2; 1 |] sortableNumbers
check "DTO round trip" (Some bookingDto) roundTrippedBookingDto

// 17. 工程组织与依赖方向

// 正式项目把这些模块放入同名 .fs 文件，并由 .fsproj 按以下顺序编译。
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
        // 应用层声明自己需要的函数，不依赖具体数据库。
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
        // Program 是组合根，在这里把用例和基础设施连接起来。
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

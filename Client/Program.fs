module ChatClient

open System
open System.IO
open System.Net.Sockets
open System.Text

[<Literal>]
let AuthOk = "AUTH_OK"

[<Literal>]
let AuthFail = "AUTH_FAIL"

type ConnectionSettings = {
    Hostname: string
    Port: int
}

let utf8NoBom = UTF8Encoding(false)

let prompt (label: string) =
    printf "%s" label
    Console.ReadLine()

let tryTrimmedValue (value: string) =
    if String.IsNullOrWhiteSpace(value) then
        None
    else
        Some(value.Trim())

let rec promptRequired (label: string) =
    match prompt label |> tryTrimmedValue with
    | Some value ->
        value
    | None ->
        printfn "Value cannot be empty."
        promptRequired label

let sendLine (writer: StreamWriter) (message: string) =
    writer.WriteLine(message)

let tryReadLine (reader: StreamReader) =
    try
        let line = reader.ReadLine()
        if isNull line then None else Some line
    with
    | :? IOException
    | :? ObjectDisposedException ->
        None

let buildCredentials username password = sprintf "%s:%s" username password

let describeAuthenticationFailure response =
    match response with
    | Some AuthFail ->
        Some "Invalid username or password. Please try again."
    | Some _ ->
        Some "Unexpected response from server. Please try again."
    | None ->
        None

let rec authenticate (reader: StreamReader) (writer: StreamWriter) =
    let username = promptRequired "Enter your username: "
    let password = promptRequired "Enter your password: "
    sendLine writer (buildCredentials username password)

    match tryReadLine reader with
    | Some AuthOk ->
        username
    | response ->
        match describeAuthenticationFailure response with
        | Some message ->
            printfn "%s" message
            authenticate reader writer
        | None ->
            failwith "Disconnected during authentication."

let renderIncomingMessage (message: string) =
    Console.WriteLine()
    Console.WriteLine(message)
    Console.Write("> ")

let rec receiveLoop (reader: StreamReader) =
    async {
        match tryReadLine reader with
        | Some message ->
            renderIncomingMessage message
            return! receiveLoop reader
        | None ->
            return ()
    }

let isQuitCommand (message: string) =
    String.Equals(message, "/quit", StringComparison.OrdinalIgnoreCase)

let rec runInputLoop (writer: StreamWriter) =
    Console.Write("> ")

    match Console.ReadLine() with
    | null ->
        ()
    | message ->
        match tryTrimmedValue message with
        | Some trimmed when isQuitCommand trimmed ->
            ()
        | Some trimmed ->
            sendLine writer trimmed
            runInputLoop writer
        | None ->
            runInputLoop writer

let tryParsePort (value: string) =
    match Int32.TryParse(value) with
    | true, port when port > 0 && port <= 65535 -> Some port
    | _ -> None

let tryGetArg index (argv: string array) =
    if index < argv.Length then
        Some argv.[index]
    else
        None

let parseArgs (argv: string array) =
    let hostname =
        argv
        |> tryGetArg 0
        |> Option.defaultValue "localhost"

    let port =
        argv
        |> tryGetArg 1
        |> Option.map (fun value ->
            value
            |> tryParsePort
            |> Option.defaultWith (fun () -> failwith "Port must be an integer between 1 and 65535."))
        |> Option.defaultValue 8963

    {
        Hostname = hostname
        Port = port
    }

let startClient (settings: ConnectionSettings) =
    use client = new TcpClient()
    client.Connect(settings.Hostname, settings.Port)

    use stream = client.GetStream()
    use reader = new StreamReader(stream, utf8NoBom, false, 1024, true)
    use writer = new StreamWriter(stream, utf8NoBom, 1024, true)
    writer.AutoFlush <- true

    let channel = promptRequired "Enter the channel name: "
    sendLine writer channel

    let username = authenticate reader writer
    printfn "Connected as %s. Type /quit to disconnect." username

    let receiveTask = receiveLoop reader |> Async.StartAsTask
    runInputLoop writer

    client.Close()
    receiveTask.Wait(500) |> ignore

[<EntryPoint>]
let main argv =
    try
        let settings = parseArgs argv
        startClient settings
        0
    with
    | :? SocketException as ex ->
        eprintfn "Connection error: %s" ex.Message
        1
    | ex ->
        eprintfn "%s" ex.Message
        1

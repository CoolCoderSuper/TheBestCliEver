module ChatClient

open System
open System.IO
open System.Net.Sockets
open System.Text
open System.Threading

[<Literal>]
let AuthOk = "AUTH_OK"

[<Literal>]
let AuthFail = "AUTH_FAIL"

let utf8NoBom = UTF8Encoding(false)

let prompt (label: string) =
    printf "%s" label
    Console.ReadLine()

let promptRequired (label: string) =
    let rec loop () =
        let value = prompt label
        if String.IsNullOrWhiteSpace(value) then
            printfn "Value cannot be empty."
            loop ()
        else
            value.Trim()
    loop ()

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

let rec authenticate (reader: StreamReader) (writer: StreamWriter) =
    let username = promptRequired "Enter your username: "
    let password = promptRequired "Enter your password: "
    sendLine writer (sprintf "%s:%s" username password)

    match tryReadLine reader with
    | Some AuthOk ->
        username
    | Some AuthFail ->
        printfn "Invalid username or password. Please try again."
        authenticate reader writer
    | Some _ ->
        printfn "Unexpected response from server. Please try again."
        authenticate reader writer
    | None ->
        failwith "Disconnected during authentication."

let startReceiveLoop (reader: StreamReader) =
    let receiveThread =
        Thread(
            ThreadStart(fun () ->
                let mutable running = true
                while running do
                    match tryReadLine reader with
                    | Some message ->
                        Console.WriteLine()
                        Console.WriteLine(message)
                        Console.Write("> ")
                    | None ->
                        running <- false))

    receiveThread.IsBackground <- true
    receiveThread.Start()
    receiveThread

let runInputLoop (writer: StreamWriter) =
    let mutable running = true

    while running do
        Console.Write("> ")
        let message = Console.ReadLine()

        if isNull message then
            running <- false
        else
            let trimmed = message.Trim()
            if String.Equals(trimmed, "/quit", StringComparison.OrdinalIgnoreCase) then
                running <- false
            elif not (String.IsNullOrWhiteSpace(trimmed)) then
                sendLine writer trimmed

let startClient (hostname: string) (port: int) =
    use client = new TcpClient()
    client.Connect(hostname, port)

    use stream = client.GetStream()
    use reader = new StreamReader(stream, utf8NoBom, false, 1024, true)
    use writer = new StreamWriter(stream, utf8NoBom, 1024, true)
    writer.AutoFlush <- true

    let channel = promptRequired "Enter the channel name: "
    sendLine writer channel

    let username = authenticate reader writer
    printfn "Connected as %s. Type /quit to disconnect." username

    let receiveThread = startReceiveLoop reader
    runInputLoop writer

    client.Close()
    receiveThread.Join(500) |> ignore

let parseArgs (argv: string array) =
    let hostname = if argv.Length > 0 then argv.[0] else "localhost"

    let port =
        if argv.Length > 1 then
            match Int32.TryParse(argv.[1]) with
            | true, value when value > 0 && value <= 65535 -> value
            | _ -> failwith "Port must be an integer between 1 and 65535."
        else
            8963

    hostname, port

[<EntryPoint>]
let main argv =
    try
        let hostname, port = parseArgs argv
        startClient hostname port
        0
    with
    | :? SocketException as ex ->
        eprintfn "Connection error: %s" ex.Message
        1
    | ex ->
        eprintfn "%s" ex.Message
        1

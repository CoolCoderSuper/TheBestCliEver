module ChatClient

open System
open Chat.ClientCore

type ConnectionSettings = {
    Hostname: string
    Port: int
}

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

let renderIncomingMessage (message: string) =
    Console.WriteLine()
    Console.WriteLine(message)
    Console.Write("> ")

let isQuitCommand (message: string) =
    String.Equals(message, "/quit", StringComparison.OrdinalIgnoreCase)

let rec runInputLoop (session: ChatSession) =
    Console.Write("> ")

    match Console.ReadLine() with
    | null ->
        session.Disconnect()
    | message ->
        match tryTrimmedValue message with
        | Some trimmed when isQuitCommand trimmed ->
            session.Disconnect()
        | Some trimmed ->
            session.SendMessage(trimmed)
            runInputLoop session
        | None ->
            runInputLoop session

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

let createChatSettings (settings: ConnectionSettings) channel username password =
    {
        Hostname = settings.Hostname
        Port = settings.Port
        Channel = channel
        Username = username
        Password = password
    }

let rec connectWithPrompt (settings: ConnectionSettings) =
    let channel = promptRequired "Enter the channel name: "
    let username = promptRequired "Enter your username: "
    let password = promptRequired "Enter your password: "
    let chatSettings = createChatSettings settings channel username password

    let onEvent event =
        match event with
        | SessionEvent.MessageReceived message ->
            renderIncomingMessage message
        | SessionEvent.Disconnected _ ->
            ()

    match Chat.ClientCore.ChatClient.connect chatSettings onEvent |> Async.RunSynchronously with
    | Ok session ->
        session
    | Error ConnectError.InvalidCredentials ->
        printfn "%s" (ConnectError.toMessage ConnectError.InvalidCredentials)
        connectWithPrompt settings
    | Error error ->
        failwith (ConnectError.toMessage error)

let startClient (settings: ConnectionSettings) =
    let session = connectWithPrompt settings
    printfn "Connected as %s. Type /quit to disconnect." session.Username
    runInputLoop session

[<EntryPoint>]
let main argv =
    try
        let settings = parseArgs argv
        startClient settings
        0
    with
    | ex ->
        eprintfn "%s" ex.Message
        1

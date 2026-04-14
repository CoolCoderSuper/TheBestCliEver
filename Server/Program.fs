module ChatServer

open System
open System.Collections.Concurrent
open System.IO
open System.Net
open System.Net.Sockets
open System.Text
open System.Threading
open Server

[<Literal>]
let AuthOk = "AUTH_OK"

[<Literal>]
let AuthFail = "AUTH_FAIL"

let clients = ConcurrentDictionary<Guid, TcpClient>()
let channels = ConcurrentDictionary<string, ConcurrentDictionary<Guid, byte>>()
let clientChannels = ConcurrentDictionary<Guid, string>()
let userRepository = JsonUserRepository("users.json") :> IUserRepository

let utf8NoBom = UTF8Encoding(false)

let tryReadLine (reader: StreamReader) =
    try
        let line = reader.ReadLine()
        if isNull line then None else Some line
    with
    | :? IOException
    | :? ObjectDisposedException ->
        None

let sendLine (writer: StreamWriter) (message: string) =
    writer.WriteLine(message)

let trySendToClient (client: TcpClient) (message: string) =
    try
        let stream = client.GetStream()
        use writer = new StreamWriter(stream, utf8NoBom, 1024, true)
        writer.AutoFlush <- true
        writer.WriteLine(message)
        true
    with
    | :? IOException
    | :? SocketException
    | :? ObjectDisposedException ->
        false

let broadcastToChannel (channel: string) (message: string) =
    match channels.TryGetValue(channel) with
    | true, members ->
        for clientId in members.Keys do
            match clients.TryGetValue(clientId) with
            | true, client ->
                trySendToClient client message |> ignore
            | false, _ -> ()
    | false, _ -> ()

let addClientToChannel (clientId: Guid) (channel: string) =
    let members = channels.GetOrAdd(channel, fun _ -> ConcurrentDictionary<Guid, byte>())
    members.[clientId] <- 0uy
    clientChannels.[clientId] <- channel

let removeClientFromChannel (clientId: Guid) =
    let mutable removedChannel = Unchecked.defaultof<string>
    if clientChannels.TryRemove(clientId, &removedChannel) then
        match channels.TryGetValue(removedChannel) with
        | true, members ->
            let mutable removedMember = 0uy
            members.TryRemove(clientId, &removedMember) |> ignore

            if members.IsEmpty then
                let mutable removedMembers = Unchecked.defaultof<ConcurrentDictionary<Guid, byte>>
                channels.TryRemove(removedChannel, &removedMembers) |> ignore
        | false, _ -> ()
        Some removedChannel
    else
        None

let tryParseCredentials (credentials: string) =
    let separatorIndex = credentials.IndexOf(':')
    if separatorIndex <= 0 || separatorIndex = credentials.Length - 1 then
        None
    else
        let username = credentials.Substring(0, separatorIndex).Trim()
        let password = credentials.Substring(separatorIndex + 1).Trim()
        if String.IsNullOrWhiteSpace(username) || String.IsNullOrWhiteSpace(password) then
            None
        else
            Some(username, password)

let authenticateUser (username: string) (password: string) =
    match userRepository.LoadUser(username) with
    | Some user ->
        user.Password = password
    | None ->
        userRepository.SaveUser({ Username = username; Password = password })
        true

let rec authenticateClient (reader: StreamReader) (writer: StreamWriter) =
    match tryReadLine reader with
    | Some credentialsLine ->
        match tryParseCredentials credentialsLine with
        | Some(username, password) when authenticateUser username password ->
            sendLine writer AuthOk
            Some username
        | _ ->
            sendLine writer AuthFail
            authenticateClient reader writer
    | None ->
        None

let disconnectClient (clientId: Guid) (username: string option) =
    let channel = removeClientFromChannel clientId

    let mutable removedClient = Unchecked.defaultof<TcpClient>
    if clients.TryRemove(clientId, &removedClient) then
        try
            removedClient.Close()
        with
        | :? SocketException -> ()
        | :? ObjectDisposedException -> ()

    match channel, username with
    | Some channelName, Some user ->
        broadcastToChannel channelName (sprintf "%s has left the chat" user)
    | _ -> ()

let handleClient (client: TcpClient) =
    let clientId = Guid.NewGuid()
    clients.TryAdd(clientId, client) |> ignore
    printfn "Client connected: %A" clientId

    let mutable username: string option = None

    try
        try
            use stream = client.GetStream()
            use reader = new StreamReader(stream, utf8NoBom, false, 1024, true)
            use writer = new StreamWriter(stream, utf8NoBom, 1024, true)
            writer.AutoFlush <- true

            match tryReadLine reader with
            | Some rawChannel ->
                let channel = rawChannel.Trim()
                if not (String.IsNullOrWhiteSpace(channel)) then
                    match authenticateClient reader writer with
                    | Some authenticatedUser ->
                        username <- Some authenticatedUser
                        addClientToChannel clientId channel
                        broadcastToChannel channel (sprintf "%s has joined the chat" authenticatedUser)

                        let mutable receiving = true
                        while receiving do
                            match tryReadLine reader with
                            | Some message when not (String.IsNullOrWhiteSpace(message)) ->
                                broadcastToChannel channel (sprintf "%s: %s" authenticatedUser message)
                            | Some _ -> ()
                            | None -> receiving <- false
                    | None -> ()
            | None -> ()
        with
        | :? IOException -> ()
        | :? SocketException -> ()
        | :? ObjectDisposedException -> ()
    finally
        disconnectClient clientId username
        printfn "Client disconnected: %A" clientId

let startServer (port: int) =
    let listener = new TcpListener(IPAddress.Any, port)
    listener.Start()
    printfn "Server started on port %d" port

    while true do
        let client = listener.AcceptTcpClient()
        let clientThread = Thread(ThreadStart(fun () -> handleClient client))
        clientThread.IsBackground <- true
        clientThread.Start()

let parsePort (argv: string array) =
    if argv.Length = 0 then
        8963
    else
        match Int32.TryParse(argv.[0]) with
        | true, port when port > 0 && port <= 65535 -> port
        | _ -> failwith "Port must be an integer between 1 and 65535."

[<EntryPoint>]
let main argv =
    try
        let port = parsePort argv
        startServer port
        0
    with
    | ex ->
        eprintfn "%s" ex.Message
        1

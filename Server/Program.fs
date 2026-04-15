module ChatServer

open System
open System.Collections.Concurrent
open System.IO
open System.Net
open System.Net.Sockets
open System.Text
open Server

[<Literal>]
let AuthOk = "AUTH_OK"

[<Literal>]
let AuthFail = "AUTH_FAIL"

type ChatState = {
    clients: ConcurrentDictionary<Guid, TcpClient>
    channels: ConcurrentDictionary<string, ConcurrentDictionary<Guid, byte>>
    clientChannels: ConcurrentDictionary<Guid, string>
    userStore: UserStore
}

let utf8NoBom = UTF8Encoding(false)

let createChatState userStore =
    {
        clients = ConcurrentDictionary<Guid, TcpClient>()
        channels = ConcurrentDictionary<string, ConcurrentDictionary<Guid, byte>>()
        clientChannels = ConcurrentDictionary<Guid, string>()
        userStore = userStore
    }

let tryGetValue key (dictionary: ConcurrentDictionary<'Key, 'Value>) =
    match dictionary.TryGetValue(key) with
    | true, value -> Some value
    | false, _ -> None

let tryRemove key (dictionary: ConcurrentDictionary<'Key, 'Value>) =
    let mutable value = Unchecked.defaultof<'Value>
    if dictionary.TryRemove(key, &value) then Some value else None

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

let tryTrimmedValue (value: string) =
    if String.IsNullOrWhiteSpace(value) then
        None
    else
        Some(value.Trim())

let tryReadNonEmptyLine reader =
    tryReadLine reader |> Option.bind tryTrimmedValue

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

let broadcastToChannel (state: ChatState) (channel: string) (message: string) =
    state.channels
    |> tryGetValue channel
    |> Option.iter (fun members ->
        members.Keys
        |> Seq.choose (fun clientId -> state.clients |> tryGetValue clientId)
        |> Seq.iter (fun client -> trySendToClient client message |> ignore))

let addClientToChannel (state: ChatState) (clientId: Guid) (channel: string) =
    let members = state.channels.GetOrAdd(channel, fun _ -> ConcurrentDictionary<Guid, byte>())
    members.[clientId] <- 0uy
    state.clientChannels.[clientId] <- channel

let removeClientFromChannel (state: ChatState) (clientId: Guid) =
    state.clientChannels
    |> tryRemove clientId
    |> Option.map (fun channel ->
        state.channels
        |> tryGetValue channel
        |> Option.iter (fun members ->
            members |> tryRemove clientId |> ignore

            if members.IsEmpty then
                state.channels |> tryRemove channel |> ignore)

        channel)

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

let formatJoinMessage username = sprintf "%s has joined the chat" username

let formatLeaveMessage username = sprintf "%s has left the chat" username

let formatChatMessage username message = sprintf "%s: %s" username message

let authenticateUser (userStore: UserStore) (username: string) (password: string) =
    match userStore.loadUser username with
    | Some user when user.Password = password ->
        true
    | Some _ ->
        false
    | None ->
        userStore.saveUser { Username = username; Password = password }
        true

let rec authenticateClient (userStore: UserStore) (reader: StreamReader) (writer: StreamWriter) =
    async {
        match tryReadLine reader with
        | Some credentialsLine ->
            match tryParseCredentials credentialsLine with
            | Some(username, password) when authenticateUser userStore username password ->
                sendLine writer AuthOk
                return Some username
            | _ ->
                sendLine writer AuthFail
                return! authenticateClient userStore reader writer
        | None ->
            return None
    }

let rec receiveMessages (reader: StreamReader) onMessage =
    async {
        match tryReadLine reader with
        | Some message ->
            message |> tryTrimmedValue |> Option.iter onMessage
            return! receiveMessages reader onMessage
        | None ->
            return ()
    }

let disconnectClient (state: ChatState) (clientId: Guid) (username: string option) =
    let channel = removeClientFromChannel state clientId

    state.clients
    |> tryRemove clientId
    |> Option.iter (fun removedClient ->
        try
            removedClient.Close()
        with
        | :? SocketException
        | :? ObjectDisposedException ->
            ())

    match channel, username with
    | Some channelName, Some user ->
        broadcastToChannel state channelName (formatLeaveMessage user)
    | _ -> ()

let handleClient (state: ChatState) (client: TcpClient) =
    async {
        let clientId = Guid.NewGuid()
        state.clients.TryAdd(clientId, client) |> ignore
        printfn "Client connected: %A" clientId

        let runSession () =
            async {
                use stream = client.GetStream()
                use reader = new StreamReader(stream, utf8NoBom, false, 1024, true)
                use writer = new StreamWriter(stream, utf8NoBom, 1024, true)
                writer.AutoFlush <- true

                match tryReadNonEmptyLine reader with
                | Some channel ->
                    let! authenticatedUser = authenticateClient state.userStore reader writer

                    match authenticatedUser with
                    | Some username ->
                        addClientToChannel state clientId channel
                        broadcastToChannel state channel (formatJoinMessage username)
                        do! receiveMessages reader (fun message -> broadcastToChannel state channel (formatChatMessage username message))
                        return Some username
                    | None ->
                        return None
                | None ->
                    return None
            }

        let! username =
            async {
                try
                    return! runSession ()
                with
                | :? IOException
                | :? SocketException
                | :? ObjectDisposedException ->
                    return None
            }

        disconnectClient state clientId username
        printfn "Client disconnected: %A" clientId
    }

let startServer (port: int) =
    let state = createChatState (JsonUserStore.create "users.json")
    use listener = new TcpListener(IPAddress.Any, port)
    listener.Start()
    printfn "Server started on port %d" port

    let rec acceptLoop () =
        async {
            let! client = listener.AcceptTcpClientAsync() |> Async.AwaitTask
            handleClient state client |> Async.Start
            return! acceptLoop ()
        }

    acceptLoop () |> Async.RunSynchronously

let tryParsePort (value: string) =
    match Int32.TryParse(value) with
    | true, port when port > 0 && port <= 65535 -> Some port
    | _ -> None

let parsePort (argv: string array) =
    argv
    |> Array.tryHead
    |> Option.map (fun value ->
        value
        |> tryParsePort
        |> Option.defaultWith (fun () -> failwith "Port must be an integer between 1 and 65535."))
    |> Option.defaultValue 8963

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

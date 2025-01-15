module EchoServer

open System
open System.Net
open System.Net.Sockets
open System.Text
open System.Threading

let clients = System.Collections.Concurrent.ConcurrentDictionary<Guid, TcpClient>()
let mutable usernames = Set.empty<string>
let channels = System.Collections.Concurrent.ConcurrentDictionary<string, System.Collections.Concurrent.ConcurrentBag<Guid>>()

let broadcastMessage (channel: string) (message: string) =
    if channels.ContainsKey(channel) then
        let validClients = channels[channel]
        for client in clients |> Seq.filter (fun kvp -> validClients |> Seq.contains kvp.Key) |> Seq.map _.Value do
            let stream = client.GetStream()
            let buffer = Encoding.ASCII.GetBytes(message)
            stream.Write(buffer, 0, buffer.Length)

let sendPresenceMessage (channel: string) (username: string) =
    let message = sprintf "%s has joined the chat" username
    broadcastMessage channel message

let sendDisconnectionMessage (channel: string) (username: string) =
    let message = sprintf "%s has left the chat" username
    broadcastMessage channel message

let handleClient (client: TcpClient) =
    let clientId = Guid.NewGuid()
    clients.TryAdd(clientId, client) |> ignore
    use stream = client.GetStream()
    let buffer = Array.zeroCreate 1024
    let rec loop (channel: string option) (username: string option) =
        try
            let bytesRead = stream.Read(buffer, 0, buffer.Length)
            if bytesRead > 0 then
                let data = Encoding.ASCII.GetString(buffer, 0, bytesRead)
                printfn "Received: %s" data
                match channel, username with
                | None, _ ->
                    let newChannel = data.Trim()
                    if not (channels.ContainsKey(newChannel)) then
                        channels.TryAdd(newChannel, System.Collections.Concurrent.ConcurrentBag<Guid>()) |> ignore
                    channels.[newChannel].Add(clientId) |> ignore
                    loop (Some newChannel) username
                | Some _, None ->
                    let newUsername = data.Trim()
                    if Set.contains newUsername usernames then
                        let errorMessage = "Username already taken. Please choose a different username."
                        let errorBuffer = Encoding.ASCII.GetBytes(errorMessage)
                        stream.Write(errorBuffer, 0, errorBuffer.Length)
                        loop channel None
                    else
                        usernames <- Set.add newUsername usernames
                        sendPresenceMessage (channel.Value) newUsername
                        loop channel (Some newUsername)
                | Some ch, Some _ ->
                    broadcastMessage ch data
                    loop channel username
            else
                let mutable removedClient = Unchecked.defaultof<TcpClient>
                clients.TryRemove(clientId, &removedClient) |> ignore
                match channel, username with
                | Some ch, Some u -> 
                    usernames <- Set.remove u usernames
                    sendDisconnectionMessage ch u
                | _ -> ()
                printfn "Client disconnected"
        with
        | :? System.IO.IOException ->
            let mutable removedClient = Unchecked.defaultof<TcpClient>
            clients.TryRemove(clientId, &removedClient) |> ignore
            match channel, username with
            | Some ch, Some u -> 
                usernames <- Set.remove u usernames
                sendDisconnectionMessage ch u
            | _ -> ()
            printfn "Client disconnected"
    loop None None

let startServer (port: int) =
    let listener = new TcpListener(IPAddress.Any, port)
    listener.Start()
    printfn "Server started on port %d" port
    let rec acceptLoop () =
        let client = listener.AcceptTcpClient()
        printfn "Client connected"
        let clientThread = new Thread(ThreadStart(fun () -> handleClient(client)))
        clientThread.Start()
        acceptLoop()
    acceptLoop()

[<EntryPoint>]
let main argv =
    let port = if argv.Length > 0 then Int32.Parse(argv.[0]) else 8963
    startServer port
    0

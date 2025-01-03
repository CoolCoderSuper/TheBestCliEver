module EchoServer

open System
open System.Net
open System.Net.Sockets
open System.Text
open System.Threading

let clients = System.Collections.Concurrent.ConcurrentDictionary<Guid, TcpClient>()

let broadcastMessage (message: string) =
    for client in clients.Values do
        let stream = client.GetStream()
        let buffer = Encoding.ASCII.GetBytes(message)
        stream.Write(buffer, 0, buffer.Length)

let sendPresenceMessage (username: string) =
    let message = sprintf "%s has joined the chat" username
    broadcastMessage message

let handleClient (client: TcpClient) =
    let clientId = Guid.NewGuid()
    clients.TryAdd(clientId, client) |> ignore
    use stream = client.GetStream()
    let buffer = Array.zeroCreate 1024
    let rec loop (username: string option) =
        try
            let bytesRead = stream.Read(buffer, 0, buffer.Length)
            if bytesRead > 0 then
                let data = Encoding.ASCII.GetString(buffer, 0, bytesRead)
                printfn "Received: %s" data
                match username with
                | None ->
                    let newUsername = data.Trim()
                    sendPresenceMessage newUsername
                    loop (Some newUsername)
                | Some _ ->
                    broadcastMessage data
                    loop username
            else
                let mutable removedClient = Unchecked.defaultof<TcpClient>
                clients.TryRemove(clientId, &removedClient) |> ignore
                printfn "Client disconnected"
        with
        | :? System.IO.IOException ->
            let mutable removedClient = Unchecked.defaultof<TcpClient>
            clients.TryRemove(clientId, &removedClient) |> ignore
            printfn "Client disconnected"
    loop None

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

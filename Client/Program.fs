module EchoClient

open System
open System.Net.Sockets
open System.Text
open System.Threading

let mutable rendering = false

let drawUI (messages: string list) =
    while rendering do
        Thread.Sleep(10)
    rendering <- true
    Console.Clear()
    for message in messages do
        printfn "%s" message
    Console.SetCursorPosition(0, Console.WindowHeight - 1)
    printf "Enter Message: "
    rendering <- false

let receiveMessages (client: TcpClient) (messages: string list ref) =
    let stream = client.GetStream()
    let buffer = Array.zeroCreate 1024
    let rec loop () =
        try
            let bytesRead = stream.Read(buffer, 0, buffer.Length)
            if bytesRead > 0 then
                let data = Encoding.ASCII.GetString(buffer, 0, bytesRead)
                messages := data :: !messages
                drawUI !messages
                loop()
        with
        | :? System.IO.IOException -> ()
    loop()

let sendMessage (client: TcpClient) (username: string) (message: string) =
    let stream = client.GetStream()
    let fullMessage = sprintf "%s: %s" username message
    let buffer = Encoding.ASCII.GetBytes(fullMessage)
    stream.Write(buffer, 0, buffer.Length)

let promptUsername () =
    printf "Enter your username: "
    Console.ReadLine()

let startClient (hostname: string) (port: int) =
    let client = new TcpClient(hostname, port)
    let messages = ref []
    let receiveThread = new Thread(ThreadStart(fun () -> receiveMessages client messages))
    receiveThread.Start()
    let username = promptUsername()
    let rec loop () =
        drawUI !messages
        let message = Console.ReadLine()
        if not (String.IsNullOrEmpty(message)) then
            sendMessage client username message
            loop()
    loop()

[<EntryPoint>]
let main argv =
    let hostname = if argv.Length > 0 then argv.[0] else "localhost"
    let port = if argv.Length > 1 then Int32.Parse(argv.[1]) else 8963
    startClient hostname port
    0

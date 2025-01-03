module EchoClient

open System
open System.Net.Sockets
open System.Text
open System.Threading

let receiveMessages (client: TcpClient) =
    let stream = client.GetStream()
    let buffer = Array.zeroCreate 1024
    let rec loop () =
        try
            let bytesRead = stream.Read(buffer, 0, buffer.Length)
            if bytesRead > 0 then
                let data = Encoding.ASCII.GetString(buffer, 0, bytesRead)
                Console.SetCursorPosition(0, Console.CursorTop)
                printfn "\n%s" data
                printfn "Enter Message: "
                loop()
        with
        | :? System.IO.IOException -> ()
    loop()

let sendMessage (client: TcpClient) (message: string) =
    let stream = client.GetStream()
    let buffer = Encoding.ASCII.GetBytes(message)
    stream.Write(buffer, 0, buffer.Length)

let startClient (hostname: string) (port: int) =
    let client = new TcpClient(hostname, port)
    let receiveThread = new Thread(ThreadStart(fun () -> receiveMessages(client)))
    receiveThread.Start()
    let rec loop () =
        printf "Enter Message: "
        let message = Console.ReadLine()
        if not (String.IsNullOrEmpty(message)) then
            sendMessage client message
            loop()
    loop()

[<EntryPoint>]
let main argv =
    let hostname = if argv.Length > 0 then argv.[0] else "localhost"
    let port = if argv.Length > 1 then Int32.Parse(argv.[1]) else 8963
    startClient hostname port
    0

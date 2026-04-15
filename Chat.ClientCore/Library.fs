namespace Chat.ClientCore

open System
open System.IO
open System.Net.Sockets
open System.Text
open System.Threading

type ConnectionSettings = {
    Hostname: string
    Port: int
    Channel: string
    Username: string
    Password: string
}

[<RequireQualifiedAccess>]
type ConnectError =
    | InvalidConfiguration of string
    | InvalidCredentials
    | UnexpectedAuthenticationResponse of string option
    | DisconnectedDuringAuthentication
    | ConnectionFailed of string

module ConnectError =
    let toMessage error =
        match error with
        | ConnectError.InvalidConfiguration message ->
            message
        | ConnectError.InvalidCredentials ->
            "Invalid username or password. Please try again."
        | ConnectError.UnexpectedAuthenticationResponse _ ->
            "Unexpected response from server. Please try again."
        | ConnectError.DisconnectedDuringAuthentication ->
            "Disconnected during authentication."
        | ConnectError.ConnectionFailed message when String.IsNullOrWhiteSpace(message) ->
            "Connection error."
        | ConnectError.ConnectionFailed message ->
            sprintf "Connection error: %s" message

[<RequireQualifiedAccess>]
type SessionEvent =
    | MessageReceived of string
    | Disconnected of string option

[<Sealed>]
type ChatSession internal (username: string, sendMessage: string -> unit, disconnect: unit -> unit) =
    member _.Username = username

    member _.SendMessage(message: string) =
        sendMessage message

    member _.Disconnect() =
        disconnect ()

    interface IDisposable with
        member this.Dispose() =
            this.Disconnect()

module ChatClient =
    [<Literal>]
    let AuthOk = "AUTH_OK"

    [<Literal>]
    let AuthFail = "AUTH_FAIL"

    let private utf8NoBom = UTF8Encoding(false)

    let private tryTrimmedValue (value: string) =
        if String.IsNullOrWhiteSpace(value) then
            None
        else
            Some(value.Trim())

    let private requireValue label value =
        match tryTrimmedValue value with
        | Some trimmed ->
            Ok trimmed
        | None ->
            Error(ConnectError.InvalidConfiguration(sprintf "%s cannot be empty." label))

    let private validateSettings (settings: ConnectionSettings) =
        if settings.Port < 1 || settings.Port > 65535 then
            Error(ConnectError.InvalidConfiguration("Port must be an integer between 1 and 65535."))
        else
            match requireValue "Hostname" settings.Hostname with
            | Error error ->
                Error error
            | Ok hostname ->
                match requireValue "Channel" settings.Channel with
                | Error error ->
                    Error error
                | Ok channel ->
                    match requireValue "Username" settings.Username with
                    | Error error ->
                        Error error
                    | Ok username ->
                        match requireValue "Password" settings.Password with
                        | Error error ->
                            Error error
                        | Ok password ->
                            Ok {
                                Hostname = hostname
                                Port = settings.Port
                                Channel = channel
                                Username = username
                                Password = password
                            }

    let private sendLine (writer: StreamWriter) (message: string) =
        writer.WriteLine(message)

    let private tryReadLine (reader: StreamReader) =
        try
            let line = reader.ReadLine()
            if isNull line then None else Some line
        with
        | :? IOException
        | :? ObjectDisposedException ->
            None

    let private closeConnection (client: TcpClient) (reader: StreamReader option) (writer: StreamWriter option) =
        writer
        |> Option.iter (fun value ->
            try
                value.Dispose()
            with
            | :? IOException
            | :? ObjectDisposedException ->
                ())

        reader
        |> Option.iter (fun value ->
            try
                value.Dispose()
            with
            | :? IOException
            | :? ObjectDisposedException ->
                ())

        try
            client.Close()
        with
        | :? SocketException
        | :? ObjectDisposedException ->
            ()

    let private buildCredentials username password =
        sprintf "%s:%s" username password

    let connect (settings: ConnectionSettings) (onEvent: SessionEvent -> unit) =
        async {
            match validateSettings settings with
            | Error error ->
                return Error error
            | Ok validatedSettings ->
                let client = new TcpClient()
                let mutable reader: StreamReader option = None
                let mutable writer: StreamWriter option = None
                let disconnected = ref 0
                let closed = ref 0

                let notifyDisconnected reason =
                    if Interlocked.Exchange(disconnected, 1) = 0 then
                        onEvent (SessionEvent.Disconnected reason)

                let disconnect () =
                    if Interlocked.Exchange(closed, 1) = 0 then
                        closeConnection client reader writer

                let sendMessage message =
                    message
                    |> tryTrimmedValue
                    |> Option.iter (fun trimmed ->
                        try
                            writer |> Option.iter (fun activeWriter -> sendLine activeWriter trimmed)
                        with
                        | :? IOException as ex ->
                            notifyDisconnected (Some ex.Message)
                            disconnect ()
                        | :? ObjectDisposedException ->
                            notifyDisconnected None
                            disconnect ())

                try
                    do! client.ConnectAsync(validatedSettings.Hostname, validatedSettings.Port) |> Async.AwaitTask

                    let stream = client.GetStream()
                    let activeReader = new StreamReader(stream, utf8NoBom, false, 1024, true)
                    let activeWriter = new StreamWriter(stream, utf8NoBom, 1024, true)
                    activeWriter.AutoFlush <- true

                    reader <- Some activeReader
                    writer <- Some activeWriter

                    sendLine activeWriter validatedSettings.Channel
                    sendLine activeWriter (buildCredentials validatedSettings.Username validatedSettings.Password)

                    match tryReadLine activeReader with
                    | Some AuthOk ->
                        let rec receiveLoop () =
                            async {
                                match tryReadLine activeReader with
                                | Some message ->
                                    onEvent (SessionEvent.MessageReceived message)
                                    return! receiveLoop ()
                                | None ->
                                    notifyDisconnected None
                                    disconnect ()
                            }

                        receiveLoop () |> Async.Start
                        return Ok(ChatSession(validatedSettings.Username, sendMessage, disconnect))
                    | Some AuthFail ->
                        disconnect ()
                        return Error ConnectError.InvalidCredentials
                    | Some response ->
                        disconnect ()
                        return Error(ConnectError.UnexpectedAuthenticationResponse(Some response))
                    | None ->
                        disconnect ()
                        return Error ConnectError.DisconnectedDuringAuthentication
                with
                | :? SocketException as ex ->
                    disconnect ()
                    return Error(ConnectError.ConnectionFailed ex.Message)
                | :? IOException as ex ->
                    disconnect ()
                    return Error(ConnectError.ConnectionFailed ex.Message)
                | :? ObjectDisposedException as ex ->
                    disconnect ()
                    return Error(ConnectError.ConnectionFailed ex.Message)
        }

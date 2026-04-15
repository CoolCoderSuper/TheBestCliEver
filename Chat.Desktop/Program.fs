namespace Chat.Desktop

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.FuncUI
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Hosts
open Avalonia.Layout
open Avalonia.Styling
open Avalonia.Themes.Fluent
open Avalonia.Threading
open Chat.ClientCore

type Model = {
    Hostname: string
    PortText: string
    Channel: string
    Username: string
    Password: string
    DraftMessage: string
    Messages: string list
    StatusMessage: string option
    Session: ChatSession option
    IsConnecting: bool
}

module Main =
    let initialModel = {
        Hostname = "localhost"
        PortText = "8963"
        Channel = "general"
        Username = ""
        Password = ""
        DraftMessage = ""
        Messages = []
        StatusMessage = Some "Enter a channel and credentials, then connect."
        Session = None
        IsConnecting = false
    }

    let tryTrimmedValue (value: string) =
        if String.IsNullOrWhiteSpace(value) then
            None
        else
            Some(value.Trim())

    let tryParsePort (value: string) =
        match Int32.TryParse(value) with
        | true, port when port > 0 && port <= 65535 -> Some port
        | _ -> None

    let buildHistory (messages: string list) =
        messages
        |> List.rev
        |> String.concat Environment.NewLine

    let field label control =
        StackPanel.create [
            StackPanel.spacing 4.0
            StackPanel.children [
                TextBlock.create [
                    TextBlock.text label
                ]
                control
            ]
        ]

    let view =
        Component(fun ctx ->
            let state = ctx.useState initialModel

            let update updater =
                state.Set(updater state.Current)

            let postUpdate updater =
                Dispatcher.UIThread.Post(fun _ -> state.Set(updater state.Current))

            let disconnectActiveSession () =
                state.Current.Session |> Option.iter (fun session -> session.Disconnect())

                update (fun current ->
                    {
                        current with
                            Session = None
                            IsConnecting = false
                            DraftMessage = ""
                            StatusMessage = Some "Disconnected."
                    })

            let connect () =
                match tryParsePort state.Current.PortText with
                | None ->
                    update (fun current ->
                        {
                            current with
                                StatusMessage = Some "Port must be an integer between 1 and 65535."
                        })
                | Some port ->
                    let request: ConnectionSettings = {
                        Hostname = state.Current.Hostname
                        Port = port
                        Channel = state.Current.Channel
                        Username = state.Current.Username
                        Password = state.Current.Password
                    }

                    let onEvent event =
                        match event with
                        | SessionEvent.MessageReceived message ->
                            postUpdate (fun current ->
                                {
                                    current with
                                        Messages = message :: current.Messages
                                })
                        | SessionEvent.Disconnected reason ->
                            let status =
                                reason
                                |> Option.bind tryTrimmedValue
                                |> Option.defaultValue "Disconnected from server."

                            postUpdate (fun current ->
                                {
                                    current with
                                        Session = None
                                        IsConnecting = false
                                        DraftMessage = ""
                                        StatusMessage = Some status
                                })

                    update (fun current ->
                        {
                            current with
                                IsConnecting = true
                                Session = None
                                DraftMessage = ""
                                Messages = []
                                StatusMessage = Some "Connecting..."
                        })

                    async {
                        match! Chat.ClientCore.ChatClient.connect request onEvent with
                        | Ok session ->
                            postUpdate (fun current ->
                                {
                                    current with
                                        Session = Some session
                                        IsConnecting = false
                                        StatusMessage = Some(sprintf "Connected as %s." session.Username)
                                })
                        | Error error ->
                            postUpdate (fun current ->
                                {
                                    current with
                                        Session = None
                                        IsConnecting = false
                                        StatusMessage = Some(ConnectError.toMessage error)
                                })
                    }
                    |> Async.Start

            let sendCurrentMessage () =
                match state.Current.Session, tryTrimmedValue state.Current.DraftMessage with
                | Some session, Some message when String.Equals(message, "/quit", StringComparison.OrdinalIgnoreCase) ->
                    disconnectActiveSession ()
                | Some session, Some message ->
                    session.SendMessage(message)

                    update (fun current ->
                        {
                            current with
                                DraftMessage = ""
                        })
                | _ ->
                    ()

            let current = state.Current
            let canEditConnection = not current.IsConnecting && current.Session.IsNone
            let canConnect = canEditConnection
            let canDisconnect = current.Session.IsSome || current.IsConnecting
            let canSend = current.Session.IsSome && tryTrimmedValue current.DraftMessage |> Option.isSome
            let historyText = buildHistory current.Messages

            DockPanel.create [
                DockPanel.lastChildFill true
                DockPanel.children [
                    Border.create [
                        DockPanel.dock Dock.Top
                        Border.padding 16
                        Border.child (
                            StackPanel.create [
                                StackPanel.spacing 12.0
                                StackPanel.children [
                                    TextBlock.create [
                                        TextBlock.text "TCP Chat Desktop"
                                        TextBlock.fontSize 24.0
                                    ]
                                    StackPanel.create [
                                        StackPanel.orientation Orientation.Horizontal
                                        StackPanel.spacing 12.0
                                        StackPanel.children [
                                            field
                                                "Host"
                                                (TextBox.create [
                                                    TextBox.text current.Hostname
                                                    TextBox.isEnabled canEditConnection
                                                    TextBox.onTextChanged (fun value ->
                                                        update (fun model ->
                                                            {
                                                                model with
                                                                    Hostname = value
                                                            }))
                                                ])
                                            field
                                                "Port"
                                                (TextBox.create [
                                                    TextBox.text current.PortText
                                                    TextBox.isEnabled canEditConnection
                                                    TextBox.onTextChanged (fun value ->
                                                        update (fun model ->
                                                            {
                                                                model with
                                                                    PortText = value
                                                            }))
                                                ])
                                            field
                                                "Channel"
                                                (TextBox.create [
                                                    TextBox.text current.Channel
                                                    TextBox.isEnabled canEditConnection
                                                    TextBox.onTextChanged (fun value ->
                                                        update (fun model ->
                                                            {
                                                                model with
                                                                    Channel = value
                                                            }))
                                                ])
                                        ]
                                    ]
                                    StackPanel.create [
                                        StackPanel.orientation Orientation.Horizontal
                                        StackPanel.spacing 12.0
                                        StackPanel.children [
                                            field
                                                "Username"
                                                (TextBox.create [
                                                    TextBox.text current.Username
                                                    TextBox.isEnabled canEditConnection
                                                    TextBox.onTextChanged (fun value ->
                                                        update (fun model ->
                                                            {
                                                                model with
                                                                    Username = value
                                                            }))
                                                ])
                                            field
                                                "Password"
                                                (TextBox.create [
                                                    TextBox.text current.Password
                                                    TextBox.passwordChar '*'
                                                    TextBox.isEnabled canEditConnection
                                                    TextBox.onTextChanged (fun value ->
                                                        update (fun model ->
                                                            {
                                                                model with
                                                                    Password = value
                                                            }))
                                                ])
                                        ]
                                    ]
                                    StackPanel.create [
                                        StackPanel.orientation Orientation.Horizontal
                                        StackPanel.spacing 12.0
                                        StackPanel.children [
                                            Button.create [
                                                Button.content
                                                    (if current.IsConnecting then
                                                         "Connecting..."
                                                     else
                                                         "Connect")
                                                Button.isEnabled canConnect
                                                Button.onClick (fun _ -> connect ())
                                            ]
                                            Button.create [
                                                Button.content "Disconnect"
                                                Button.isEnabled canDisconnect
                                                Button.onClick (fun _ -> disconnectActiveSession ())
                                            ]
                                        ]
                                    ]
                                    TextBlock.create [
                                        TextBlock.text (current.StatusMessage |> Option.defaultValue "")
                                    ]
                                ]
                            ]
                        )
                    ]
                    Border.create [
                        DockPanel.dock Dock.Bottom
                        Border.padding 16
                        Border.child (
                            StackPanel.create [
                                StackPanel.spacing 8.0
                                StackPanel.children [
                                    TextBlock.create [
                                        TextBlock.text "Message"
                                    ]
                                    StackPanel.create [
                                        StackPanel.orientation Orientation.Horizontal
                                        StackPanel.spacing 12.0
                                        StackPanel.children [
                                            TextBox.create [
                                                TextBox.text current.DraftMessage
                                                TextBox.isEnabled current.Session.IsSome
                                                TextBox.onTextChanged (fun value ->
                                                    update (fun model ->
                                                        {
                                                            model with
                                                                DraftMessage = value
                                                        }))
                                            ]
                                            Button.create [
                                                Button.content "Send"
                                                Button.isEnabled canSend
                                                Button.onClick (fun _ -> sendCurrentMessage ())
                                            ]
                                        ]
                                    ]
                                ]
                            ]
                        )
                    ]
                    Border.create [
                        Border.padding 16
                        Border.child (
                            StackPanel.create [
                                StackPanel.spacing 8.0
                                StackPanel.children [
                                    TextBlock.create [
                                        TextBlock.text "Chat History"
                                    ]
                                    TextBox.create [
                                        TextBox.text historyText
                                        TextBox.isReadOnly true
                                        TextBox.acceptsReturn true
                                        TextBox.textWrapping TextWrapping.Wrap
                                        TextBox.caretIndex historyText.Length
                                    ]
                                ]
                            ]
                        )
                    ]
                ]
            ])

type MainWindow() as this =
    inherit HostWindow()

    do
        this.Title <- "TCP Chat Desktop"
        this.Width <- 960.0
        this.Height <- 720.0
        this.Content <- Main.view

type App() =
    inherit Application()

    override this.Initialize() =
        this.Styles.Add(FluentTheme())
        this.RequestedThemeVariant <- ThemeVariant.Dark

    override this.OnFrameworkInitializationCompleted() =
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktopLifetime ->
            desktopLifetime.MainWindow <- MainWindow()
        | _ ->
            ()

module Program =
    [<EntryPoint>]
    let main(args: string[]) =
        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .UseSkia()
            .StartWithClassicDesktopLifetime(args)

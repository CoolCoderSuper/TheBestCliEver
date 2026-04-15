# TCP Chat

A simple multi-channel TCP chat application written in F#.

The solution contains:

- `Server`: accepts client connections, authenticates users, and broadcasts messages.
- `Client`: console app for joining a channel and sending/receiving messages.
- `Chat.ClientCore`: shared TCP chat protocol/client library used by both frontends.
- `Chat.Desktop`: Avalonia.FuncUI desktop frontend for connecting and chatting visually.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- A terminal (use multiple terminal windows to run server + clients)

## Project Structure

- `TcpChat.sln`: solution file including server, console client, shared client core, and desktop app
- `Server/Program.fs`: chat server with functional state and async session flow
- `Server/UserStore.fs`: shared domain types and functional storage dependency record
- `Server/JsonUserStore.fs`: JSON-backed user storage implemented with pure transforms + IO boundary
- `Chat.ClientCore/Library.fs`: reusable TCP client connection/auth/session logic
- `Client/Program.fs`: console chat client now powered by the shared client core
- `Chat.Desktop/Program.fs`: Avalonia.FuncUI desktop application

## Build

From the repository root:

```sh
dotnet build
```

Or build projects individually:

```sh
dotnet build Server/Server.fsproj
dotnet build Client/Client.fsproj
dotnet build Chat.Desktop/Chat.Desktop.fsproj
```

## Run

### 1) Start the server

Default port is `8963`:

```sh
dotnet run --project Server
```

Specify a custom port:

```sh
dotnet run --project Server -- 1234
```

### 2) Start one or more clients

Default target is `localhost:8963`:

```sh
dotnet run --project Client
```

Connect to a custom host/port:

```sh
dotnet run --project Client -- <hostname> <port>
```

### 3) Start the Avalonia desktop client

Default target is `localhost:8963` and the default channel is `general`:

```sh
dotnet run --project Chat.Desktop
```

From the desktop UI you can:

1. Enter host, port, channel, username, and password.
2. Connect using the same authentication flow as the console client.
3. Read channel history in the chat window and send new messages from the input box.
4. Disconnect without closing the application.

## How Operation Works

When a client starts:

1. Enter a **channel name**.
2. Enter **username** and **password**.
3. If the username is new, it is created automatically.
4. If the username exists, the password must match.
5. After successful authentication, the client joins the channel and can chat.

Behavior details:

- Messages are broadcast only to users in the same channel.
- Join/leave presence messages are sent to channel members.
- Type `/quit` in the client to disconnect.

## Authentication and Storage

- User credentials are stored in `users.json` (created automatically on first registration).
- `users.json` is local file storage intended for development/demo usage.
- Storage is wired through a functional `UserStore` record, which keeps persistence swappable without relying on an OO repository interface.

## Troubleshooting

- **Connection error**: verify server is running and host/port are correct.
- **Auth keeps failing**: ensure username/password are entered correctly at prompts (values cannot be blank).
- **Port parse error**: pass a port in range `1-65535`.

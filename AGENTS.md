# AGENTS.md

## Cursor Cloud specific instructions

### Overview

TCP Chat is an F# application with two console projects (Server and Client) targeting .NET 8. There are no NuGet package dependencies, no database, and no Docker — only the .NET 8 SDK is required.

### Build

There is no `.sln` file. Build each project individually:

```sh
dotnet build Server/Server.fsproj
dotnet build Client/Client.fsproj
```

### Running

- **Server:** `dotnet run --project Server` (default port 8963, or pass a custom port as an argument)
- **Client:** `dotnet run --project Client` (interactive console app — requires stdin for channel name, username, password, and messages)

The Client is fully interactive and cannot be driven non-interactively with `dotnet run`. For automated testing, connect directly via raw TCP sockets (e.g. Python `socket` module) and send the protocol lines: channel name, then `username:password`, then chat messages.

### Protocol (for automated testing)

1. Client sends channel name (one line)
2. Client sends `username:password` (one line)
3. Server responds with `AUTH_OK` or `AUTH_FAIL`
4. After `AUTH_OK`, client sends chat messages (one line each); server broadcasts to all channel members

### User storage

User credentials are auto-persisted to `users.json` in the working directory. This file is created on first registration. Delete it to reset all accounts.

### .NET SDK path

The .NET 8 SDK is installed to `$HOME/.dotnet`. The PATH export is in `~/.bashrc`. If `dotnet` is not found, run:

```sh
export PATH="$HOME/.dotnet:$PATH"
```

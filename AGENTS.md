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

The Client is interactive (reads from stdin). To test it in a headless/agent environment, run it inside a tmux session and use `tmux send-keys` to feed input line by line. Wait ~4 seconds after `dotnet run --project Client` for the F# compilation/startup before sending the first input.

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

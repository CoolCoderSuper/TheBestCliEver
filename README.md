# TCP Chat

This is simple TCP chat implemented in F#.

## Building the Server

To build the server, you need to have the .NET SDK installed. You can download it from [here](https://dotnet.microsoft.com/download).

Once you have the .NET SDK installed, navigate to the `Server` directory and run the following command:

```sh
dotnet build
```

## Running the Server

To run the server, use the following command:

```sh
dotnet run --project Server
```

By default, the server will start on port 8080. You can specify a different port by providing it as an argument:

```sh
dotnet run --project Server -- 1234
```

The server now supports multiple channels. When a client connects, they will be prompted to enter a channel name. Messages will be broadcasted only to clients in the same channel.

## Building the Client

To build the client, you need to have the .NET SDK installed. You can download it from [here](https://dotnet.microsoft.com/download).

Once you have the .NET SDK installed, navigate to the `Client` directory and run the following command:

```sh
dotnet build
```

## Running the Client

To run the client, use the following command:

```sh
dotnet run --project Client
```

By default, the client will connect to `localhost` on port `8963`. You can specify a different hostname and port by providing them as arguments:

```sh
dotnet run --project Client -- <hostname> <port>
```

When the client connects, they will be prompted to enter a channel name. Messages will be sent only to clients in the same channel.

## User Authentication

The server now supports user authentication. When a client connects, they will be prompted to enter a username and password. The server will authenticate the user using the saved user information. If the username and password are valid, the user will be allowed to join the chat. If the username and password are invalid, the user will be prompted to enter the username and password again.

The user information is saved to a JSON file. This makes it easy to swap out the JSON file for a real database in the future.

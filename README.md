# Echo TCP Server

This is a simple echo TCP server implemented in F#.

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

## Connecting to the Server

You can connect to the server using `telnet`. Open a terminal and run the following command:

```sh
telnet localhost 8963
```

Replace `8963` with the port number you specified when starting the server.

Once connected, you can type messages, and the server will echo them back to you.

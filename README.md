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
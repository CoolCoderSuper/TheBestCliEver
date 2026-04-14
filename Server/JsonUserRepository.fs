namespace Server

open System
open System.IO
open System.Text.Json

type JsonUserRepository(filePath: string) =
    let fileLock = obj()
    let serializerOptions = JsonSerializerOptions(WriteIndented = true)

    let readUsersUnsafe () =
        if not (File.Exists(filePath)) then
            []
        else
            let json = File.ReadAllText(filePath)
            if String.IsNullOrWhiteSpace(json) then
                []
            else
                try
                    let users = JsonSerializer.Deserialize<User array>(json, serializerOptions)
                    if isNull users then [] else users |> Array.toList
                with
                | :? JsonException ->
                    []

    let writeUsersUnsafe (users: User list) =
        let fullPath = Path.GetFullPath(filePath)
        let directory = Path.GetDirectoryName(fullPath)

        if not (String.IsNullOrEmpty(directory)) then
            Directory.CreateDirectory(directory) |> ignore

        let json = JsonSerializer.Serialize(users, serializerOptions)
        File.WriteAllText(filePath, json)

    interface IUserRepository with
        member _.SaveUser(user: User) =
            lock fileLock (fun () ->
                let users = readUsersUnsafe ()
                let updatedUsers =
                    users
                    |> List.filter (fun existing -> existing.Username <> user.Username)
                    |> fun remaining -> user :: remaining
                    |> List.sortBy (fun u -> u.Username)

                writeUsersUnsafe updatedUsers)

        member _.LoadUser(username: string) =
            lock fileLock (fun () ->
                readUsersUnsafe ()
                |> List.tryFind (fun user ->
                    String.Equals(user.Username, username, StringComparison.Ordinal)))

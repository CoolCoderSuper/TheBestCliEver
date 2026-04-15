namespace Server

open System
open System.IO
open System.Text.Json

module JsonUserStore =
    let private serializerOptions = JsonSerializerOptions(WriteIndented = true)

    let private normalizeUsers (users: User list) =
        users |> List.sortBy (fun user -> user.Username)

    let private tryFindUser (username: string) (users: User list) =
        users
        |> List.tryFind (fun user ->
            String.Equals(user.Username, username, StringComparison.Ordinal))

    let private upsertUser (user: User) (users: User list) =
        users
        |> List.filter (fun existing -> existing.Username <> user.Username)
        |> fun remaining -> user :: remaining
        |> normalizeUsers

    let private readUsersUnsafe (filePath: string) =
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

    let private writeUsersUnsafe (filePath: string) (users: User list) =
        let fullPath = Path.GetFullPath(filePath)
        let directory = Path.GetDirectoryName(fullPath)

        if not (String.IsNullOrEmpty(directory)) then
            Directory.CreateDirectory(directory) |> ignore

        let json = JsonSerializer.Serialize(normalizeUsers users, serializerOptions)
        File.WriteAllText(filePath, json)

    let create (filePath: string) =
        let fileLock = obj()

        let saveUser user =
            lock fileLock (fun () ->
                readUsersUnsafe filePath
                |> upsertUser user
                |> writeUsersUnsafe filePath)

        let loadUser username =
            lock fileLock (fun () ->
                readUsersUnsafe filePath
                |> tryFindUser username)

        {
            saveUser = saveUser
            loadUser = loadUser
        }

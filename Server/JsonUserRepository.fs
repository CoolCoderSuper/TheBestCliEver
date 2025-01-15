namespace Server

open System.IO
open System.Text.Json

type JsonUserRepository(filePath: string) =
    interface IUserRepository with
        member this.SaveUser(user: User) =
            let users = 
                if File.Exists(filePath) then
                    let json = File.ReadAllText(filePath)
                    JsonSerializer.Deserialize<User list>(json)
                else
                    []
            let updatedUsers = user :: users
            let json = JsonSerializer.Serialize(updatedUsers)
            File.WriteAllText(filePath, json)

        member this.LoadUser(username: string) =
            if File.Exists(filePath) then
                let json = File.ReadAllText(filePath)
                let users = JsonSerializer.Deserialize<User list>(json)
                users |> List.tryFind (fun u -> u.Username = username)
            else
                None

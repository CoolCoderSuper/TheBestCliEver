namespace Server

type User = {
    Username: string
    Password: string
}

type IUserRepository =
    abstract member SaveUser: User -> unit
    abstract member LoadUser: string -> User option

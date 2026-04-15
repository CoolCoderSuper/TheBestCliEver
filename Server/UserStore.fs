namespace Server

type User = {
    Username: string
    Password: string
}

type UserStore = {
    saveUser: User -> unit
    loadUser: string -> User option
}

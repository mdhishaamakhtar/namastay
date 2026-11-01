namespace Namastay.Shared.Contracts

type Credentials = { Email: string; Password: string }

[<RequireQualifiedAccess>]
type SignInProvider =
    | Password
    | Google

type Account = {
    Email: string
    Provider: SignInProvider
}

type SavedTripSummary = {
    Id: string
    ShareToken: string
    Title: string
    Days: int
    SavedOn: string
}

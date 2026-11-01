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

type SignInOptions = {
    Google: bool
    Photo: GuidePhoto option
}

type SavedTripSummary = {
    Id: string
    ShareToken: string
    Title: string
    Days: int
    SavedOn: string
    Route: string list
    Photo: GuidePhoto option
    Colours: PlaceColours option
}

namespace Namastay.Shared.Contracts

type Credentials = { Email: string; Password: string }

[<RequireQualifiedAccess>]
type SignInProvider =
    | Password
    | Google

type Account = {
    Email: string
    Provider: SignInProvider
    HasPassword: bool
}

type NewPassword = { Password: string }

/// What a native app keeps after signing in, and sends as a bearer token.
type TokenGrant = { Token: string; Account: Account }

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

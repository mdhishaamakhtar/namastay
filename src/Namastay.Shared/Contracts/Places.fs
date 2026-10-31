namespace Namastay.Shared.Contracts

/// Search result sent to the client. Only Fable-compatible types belong in this project.
type PlaceSummary = {
    Slug: string
    Name: string
    Region: string
    FamilyDeep: string
    Published: bool
}

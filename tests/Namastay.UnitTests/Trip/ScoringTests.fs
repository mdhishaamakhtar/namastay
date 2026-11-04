module Namastay.UnitTests.Trip.ScoringTests

open Xunit
open Namastay.Domain.Catalog
open Namastay.Domain.Trip
open Namastay.Domain.Trip.Profile
open Namastay.Shared.Contracts

let private request = TripBuilderTests.request [] 7 None

let private wants interests : TravellerProfile = {
    Interests = interests
    Parents = Map.ofList [ "snow-peaks", "mountains" ]
    Depth = 0
    Offbeat = 0
    Collected = []
    Likes = Map.empty
    Declined = []
}

let private strongIn interest =
    TripBuilderTests.place interest "c-hills" "pine" interest

let private taste profile place = Scoring.taste profile request place

[<Fact>]
let ``a child interest counts half towards a liked parent`` () =
    let profile = wants [ "mountains", 6 ]
    Assert.Equal(30, taste profile (strongIn "mountains"))
    Assert.Equal(15, taste profile (strongIn "snow-peaks"))
    Assert.Equal(0, taste profile (strongIn "beaches"))

[<Fact>]
let ``a liked parent counts half towards its child`` () =
    let profile = wants [ "snow-peaks", 6 ]
    Assert.Equal(15, taste profile (strongIn "mountains"))

[<Fact>]
let ``a place takes the best of its matches, not both`` () =
    let profile = wants [ "mountains", 6; "snow-peaks", 4 ]
    Assert.Equal(30, taste profile (strongIn "mountains"))

[<Fact>]
let ``a liked place beats a matching one, and two likes beat one`` () =
    let once = {
        wants [ "beaches", 6 ] with
            Collected = [ "mountains" ]
            Likes = Map.ofList [ "mountains", 1 ]
    }

    let twice = {
        once with
            Likes = Map.ofList [ "mountains", 2 ]
    }

    let many = {
        once with
            Likes = Map.ofList [ "mountains", 5 ]
    }

    Assert.True(taste once (strongIn "mountains") > taste once (strongIn "beaches"))
    Assert.True(taste twice (strongIn "mountains") > taste once (strongIn "mountains"))
    Assert.Equal(taste many (strongIn "mountains"), taste twice (strongIn "mountains"))

[<Fact>]
let ``a place whose every card was passed loses ground`` () =
    let profile = {
        wants [ "mountains", 6 ] with
            Declined = [ "mountains" ]
    }

    Assert.True(taste profile (strongIn "mountains") < taste (wants [ "mountains", 6 ]) (strongIn "mountains"))

[<Fact>]
let ``the profile reads likes, declines and the interest tree from the answers`` () =
    let catalog = {
        TripBuilderTests.catalog with
            Interests =
                {
                    Slug = "snow-peaks"
                    Name = "Snow peaks"
                    ParentSlug = Some "mountains"
                    Colour = None
                }
                :: TripBuilderTests.catalog.Interests
    }

    let answers = [
        { Card = "peak-card"; Liked = true }
        { Card = "likes-hills"; Liked = true }
        { Card = "quiet-card"; Liked = false }
        { Card = "unknown"; Liked = true }
    ]

    let profile = Profile.from catalog (TripBuilderTests.request answers 7 None)
    Assert.Equal<string list>([ "peak" ], profile.Collected)
    Assert.Equal(Some 1, profile.Likes.TryFind "peak")
    Assert.Equal<string list>([ "quiet-beach" ], profile.Declined)
    Assert.Equal(Some "mountains", profile.Parents.TryFind "snow-peaks")
    Assert.Equal(("mountains", 6), profile.Interests.Head)

let private doesEverything = {
    strongIn "city" with
        Interests = [
            {
                InterestSlug = "mountains"
                Weight = 5
            }
            { InterestSlug = "beaches"; Weight = 5 }
            { InterestSlug = "forts"; Weight = 5 }
        ]
}

[<Fact>]
let ``a city that does everything counts only for its two best`` () =
    let profile = wants [ "mountains", 6; "beaches", 6; "forts", 6 ]
    Assert.Equal(60, taste profile doesEverything)

[<Fact>]
let ``an interest already covered by a stop counts less`` () =
    let profile = wants [ "mountains", 6 ]
    let peak = strongIn "mountains"
    Assert.Equal(30, Scoring.tasteAfter profile request [] peak)
    Assert.Equal(20, Scoring.tasteAfter profile request [ peak ] peak)
    Assert.Equal(15, Scoring.tasteAfter profile request [ peak; peak ] peak)
    Assert.Equal(30, Scoring.tasteAfter profile request [ strongIn "beaches" ] peak)

[<Fact>]
let ``a liked place in a good month beats one in a hard month`` () =
    let profile = {
        wants [] with
            Collected = [ "sunny"; "rainy" ]
            Likes = Map.ofList [ "sunny", 1; "rainy", 1 ]
    }

    let sunny = {
        strongIn "sunny" with
            Months = TripBuilderTests.months [] []
    }

    let rainy = {
        strongIn "rainy" with
            Months = TripBuilderTests.months [] [ 7 ]
    }

    let july = TripBuilderTests.request [] 7 (Some 7)
    Assert.True(Scoring.total profile july sunny > Scoring.total profile july rainy)
    Assert.True(Scoring.total profile july rainy > Scoring.total (wants []) july (strongIn "filler"))

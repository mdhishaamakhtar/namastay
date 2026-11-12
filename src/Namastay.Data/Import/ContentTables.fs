/// The tables that hold travel content, which publishing copies to production.
/// Accounts, shared itineraries and saved trips are not listed, so publishing can't touch them.
/// Every name here is fixed, because publishing builds its SQL from them.
module Namastay.Data.Import.ContentTables

/// What happens to a row production has and the local database doesn't.
[<RequireQualifiedAccess>]
type Removal =
    /// It stays, because other rows may still point at it.
    | Keep
    /// It's marked retired and kept, because a saved trip may still show it.
    | Retire
    | Delete
    /// It's deleted, but only while the row it belongs to is still published. What belongs
    /// to a retired place stays with it.
    | DeleteWithin of column: string * owner: string

type ContentTable = {
    Name: string
    Key: string list
    /// Every other column, apart from `retired_at`, which `Removal.Retire` looks after.
    Data: string list
    Removal: Removal
}

let private table name key data removal = {
    Name = name
    Key = key
    Data = data
    Removal = removal
}

let private within column owner = Removal.DeleteWithin(column, owner)

/// Parents come before the rows that point at them.
let all = [
    table "regions" [ "slug" ] [ "name"; "colour"; "sort" ] Removal.Keep
    table "clusters" [ "slug" ] [ "region_slug"; "name" ] Removal.Keep
    table "colour_families" [ "slug" ] [ "name"; "used_for"; "deep"; "mid"; "pale"; "text_on_deep" ] Removal.Keep
    table "interests" [ "slug" ] [ "name"; "parent_slug"; "colour" ] Removal.Keep
    table "sources" [ "id" ] [ "url"; "title"; "publisher"; "kind"; "accessed_on" ] Removal.Keep
    table
        "places"
        [ "slug" ]
        [
            "cluster_slug"
            "name"
            "state"
            "family_slug"
            "shade"
            "gate"
            "popularity"
            "days_min"
            "days_max"
            "cost_low"
            "cost_high"
            "summary"
            "is_this_for_you"
            "published"
            "status"
            "last_checked"
            "getting_there"
            "airport"
            "food_intro"
            "hub"
        ]
        Removal.Retire
    table "place_interests" [ "place_slug"; "interest_slug" ] [ "weight" ] (within "place_slug" "places")
    table
        "place_months"
        [ "place_slug"; "month" ]
        [ "rating"; "note"; "status"; "last_checked" ]
        (within "place_slug" "places")
    table "traveller_fit" [ "place_slug"; "traveller" ] [ "fit" ] (within "place_slug" "places")
    table
        "experiences"
        [ "slug" ]
        [ "place_slug"; "name"; "description"; "sort"; "status"; "last_checked" ]
        Removal.Retire
    table
        "day_plans"
        [ "place_slug"; "variant"; "day" ]
        [ "plan"; "status"; "last_checked" ]
        (within "place_slug" "places")
    table
        "stay_areas"
        [ "place_slug"; "name" ]
        [ "cost_level"; "note"; "status"; "last_checked" ]
        (within "place_slug" "places")
    table
        "transport_links"
        [ "slug" ]
        [
            "from_slug"
            "to_slug"
            "mode"
            "minutes_min"
            "minutes_max"
            "distance_km"
            "run_days"
            "valid_from"
            "valid_to"
            "comfort_note"
            "status"
            "last_checked"
        ]
        Removal.Retire
    table
        "trains"
        [ "link_slug"; "number" ]
        [ "departs"; "arrives"; "run_days"; "source_note"; "status"; "last_checked" ]
        (within "link_slug" "transport_links")
    table
        "festivals"
        [ "slug" ]
        [ "name"; "place_slug"; "date_rule"; "visitor_notes"; "status"; "last_checked" ]
        Removal.Delete
    table
        "festival_dates"
        [ "festival_slug"; "year" ]
        [ "starts"; "ends"; "is_moon_dependent" ]
        (within "festival_slug" "festivals")
    table
        "notices"
        [ "slug" ]
        [
            "scope"
            "scope_ref"
            "kind"
            "severity"
            "body"
            "valid_from"
            "valid_to"
            "status"
            "last_checked"
        ]
        Removal.Delete
    table "exclusions" [ "slug" ] [ "name"; "reason" ] Removal.Delete
    table "illustrations" [ "slug" ] [ "file_path"; "alt"; "brief"; "status" ] Removal.Keep
    table
        "photos"
        [ "slug" ]
        [ "place_slug"; "file_path"; "alt"; "credit"; "licence"; "source_url"; "sort" ]
        Removal.Keep
    table
        "cards"
        [ "slug" ]
        [
            "place_slug"
            "title"
            "line"
            "illustration_slug"
            "is_start"
            "photo_slug"
            "landing_sort"
        ]
        Removal.Retire
    table "card_branches" [ "from_card"; "on_answer"; "to_card" ] [ "priority" ] (within "from_card" "cards")
    table "card_signals" [ "card_slug"; "signal" ] [ "weight" ] (within "card_slug" "cards")
    table
        "dishes"
        [ "place_slug"; "name" ]
        [
            "description"
            "vegetarian"
            "status"
            "last_checked"
            "photo_slug"
            "rank"
            "vegan"
        ]
        (within "place_slug" "places")
    table
        "eateries"
        [ "place_slug"; "name" ]
        [ "area"; "go_for"; "cost_level"; "vegetarian_only"; "status"; "last_checked" ]
        (within "place_slug" "places")
    table "fact_sources" [ "table_name"; "row_slug"; "field"; "source_id" ] [] Removal.Delete
]

/// Tables that aren't travel content. Publishing copies none of them.
let notContent = [ "users"; "itineraries"; "saved_trips"; "import_batches"; "schemaversions" ]

/// How a trip's scope is written in an address, so the site and the API read it the same way.
module Namastay.Shared.TripScopes

open Namastay.Shared.Contracts

/// A trip can be planned around this many feelings at once.
let maxFeelings = 3

let private kinds = [
    "region", ScopeKind.Region
    "food", ScopeKind.FoodInRegion
    "feeling", ScopeKind.Feeling
    "dish", ScopeKind.Dish
]

/// How many things a scope of this kind may name. Only feelings can be mixed.
let limit (kind: ScopeKind) =
    if kind = ScopeKind.Feeling then maxFeelings else 1

/// Whether a scope names something, nothing twice, and no more than its kind allows.
let isValid (scope: TripScope) =
    not scope.Slugs.IsEmpty
    && scope.Slugs.Length <= limit scope.Kind
    && (List.distinct scope.Slugs).Length = scope.Slugs.Length

/// Adds a feeling to the ones mixed with a page's own, or takes it out again. A page's
/// own feeling counts towards the most a trip can have, so one fewer can be added.
let mix (slug: string) (mixed: string list) =
    if List.contains slug mixed then
        mixed |> List.filter (fun other -> other <> slug)
    elif mixed.Length < maxFeelings - 1 then
        mixed @ [ slug ]
    else
        mixed

/// Reads the `scope` and `of` values of an address. Several feelings are separated by commas.
let ofQuery (kind: string option) (named: string option) : TripScope option =
    let found =
        kind
        |> Option.bind (fun name -> kinds |> List.tryFind (fun (word, _) -> word = name))
        |> Option.map snd

    match found, named with
    | Some chosen, Some text ->
        let scope = {
            Kind = chosen
            Slugs = text.Split ',' |> Array.toList |> List.filter (fun slug -> slug <> "")
        }

        if isValid scope then Some scope else None
    | _ -> None

/// The `scope` and `of` values that `ofQuery` reads back.
let toQuery (scope: TripScope) = [
    "scope", kinds |> List.find (fun (_, kind) -> kind = scope.Kind) |> fst
    "of", String.concat "," scope.Slugs
]

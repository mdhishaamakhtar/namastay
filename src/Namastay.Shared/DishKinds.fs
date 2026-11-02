module Namastay.Shared.DishKinds

open Namastay.Shared.Contracts

let ofFlags vegetarian vegan =
    if vegan then DishKind.Vegan
    elif vegetarian then DishKind.Vegetarian
    else DishKind.NonVeg

let name kind =
    match kind with
    | DishKind.Vegan -> "Vegan"
    | DishKind.Vegetarian -> "Vegetarian"
    | DishKind.NonVeg -> "Non-veg"

/// Vegan food suits a vegetarian too, but not the other way round.
let suits diet kind =
    match diet, kind with
    | Diet.Everything, _
    | Diet.Vegetarian, (DishKind.Vegetarian | DishKind.Vegan)
    | Diet.Vegan, DishKind.Vegan -> true
    | _ -> false

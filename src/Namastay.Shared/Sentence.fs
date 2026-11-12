/// How names are written into a sentence.
module Namastay.Shared.Sentence

/// "A, B and C", the way a sentence lists things.
let listed (names: string list) =
    match List.rev names with
    | last :: (_ :: _ as earlier) -> String.concat ", " (List.rev earlier) + " and " + last
    | _ -> String.concat "" names

module Namastay.Shared.NoticeOrder

open Namastay.Shared.Contracts

let private rank severity =
    match severity with
    | NoticeSeverity.Danger -> 0
    | NoticeSeverity.Permit -> 1
    | NoticeSeverity.Warning -> 2
    | NoticeSeverity.Info -> 3

let private mustShow severity =
    severity = NoticeSeverity.Danger || severity = NoticeSeverity.Permit

/// What to show first and what can fold away, so pages stay a similar length.
/// Anything that could stop a trip is always shown, however long the list gets.
let split (severity: 'Notice -> NoticeSeverity) (limit: int) (notices: 'Notice list) =
    let ordered = notices |> List.sortBy (severity >> rank)
    let urgent = ordered |> List.filter (severity >> mustShow) |> List.length
    let shown = max urgent limit |> min ordered.Length
    List.splitAt shown ordered

let kindName kind =
    match kind with
    | GuideNoticeKind.Closure -> "Opening"
    | GuideNoticeKind.Safety -> "Safety"
    | GuideNoticeKind.Legal -> "Rules"
    | GuideNoticeKind.Permit -> "Permit"

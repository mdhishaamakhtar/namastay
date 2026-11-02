module Namastay.Client.Pages.Journey.Questions

open Namastay.Client.Pages.Journey.State
open Namastay.Shared.Contracts

type Option' = {
    Name: string
    Note: string
    Choice: Choice
}

type Question = {
    Title: string
    Line: string
    Compact: bool
    Options: Option' list
}

let private option' name note choice = {
    Name = name
    Note = note
    Choice = choice
}

let private monthNames = [
    "Jan"
    "Feb"
    "Mar"
    "Apr"
    "May"
    "Jun"
    "Jul"
    "Aug"
    "Sep"
    "Oct"
    "Nov"
    "Dec"
]

let all = [
    {
        Title = "When can you go?"
        Line = "Seasons change everything in India, so we plan around your month."
        Compact = true
        Options = [
            yield!
                monthNames
                |> List.mapi (fun index name -> option' name "" (Choice.Month(Some(index + 1))))
            option' "Not sure" "" (Choice.Month None)
        ]
    }
    {
        Title = "How long have you got?"
        Line = "Between 5 and 21 days."
        Compact = false
        Options = [
            option' "About a week" "One region, unhurried" (Choice.Days 7)
            option' "Two weeks" "Two regions" (Choice.Days 14)
            option' "Three weeks" "Up to three regions" (Choice.Days 21)
        ]
    }
    {
        Title = "Who's coming?"
        Line = "It changes where we send you."
        Compact = false
        Options = [
            option' "Just me" "" (Choice.Who TravellerType.Solo)
            option' "Two of us" "" (Choice.Who TravellerType.Couple)
            option' "Friends" "" (Choice.Who TravellerType.Friends)
            option' "Family" "" (Choice.Who TravellerType.Family)
        ]
    }
    {
        Title = "What feels right to spend?"
        Line = "We suggest areas to stay, not hotels."
        Compact = false
        Options = [
            option' "Keep it simple" "Budget stays and local transport" (Choice.Spend BudgetLevel.Budget)
            option' "Some comfort" "Mid-range, with a few treats" (Choice.Spend BudgetLevel.Mid)
            option' "Treat ourselves" "Luxury where it exists" (Choice.Spend BudgetLevel.Luxury)
        ]
    }
    {
        Title = "How do you like to travel?"
        Line = "India is big. Flights save days."
        Compact = false
        Options = [
            option' "Whatever is best" "Flights, trains and roads" (Choice.Travel TransportComfort.Anything)
            option' "Keep me on the ground" "Trains and roads only" (Choice.Travel TransportComfort.NoFlights)
        ]
    }
    {
        Title = "How do you eat?"
        Line = "We'll pick out what to try at every stop."
        Compact = false
        Options = [
            option' "I eat everything" "Nothing to plan around" (Choice.Eat Diet.Everything)
            option' "Vegetarian" "No meat, fish or eggs" (Choice.Eat Diet.Vegetarian)
            option' "Vegan" "No dairy either, and that includes ghee" (Choice.Eat Diet.Vegan)
        ]
    }
]

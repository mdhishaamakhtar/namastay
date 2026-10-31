# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Stack

F# throughout, one monorepo: Fable for the frontend, and ASP.NET Core with Postgres for the backend from stage 1. All travel data lives in the database and is served by the backend, so new research never needs a frontend build. Swipe physics may use a small amount of hand-written JavaScript.

## Users

Foreign travellers from anywhere who want to visit India and feel overwhelmed by how diverse it is. They don't know where to go, how to build a trip, or what they'd love. People already in India are not the audience, so anything read by them must work in plain English for a non-Indian reader.

## Product Purpose

A free website that works out what kind of traveller someone is through a swipe experience, then gives them a day-by-day India itinerary built around them. Success is a stranger finishing it on a phone and feeling the trip was made for them. It's a fun thing to put out there, not a business.

## Positioning

Instead of the same Delhi-Agra-Jaipur route for everyone, swipes reveal a mix of interests, depth or breadth, and mainstream or offbeat taste, and later cards branch from earlier swipes. Every trip is built from researched, sourced data, with honest warnings where facts are uncertain.

## Operating Context

Used mostly on a phone, anywhere in the world, during trip dreaming. No sign-up is needed to get an itinerary. A visitor sees about 20-30 cards from a set of 100-150. Stage 2 adds saved trips and manual editing. An AI editing agent is a later decision.

## Capabilities and Constraints

- Inputs: swipes, trip length (5-21 days), dates, comfort with flights and trains, budget, and who's travelling.
- Output: day-by-day plan with travel legs, arrival and departure cities, suggested stay areas, a rough cost level and a route map, shareable by link.
- 60 researched places in 23 clusters, some marked with warnings or permit steps.
- No booking, payments or live AI in stages 1 and 2. Not deployed yet.
- Every place gets a custom illustration, not a licensed photo. Only rough versions exist so far.

## Brand Commitments

The name is Namastay, a pun on namaste + stay, chosen by the owner. A trademark and domain check is still owed before launch. A small cannabis leaf may appear inside mountain scenery with no caption or mention. The product never recommends charas or ganja.

## Evidence on Hand

`reports/India travel knowledge base gap closure.md` holds the research, with status tags and sources. There are no users, testimonials, partners or press, and none may be invented.

## Product Principles

- It must feel like an experience, never an interview.
- Never invent a place, route or fact. Say honestly when something is uncertain.
- Give the itinerary before asking for anything.
- Several interests, not one label: people are a mix.
- Respect the traditions and places shown, and treat all of them fairly.
- The product is its data and its interface, so delight and polish are core features: every interaction, state and asset should feel finished.

## Accessibility & Inclusion

Swiping must have a tap and keyboard alternative. Respect reduced motion. No required sign-in.

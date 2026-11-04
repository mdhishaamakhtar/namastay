# The planner scorecard

`make bench` plays 300 synthetic travellers through the swipe deck and the trip
builder, using the real `data/`, and prints a scorecard. Every line says which way
it should move. There is no single score on purpose: a change is judged by reading
the lines it moved, not by one number going up.

- `make bench` compares the run with `docs/scorecards/baseline.json`.
- `make bench-baseline` keeps the run as the new baseline, once a change is accepted.
- `dotnet run --project src/Namastay.Bench -- data --show 10` prints ten sessions in
  full: who the traveller is, what they liked, and the trip they got.

The travellers are the same every run (`Namastay.Bench/Personas.fs`), so two runs
differ only by the planner. 240 have a taste of one or two interests and like the
cards that match it, with a little noise. 40 flip a coin on every card. 20 like
everything. A fifth of the tasteful ones plan from their first interest as a
feeling, the way the feeling pages do.

## Lines

**Variety** asks whether different people get different trips.

- *first cards, open deck*: distinct opening cards across unscoped sessions.
- *distinct trips %*: share of trips that are a route nobody else got.
- *places reached %*: share of published places that appear in any trip.
- *places that open a trip*: distinct first stops.
- *top place in trips %*: share of trips that contain the most common place.

**Direction** asks whether a trip follows the traveller's taste.

- *top interest in trip %*: a stop suits their first interest strongly.
- *every interest in trip %*: each of their interests has such a stop.
- *days on taste %*: share of days at places that suit any of their interests.
- *scope cards shown %*: in scoped sessions, cards that carry the feeling's signal.
- *passed place in trip %*: a stop whose every card they passed.

**Comfort** asks whether the trip is pleasant to travel.

- *travel hours per trip* and *longest leg hours*, from the shortest times recorded.
- *avoid-month stops per trip*: stops rated avoid in the traveller's month.
- *free days per trip*: days with no plan.
- *city twice %*: a stop repeated, or an airport city that is also a middle stop.
- *swaps per trip*: liked places replaced because of the month.
- *trips not built*: sessions that ended in an error.

A scored trip has the full `Trip` the API would return, so a new line is a
function of `Session` in `Namastay.Bench/Metrics.fs`.

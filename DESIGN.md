# Namastay design

Locked on 31 October 2026 from the Claude Design canvas: https://claude.ai/artifact/MR3MKTiSbNebkkri5yy4tv. Source copies are in `docs/design/mockups/`, and every place's theme is in `docs/design/place-themes.md`.

## Look

A calmer take on Indian matchbox labels. Each card is one framed label with a real photo, a small illustrated mark, a hand-lettered place name and flat confident colour. There are no slogans, no stacks of clashing colours and no decorative noise.

## Type

- **Titles:** Yatra One.
- **Everything else:** Mukta, weights 400, 500 and 600. Body text is 16-18px with a line height of about 1.4.

## Colour

- **Brand:** marigold #F2B52B with #2B2108 text, on paper #FFF8E6.
- **Warnings:** charcoal #1C1B1A with white text and a warning icon. Never used for a place.
- **Places:** 16 fixed families, defined in `docs/design/place-themes.md`. Saffron is reserved for Hindu sites.
- **The journey:** every visitor starts on marigold, on phone and desktop. Each card keeps its own family in its frame. The stage stays marigold until one interest clearly leads, then eases to that family, and moves again if the lead changes.
- **Trips:** the family with the most days sets the base, and each stop keeps its own family.

## Components

- **Label:** white or tinted paper, 10px padding, a 2px inner frame in the accent colour, 4px outer radius, shadow `0 14px 30px rgba(0,0,0,.22)`.
- **Buttons:** 52-56px tall, 2px radius, Mukta 600. Primary is filled, secondary has a 1px outline. Press scales to 0.97-0.98.
- **Stops:** a coloured header that expands on tap to show details and a link to the full guide.
- **Tiles:** two-column quick facts as separate tiles with a 4px paper gap.

## Photos and illustrations

- **Real places:** a licensed photo inside the label, with a small illustrated mark set into the frame like a matchbox emblem. Guides and expanded stops use photos.
- **Themes:** a photo where one fits, otherwise a full illustration.
- **Full illustrations:** ideas, brand moments, the route map, and loading, empty and error states.
- **Photos stay consistent:** a shared crop and light colour treatment, about 100-200 KB each, with credits shown where licences need them.

## Desktop

Desktop has its own layouts, never a stretched phone. Colour is always a full-height panel or stage, not a wide strip over a narrow column. Guides and itineraries use a colour panel beside the content, home splits into two, and the swipe puts the card on a full stage with the taste mix and buttons beside it.

## Motion

- **Easing:** `cubic-bezier(.23,1,.32,1)` for UI movement.
- **Swipe:** the card follows the finger with slight rotation. It's dismissed past 110px, or by a flick over 0.45px/ms. Otherwise it springs back in 320ms. Exit takes 240ms.
- **Expand:** 240ms.
- **Colour change:** background transitions in 360ms.
- **Reduced motion:** movement is removed and fades remain.

**Delight:** polish is a core feature. Frequent actions stay instant and rare moments get the richest motion, like the trip reveal and the route map drawing itself. All motion shares one set of durations and easing.

## Accessibility

Swipes always have button alternatives. Touch targets are at least 44px, text contrast is at least 4.5:1, and controls use real buttons and links with visible focus.

## Landing page

Home is a full landing page. A hand of three labels fans out in the hero, then the wall of labels sits on the dark ink colour with the place search beside it. The word India in the closing heading runs through saffron, white and green, and that is the only tricolour on the page.

## Food by diet

Dishes carry the marks printed on Indian menus, always with the word beside them, so the page teaches what to look for: a green dot for vegetarian, a green leaf for vegan and a brown triangle for non-veg. The Food page has a quiet Everything, Vegetarian and Vegan switch. Every stop in an itinerary has an Eat row beside Days and Stay, picked for how the visitor said they eat.

## Still to design

Tablet checks; all final illustrations.

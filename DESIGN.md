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
- **Trips:** the family with the most days sets the base, and each stop keeps its own family.

## Components

- **Label:** white or tinted paper, 10px padding, a 2px inner frame in the accent colour, 4px outer radius, shadow `0 14px 30px rgba(0,0,0,.22)`.
- **Buttons:** 52-56px tall, 2px radius, Mukta 600. Primary is filled, secondary has a 1px outline. Press scales to 0.97-0.98.
- **Stops:** a coloured header that expands on tap to show details and a link to the full guide.
- **Tiles:** two-column quick facts with a 1px gap.

## Photos and illustrations

- **Real places:** a licensed photo inside the label, with a small illustrated mark set into the frame like a matchbox emblem. Guides and expanded stops use photos.
- **Themes:** a photo where one fits, otherwise a full illustration.
- **Full illustrations:** ideas, brand moments, the route map, and loading, empty and error states.
- **Photos stay consistent:** a shared crop and light colour treatment, about 100-200 KB each, with credits shown where licences need them.

## Motion

- **Easing:** `cubic-bezier(.23,1,.32,1)` for UI movement.
- **Swipe:** the card follows the finger with slight rotation. It's dismissed past 110px, or by a flick over 0.45px/ms. Otherwise it springs back in 320ms. Exit takes 240ms.
- **Expand:** 240ms.
- **Colour change:** background transitions in 360ms.
- **Reduced motion:** movement is removed and fades remain.

**Delight:** polish is a core feature. Frequent actions stay instant and rare moments get the richest motion, like the trip reveal and the route map drawing itself. All motion shares one set of durations and easing.

## Accessibility

Swipes always have button alternatives. Touch targets are at least 44px, text contrast is at least 4.5:1, and controls use real buttons and links with visible focus.

## Still to design

The framed photo card; questions for dates, trip length, budget and who's travelling; desktop layouts; all final illustrations.

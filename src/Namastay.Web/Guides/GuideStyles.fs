module Namastay.Web.Guides.GuideStyles

open Namastay.Shared.Contracts

/// Sets the place's colours in the page itself, so the first paint is already right.
let colours (place: PlaceColours) =
    $":root{{--deep:{place.Deep};--mid:{place.Mid};--pale:{place.Pale};--on-deep:{place.TextOnDeep}}}"

let page =
    """
*{box-sizing:border-box}
body{margin:0;background:#FFF8E6;color:#2B2108;font:400 17px/1.45 Mukta,system-ui,sans-serif}
main{max-width:640px;margin:0 auto;padding:0 16px 48px}
nav{display:flex;justify-content:space-between;padding:12px 16px;background:var(--deep);color:var(--on-deep)}
nav a{color:inherit;min-height:44px;display:flex;align-items:center;text-decoration:none;font-weight:600}
header{background:var(--deep);color:var(--on-deep);padding:24px 16px 32px}
h1,h2{font-family:"Yatra One",Mukta,serif;font-weight:400;line-height:1.15}
h1{font-size:44px;margin:0 0 8px}
h2{font-size:26px;margin:32px 0 8px;color:var(--deep)}
.facts{display:grid;grid-template-columns:1fr 1fr;gap:1px;background:var(--mid);margin:24px 0;border-radius:4px;overflow:hidden}
.facts div{background:var(--pale);padding:12px}
.facts strong{display:block;font-size:20px}
figure{margin:24px 0}
figure img{display:block;width:100%;height:auto;border-radius:4px}
figcaption{font-size:14px;margin-top:4px}
figcaption a,footer a{color:inherit}
details{border-top:1px solid var(--mid);padding:12px 0}
summary{font-weight:600;min-height:44px;display:flex;align-items:center;cursor:pointer}
.months{list-style:none;display:grid;grid-template-columns:repeat(6,1fr);gap:4px;padding:0}
.months li{padding:8px 0;text-align:center;border-radius:2px;background:#fff;font-size:14px}
.months .best{background:var(--deep);color:var(--on-deep)}
.months .avoid,.months .closed{background:#1C1B1A;color:#fff}
.warning{background:#1C1B1A;color:#fff;padding:12px 16px;border-radius:4px;margin:12px 0}
.hedge{font-style:italic}
.plan{display:flex;align-items:center;justify-content:center;min-height:56px;background:var(--deep);color:var(--on-deep);border-radius:2px;font-weight:600;text-decoration:none;margin-top:32px;transition:transform 140ms cubic-bezier(.23,1,.32,1)}
.plan:active{transform:scale(.98)}
a:focus-visible,summary:focus-visible{outline:2px solid currentColor;outline-offset:2px}
footer{font-size:14px;margin-top:32px}
@media (prefers-reduced-motion:reduce){.plan{transition:none}}
"""

module Namastay.Web.Guides.GuideStyles

open Namastay.Shared.Contracts

/// Sets the place's colours in the page itself, so the first paint is already right.
let colours (place: PlaceColours) =
    $":root{{--deep:{place.Deep};--mid:{place.Mid};--pale:{place.Pale};--on-deep:{place.TextOnDeep}}}"

/// Phone first. On a wide screen the place's colour becomes a full-height side panel.
let page =
    """
@font-face{font-family:"Yatra One";font-weight:400;font-display:swap;src:url(/fonts/yatra-one-latin-400-normal.woff2) format("woff2")}
@font-face{font-family:Mukta;font-weight:400;font-display:swap;src:url(/fonts/mukta-latin-400-normal.woff2) format("woff2")}
@font-face{font-family:Mukta;font-weight:600;font-display:swap;src:url(/fonts/mukta-latin-600-normal.woff2) format("woff2")}
*{box-sizing:border-box}
body{margin:0;background:#FBFAF2;color:#2B2108;font:400 17px/1.5 Mukta,system-ui,sans-serif;-webkit-tap-highlight-color:transparent}
h1,h2{font-family:"Yatra One",Mukta,serif;font-weight:400;margin:0}
h1{font-size:46px;line-height:1.05}
h2{font-size:27px;line-height:1.15;color:var(--deep)}
p{margin:0}
a{color:inherit}
.panel{display:flex;flex-direction:column;gap:16px;padding:16px 24px 24px;background:var(--deep);color:var(--on-deep)}
.top{display:flex;justify-content:space-between;align-items:center;min-height:44px}
.top a{display:flex;align-items:center;min-height:44px;text-decoration:none}
.brand{font-family:"Yatra One",Mukta,serif;font-size:24px}
.back{font-weight:600}
.back svg{margin-left:-6px}
.label{position:relative;padding:8px;background:#FBFAF2;border-radius:4px;box-shadow:0 14px 30px rgba(0,0,0,.22);color:#2B2108}
.frame{padding:8px;border:2px solid var(--deep)}
.hero img{display:block;width:100%;aspect-ratio:4/3;object-fit:cover}
.credit{margin-top:8px;font-size:13px;line-height:1.3}
.mark{position:absolute;right:16px;bottom:40px;display:flex;align-items:center;justify-content:center;width:64px;height:64px;padding:12px;background:#FBFAF2;border:2px solid var(--deep);border-radius:50%}
.mark img{display:block;width:100%;height:100%}
.summary{font-size:18px;line-height:1.4}
.plan{display:flex;align-items:center;justify-content:center;min-height:56px;border-radius:2px;font-weight:600;text-decoration:none;transition:transform 140ms cubic-bezier(.23,1,.32,1)}
.plan:active{transform:scale(.97)}
.panel .plan{display:none;background:#FBFAF2;color:#2B2108;margin-top:auto}
.content{display:flex;flex-direction:column;gap:32px;padding:24px 24px 40px}
.content .plan{background:var(--deep);color:var(--on-deep)}
section{display:flex;flex-direction:column;gap:8px;min-width:0}
.pair{display:flex;flex-direction:column;gap:32px}
.facts{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:4px}
.facts div{padding:12px;background:var(--pale);border-radius:2px;color:#2B2108}
.facts strong{display:block;font-family:"Yatra One",Mukta,serif;font-size:21px;font-weight:400;line-height:1.2;color:var(--deep)}
.facts span{font-size:15px}
.months{display:grid;grid-template-columns:repeat(6,minmax(0,1fr));gap:4px;margin:0;padding:0;list-style:none}
.months li{display:flex;align-items:center;justify-content:center;height:52px;background:var(--pale);color:#2B2108;font-size:15px}
.months .best{background:var(--deep);color:var(--on-deep);font-family:"Yatra One",Mukta,serif;font-size:19px}
.months .avoid,.months .closed{background:#1C1B1A;color:#FFFFFF;font-size:13px}
.months .unknown{background:none;border:1px dashed color-mix(in srgb,var(--deep) 45%,transparent);color:color-mix(in srgb,#2B2108 70%,transparent);font-size:13px}
.note{font-size:15px;line-height:1.4}
.spots{display:grid;gap:8px}
.spot{display:flex;flex-direction:column;gap:6px;padding:14px 16px;background:#FFFFFF;border:1px solid var(--deep)}
.spot h3{margin:0;font-family:"Yatra One",Mukta,serif;font-size:22px;font-weight:400;line-height:1.2}
details{background:#FFFFFF;border:1px solid var(--deep)}
summary{display:flex;justify-content:space-between;align-items:center;min-height:54px;padding:8px 12px;font-family:"Yatra One",Mukta,serif;font-size:22px;line-height:1.2;cursor:pointer;list-style:none}
summary::-webkit-details-marker{display:none}
summary svg{flex:none;transition:transform 220ms cubic-bezier(.23,1,.32,1)}
details[open] summary svg{transform:rotate(180deg)}
details p{padding:0 12px 12px}
.gallery{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:8px}
.gallery figure{display:flex;flex-direction:column;gap:6px;margin:0;min-width:0;padding:6px;background:#FFFFFF;border:1px solid var(--deep)}
.gallery img{display:block;width:100%;aspect-ratio:4/3;object-fit:cover}
.gallery figcaption{font-size:12px;line-height:1.3;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.stays p+p{margin-top:8px}
.lightbox{display:none}
.lightbox:target{display:flex;align-items:center;justify-content:center;position:fixed;inset:0;z-index:10;padding:16px;background:rgb(16 14 10 / 94%);color:#FFFFFF;font-size:15px}
.lightbox figure{position:relative;z-index:1;margin:0;max-width:100%;pointer-events:none}
.lightbox figcaption{padding-top:8px;text-align:center;pointer-events:auto}
.lightbox img{display:block;max-width:100%;max-height:calc(100dvh - 88px);margin:0 auto;object-fit:contain}
.lightbox-close{position:absolute;inset:0}
.lightbox-step{position:absolute;top:0;bottom:0;z-index:2;width:22%}
.lightbox-step::after{position:absolute;top:50%;font-size:40px;line-height:1;transform:translateY(-50%)}
.lightbox-previous{left:0}
.lightbox-previous::after{content:"\2039";left:16px}
.lightbox-next{right:0}
.lightbox-next::after{content:"\203A";right:16px}
.gallery a img,.hero a img{cursor:zoom-in}
.notices{display:flex;flex-direction:column}
.notices h2{margin-bottom:8px}
.notice{display:grid;grid-template-columns:72px minmax(0,1fr);gap:12px;padding:12px 0;border-top:1px solid color-mix(in srgb,var(--deep) 25%,transparent)}
.notice strong{font-size:15px;font-weight:600;color:var(--deep)}
.more-notices{background:none;border:0;border-top:1px solid color-mix(in srgb,var(--deep) 25%,transparent)}
.more-notices summary{min-height:48px;padding:8px 0;font:600 16px/1.4 Mukta,system-ui,sans-serif}
.more-notices p{padding:0}
.hedge{font-style:italic}
footer{font-size:14px}
a:focus-visible,summary:focus-visible{outline:2px solid currentColor;outline-offset:2px}
@media (min-width:640px){.facts{grid-template-columns:repeat(4,minmax(0,1fr))}.gallery{grid-template-columns:repeat(3,minmax(0,1fr))}.spots{grid-template-columns:repeat(2,minmax(0,1fr))}.spot:last-child:nth-child(odd){grid-column:1/-1}.pair{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:32px}}
@media (min-width:1024px){
h1{font-size:60px}
h2{font-size:30px}
.guide{display:grid;grid-template-columns:minmax(380px,520px) minmax(0,1fr);min-height:100vh}
.panel{position:sticky;top:0;align-self:start;height:100vh;gap:24px;padding:32px 48px 40px;overflow-y:auto}
.panel .plan{display:flex}
.content{gap:32px;padding:40px 56px 48px}
.content>*{width:100%;max-width:900px}
.content>.plan{display:none}
}
@media (prefers-reduced-motion:reduce){.plan,summary svg{transition:none}}
"""

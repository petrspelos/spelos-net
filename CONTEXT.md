# Spelos .NET

Spelos .NET is Peter's personal website: a public identity page and a home for small interactive tools.

## Language

**Home page**:
The public business-card page at `/`, presenting Peter's identity and selected external destinations in a code-inspired design.
_Avoid_: Link tree, landing page

**External link**:
A named outbound destination presented on the home page.
_Avoid_: Social link

**Internal link**:
A named destination within Spelos .NET, presented alongside external links while remaining distinguishable as site navigation.
_Avoid_: Local link

**Tool**:
A client-side interactive utility written in C# and published as part of the site.
_Avoid_: Widget, app

**Tools page**:
The discoverable catalog for tools at `/tools`, including tools added in the future.
_Avoid_: Widgets page, utilities page

**Discord timestamp**:
A Discord markup token containing a Unix instant and display format, rendered in each viewer's local time zone.
_Avoid_: Discord time code, epoch code

**Time-zone note**:
Optional quoted Markdown copied beneath a Discord timestamp to explain that Discord displays it in each viewer's local time zone.
_Avoid_: Disclaimer, warning

**Browser preview**:
A localized approximation of how Discord will render a Discord timestamp; the generated Discord timestamp remains authoritative.
_Avoid_: Discord preview, exact preview

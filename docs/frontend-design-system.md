# Frontend design system

## Direction

UniConnect uses a warm campus tone within a clear SaaS workspace: forest-green actions, soft sage surfaces, warm paper backgrounds, quiet borders and restrained coral accents. The UI uses card-based groupings, generous space and concise labels. Decorative motifs reference connection and learning without copying another social platform.

## Tokens

Defined at `:root` in `clients/react-web/src/index.css`:

- Ink `#20342e`, muted `#75817a`, paper `#fff`, canvas `#f7f8f5`.
- Primary green `#315f4f`, hover green `#244d40`, mint `#e7f0e9`, coral `#c87961`.
- Border `#e5e9e3`, radius 14px; smaller controls use 8–10px.
- System sans-serif typography with Georgia for selected editorial headings.
- Spacing follows 4/8px rhythm, with 12–34px content padding.
- Focus uses a visible sage outline. Reduced-motion disables animated transitions.

## Shared patterns

The client uses reusable `Heading`, `Loading`, `Notice`, `Empty`, and `Avatar` components, plus consistent buttons, cards, badges, form controls, tabs, dialogs and status labels. These are shadcn/ui-style lightweight primitives rather than a large component framework. Lucide icons are paired with text for important actions.

## Layout

Desktop uses a 225px navigation rail, flexible main column and 255px contextual rail. At 1200px the contextual rail collapses; at 760px the sidebar becomes a drawer, content becomes one column and persistent bottom navigation appears. Mobile controls preserve touch-friendly sizing where practical.

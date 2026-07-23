typan-station-goal-objective-issuer = Typan Station
typan-war-objective-issuer = Station War

typan-war-sender = Station War Command
typan-war-sender-nt = Station War Command (NanoTrasen)
typan-war-sender-typan = Station War Command (Syndicate)

typan-war-declaration = A war has been declared between the NanoTrasen station and Typan. All personnel must arm themselves immediately and follow combat readiness protocols.

typan-war-objective-typan = Minimize Typan crew losses and pursue the station goal. Code Omega — full combat readiness.
typan-war-objective-nt = Defend the NanoTrasen station and minimize crew losses. Code Gamma — martial law.

typan-war-hud-nt = NT
typan-war-hud-typan = Syndicate
typan-war-hud-pending = Preparation
typan-war-hud-active = War
typan-war-hud-ended = End
typan-war-hud-winner-nt = NT Victory
typan-war-hud-winner-typan = Syndicate Victory

typan-war-end-warning = One minute remains until the station war ends.
typan-war-event-supply = [color=#C8C4D8]Station War:[/color] supply caches unlocked on both stations — check war armories.
typan-war-event-intel = [color=#A8C8FF]NT score:[/color] {$nt} pts. [color=#FFB0B0]Syndicate:[/color] {$typan} pts.

typan-war-capture-zones-active-header = Attention NanoTrasen and Syndicate forces! Strategically important capture zones have been marked:
typan-war-capture-zones-active-line = Zone {$label} — {$location}
typan-war-capture-zone-named = Zone {$label} ({$location})
typan-war-capture-zone-nt = NT capture zone
typan-war-capture-zone-typan = Syndicate capture zone
typan-war-capture-zone-trade = trade outpost capture zone
typan-war-capture-location-station = {$station} — {$area}
typan-war-capture-location-trade-nt = NanoTrasen trade outpost
typan-war-capture-location-trade-typan = Syndicate trade outpost
typan-war-hud-zone-line = Zone {$label}: {$location}
typan-war-capture-nt = NanoTrasen captured {$zone}.
typan-war-capture-typan = Syndicate captured {$zone}.
typan-war-capture-neutral = {$zone} is now neutral.
typan-war-capture-loot-nt = A NanoTrasen surplus crate has been delivered to zone {$label} — {$location}.
typan-war-capture-loot-typan = A Syndicate surplus crate has been delivered to zone {$label} — {$location}.
typan-war-capture-trade-zone-swapped = Zone {$label} has been relocated to {$location} to balance the fight.
typan-war-hud-zone-inactive = inactive
typan-war-hud-zone-owner-neutral = neutral
typan-war-hud-zone-owner-nanotrasen = NT
typan-war-hud-zone-owner-typan = Syndicate

typan-war-prep-announce = A war has been declared between the NanoTrasen and Typan stations. Bluespace jumps are blocked for the duration of the war. Follow combat readiness protocols — hostilities begin in five minutes.

typan-war-manifest =
    {"[font size=18][color=#C8C4D8][bold]STATION WAR — OPERATIONAL BRIEF[/bold][/color][/font]"}
    {"[color=#888898]────────────────────────────────[/color]"}
    {"[color=#C8C4D8][color=#A8C8FF][bold]NanoTrasen[/bold][/color] and [color=#FFB0B0][bold]Syndicate (Typan)[/bold][/color] forces are engaged in open combat on a shared battlefield.[/color]"}
    {" "}
    {"[color=#C8C4D8][bold]Primary objectives[/bold][/color]"}
    {"[color=#A8C8FF]▸ NanoTrasen:[/color] hold capture zones and reach [bold]100 points[/bold]."}
    {"[color=#FFB0B0]▸ Syndicate:[/color] hold capture zones and reach [bold]100 points[/bold]."}
    {"[color=#C8C4D8]▸[/color] If time runs out first, the faction with more points wins."}
    {"[color=#C8C4D8]▸[/color] Active combat lasts [bold]45 minutes[/bold]."}
    {" "}
    {"[color=#C8C4D8][bold]Capture zones[/bold][/color] [color=#888898](A / B / C)[/color]"}
    {"[color=#C8C4D8]▸[/color] Three 3×3 zones appear when combat begins — NT station, Typan station, and a trade outpost."}
    {"[color=#C8C4D8]▸[/color] Hold a zone to earn capture points and receive [bold]faction supply crates[/bold] on a timer."}
    {"[color=#C8C4D8]▸[/color] NT-held zones drop [color=#A8C8FF]NT surplus[/color]; Syndicate-held zones drop [color=#FFB0B0]Syndicate surplus[/color]."}
    {" "}
    {"[color=#C8C4D8][bold]Combat rules[/bold][/color]"}
    {"[color=#C8C4D8]▸[/color] Ranged weapons are configured [bold]not to harm allies[/bold] — toggle [bold]Ally Protection[/bold] if needed."}
    {"[color=#C8C4D8]▸[/color] Reinforcements respawn at captured zones or your duty spawn."}

typan-war-manifest-score = {"[color=#C8C4D8][bold]Final score:[/bold][/color] NanoTrasen [color=#A8C8FF]{$nt}[/color] / Syndicate [color=#FFB0B0]{$typan}[/color] points (win at {$win})"}

typan-war-ff-enabled = Ally protection enabled — you will not hit allies.
typan-war-ff-disabled = Ally protection disabled.

typan-war-respawn-title = Reinforcement Request
typan-war-respawn-timer = Reinforcements available in {$seconds} s
typan-war-respawn-ready = Select a deployment point
typan-war-respawn-zone = Zone {$label} — {$location}
typan-war-respawn-zone-desc = Deploy at a captured zone held by your faction.
typan-war-respawn-base = Duty spawn
typan-war-respawn-base-desc = Return to your original spawn location.
typan-war-respawn-no-options = No deployment points available.
typan-war-respawn-no-profile = Could not load your character profile.
typan-war-respawn-failed = Deployment failed — try another point.

typan-war-minimap-title = War map
typan-war-minimap-legend = NT {$nt} / Syndicate {$typan} — win at {$win}
typan-war-minimap-forces-nt = NanoTrasen: {$count} fighters
typan-war-minimap-forces-typan = Syndicate: {$count} fighters
typan-war-minimap-map-legend = Silhouettes = stations & shuttles (named) · triangles = allies · letters = capture zones
typan-war-minimap-controls = Scroll — zoom · drag — pan
typan-war-minimap-loading = Loading map…
typan-war-layout-failed = Station war aborted: failed to merge stations onto the battlefield.
typan-war-ghost-thunderdome-blocked = Thunderdome is disabled during station war.

typan-war-ftl-blocked = Bluespace jumps are blocked during station war.

typan-war-drop-shuttle-docked-nt = A NanoTrasen reinforcement shuttle has docked {$direction} of the station at {$location}.
typan-war-drop-shuttle-docked-typan = A Syndicate reinforcement shuttle has docked {$direction} of the station at {$location}.
typan-war-drop-shuttle-lost-nt = The NanoTrasen reinforcement shuttle has been disabled — a replacement is being prepared.
typan-war-drop-shuttle-lost-typan = The Syndicate reinforcement shuttle has been disabled — a replacement is being prepared.

typan-war-balance-lobby-wait = Station war: NT is overpopulated. You remain in the lobby — late join on the Typan station instead.
typan-war-balance-lobby-wait-typan = Station war: Typan is overpopulated. You remain in the lobby — late join on the NT station instead.
typan-war-balance-latejoin-nt-full = NT station has too many players. Only Typan station entry is available right now.
typan-war-balance-latejoin-typan-full = Typan station has too many players. Only NT station entry is available right now.
typan-war-balance-latejoin-both-full = Both factions are full — wait until the balance recovers.
typan-war-balance-job-denied = This faction is full. Choose the side with fewer players.

typan-war-start-cancelled = Station war cancelled — insufficient combat-ready personnel on one side.
typan-war-start-cancelled-nt = Station war cancelled — NT has only {$nt} combat-ready personnel ({$ntMin} required).
typan-war-start-cancelled-typan = Station war cancelled — Typan has only {$typan} combat-ready personnel ({$typanMin} required).

typan-war-end-announce-nt = The station war has ended. [color=#A8C8FF]NanoTrasen[/color] wins with {$nt} capture points (Syndicate {$typan}).

typan-war-end-announce-typan = The station war has ended. [color=#FFB0B0]Syndicate[/color] wins with {$typan} capture points (NanoTrasen {$nt}).

typan-war-end-announce-nt-elimination = The station war has ended. [color=#A8C8FF]NanoTrasen[/color] wins with {$nt} capture points.

typan-war-end-announce-typan-elimination = The station war has ended. [color=#FFB0B0]Syndicate[/color] wins with {$typan} capture points.

typan-war-end-announce-stalemate = The station war has ended in a stalemate — {$nt} points for NanoTrasen and {$typan} for Syndicate.

typan-war-round-end-header = [color=#C8C4D8][bold]Station War[/bold][/color]
typan-war-round-end-initial = Deployed: NanoTrasen {$nt}, Typan {$typan}
typan-war-round-end-final = Capture points: NanoTrasen {$ntPoints}, Typan {$typanPoints} (win at {$win})
typan-war-round-end-losses = Survivors: NanoTrasen {$nt}, Typan {$typan}
typan-war-round-end-nt-goal = NT station goal: {$goal}
typan-war-round-end-typan-goal = Typan station goal: {$goal}
typan-war-round-end-winner-nt = Winner: NanoTrasen
typan-war-round-end-winner-typan = Winner: Typan
typan-war-round-end-stalemate = Result: stalemate

game-presets-typan-station-war = Station War
game-presets-typan-station-war-description = Conflict between NanoTrasen and Typan. Hold capture zones and reach 100 points before the enemy.

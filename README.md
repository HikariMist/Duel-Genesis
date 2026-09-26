# Duel: Genesis

Duel: Genesis is a 3D online card-dueling game set in Genesis City. Players explore a persistent world, visit card shops, open boosters, build collections and decks, sit at physical duel tables, and battle inside miniature tabletop arenas.

## Current prototype milestone — v0.4

The repository now includes a connected playable loop:

- Third-person movement and camera
- Interaction system
- Persistent Genesis Credits wallet
- Card shop terminal and five-card booster opening
- Original prototype card database with rarities
- Persistent player collection
- 40–60 card Deck Builder with 3-copy limit
- Legal-deck validation
- Duel Table interaction
- 8,000 LP duel prototype versus a CPU opponent
- Opening hands, draws, Main/Battle/End flow
- Normal/Tribute Summoning
- Special Summon support for prototype cards that need it
- Face-up Attack, face-up Defense and face-down Defense positions
- Monster setting and Flip Summoning
- Position-change restrictions
- ATK-vs-ATK and ATK-vs-DEF battle calculations
- Direct attacks, battle damage and deck-out
- Five Monster Zones and five Spell/Trap Zones
- Graveyard and Banished tracking
- Working prototype Spell/Trap effects
- Multiple working prototype monster effects
- CPU summoning, setting, attacking and backrow logic
- Duel rewards
- Neon cyan/purple/magenta visual pass for the arena and UI
- Automatic editor smoke tests and runtime diagnostics

## Open the prototype

After pulling the latest GitHub changes:

1. Open the project in Unity.
2. Let Unity import and compile.
3. If you already created `Assets/Scenes/GenesisPrototype.unity`, open it and press Play.
4. If the scene does not exist, use `Duel Genesis > Build First Playable Prototype` once.

Visual polish and runtime systems are applied automatically when Play Mode starts.

## Controls

- `WASD` — move
- Mouse — rotate camera
- `Space` — jump
- `E` — interact / start duel
- `Q` — leave duel table after the duel closes
- `C` — collection
- `B` — Deck Builder
- `Esc` — close menus / release cursor when appropriate
- `F9` — Editor-only developer shortcut: +50,000 GC

## Automated checks

Unity automatically runs lightweight editor smoke tests after scripts reload. They validate the card database, required prototype cards, guaranteed pack rarity behavior, and deck constants.

Runtime diagnostics also check that the card database and major gameplay systems are present. The HUD displays `SYSTEM CHECK: PASS` when those checks succeed.

A manual smoke-test command is also available at:

`Duel Genesis > Run Automated Smoke Tests`

## Prototype rules currently implemented

The duel engine is still a prototype, but its architecture now supports the core board state rather than being only a mockup. Several test cards have bespoke working effects, including search/draw, ATK modification, Trap responses, LP recovery, self-Special Summoning, battle triggers, backrow removal and Tribute-Summon effects.

Advanced rule coverage still to be expanded includes full chain timing, complete targeting choices, Ritual/Synchro/Xyz/Pendulum/Link/Fusion systems, Extra Deck rules, comprehensive once-per-turn timing, all card-effect edge cases and multiplayer synchronization.

## Development direction

Next major work should focus on:

1. A real 3D Genesis City plaza and card-shop interior.
2. Physical cards and zone placement on the tabletop arena.
3. 3D monster prefab spawning and animation hooks.
4. A more complete effect/chain engine.
5. Extra Deck and advanced summon systems.
6. Duelist XP, ranks and cosmetic progression.
7. Account persistence and online multiplayer.
8. Expanded city districts, tournaments, trading and The Underground.

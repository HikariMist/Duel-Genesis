# Duel: Genesis

Duel: Genesis is a 3D card-dueling game set in Genesis City. The current goal is a complete single-player vertical slice using original prototype cards and placeholder geometry. Production card files, artwork and 3D monster models can be connected later without rebuilding the core game loop.

## Current playable loop

The project currently supports:

- Third-person movement and camera
- Genesis City prototype district with neon runtime visual treatment
- Interactable Genesis Card Shop
- Persistent Genesis Credits (GC)
- Five-card booster opening with rarity rolls
- Persistent card collection and duplicate counts
- 40–60 card Main Deck builder with three-copy validation
- First-run 40-card starter loadout for new saves
- Duelist level, XP, title, pack count and win/loss progression
- Physical duel-table interaction
- 8,000 LP single-player CPU duels
- Opening hands, drawing and deck-out
- Main Phase and Battle Phase flow
- Normal Summons and Tribute Summons
- Attack Position, Defense Position and face-down monster setting
- Flip Summoning and once-per-turn position changes
- Five Monster Zones and five Spell/Trap Zones
- Graveyard and Banished tracking
- Battle against ATK or DEF with direct attacks and battle damage
- Multiple working prototype monster effects
- Working prototype Spells and reactive Traps
- Basic CPU deck, summoning, backrow, effects and battle decisions
- Win/loss GC rewards
- Automated Editor smoke tests and runtime diagnostics
- Neon HUD, card rarity colors, duel UI, glowing tabletop zones and plaza landmarks

## Quick start

After pulling the latest GitHub changes:

1. Open the project in Unity 6.6.
2. Wait for script compilation and import to finish.
3. Open `Assets/Scenes/GenesisPrototype.unity`.
4. Press Play.
5. A brand-new save receives a legal 40-card starter deck automatically.
6. Walk to the Duel Table and press `E` to duel, or visit the Card Shop to open more packs.

The existing scene does not need to be rebuilt for runtime-system updates. Use `Duel Genesis > Build First Playable Prototype` only if you intentionally want to regenerate the blockout scene.

## Controls

- `WASD` — move
- Mouse — camera
- `Space` — jump
- `E` — interact / start duel
- `Q` — leave duel-table seat after returning from a duel
- `C` — collection
- `B` — deck builder
- `F1` — help / current objective
- `Esc` — release mouse cursor
- Left click in Game view — capture cursor again
- `F9` — Editor-only +50,000 GC testing shortcut

## Validation tools

Unity automatically runs lightweight Editor validation after scripts reload. Manual tools are also available under the `Duel Genesis` top menu:

- `Run Automated Smoke Tests`
- `Run Vertical Slice Validation`
- `DEV > Reset All Local Prototype Progress`
- `DEV > Add 50,000 GC In Play Mode`
- `DEV > Re-run Runtime Diagnostics In Play Mode`

## What is intentionally deferred

The vertical slice is being built so external production content can drop in later. These are not required to prove the core game loop:

- Final licensed/owned card database and artwork
- Production 3D monster models, animation, VFX and audio
- Exact full rules coverage for every future card interaction
- Extra Deck summon families that need real card data
- Online accounts, multiplayer networking, matchmaking and server persistence
- Trading and player marketplace
- Expanded Genesis City MMO zones

The next production pass should focus on data-driven card importing, physical 3D card presentation on the tabletop, targeting/chain UX, and replacing prototype visuals with final assets.

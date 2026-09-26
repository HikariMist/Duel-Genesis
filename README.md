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
- Pause/resume/quit menu for standalone builds
- Automated Editor smoke tests and runtime diagnostics
- Neon HUD, card rarity colors, duel UI, glowing tabletop zones, animated arena hologram FX and plaza landmarks
- One-click Windows x64 playable build command

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
- `Esc` — pause while exploring; modal screens handle Escape themselves
- Left click in Game view — capture cursor again
- `F9` — Editor-only +50,000 GC testing shortcut

## Make a Windows playable build

Use either:

- `Duel Genesis > Build > Windows Playable`
- `Duel Genesis > Build > Windows Development Build`

The executable is generated at:

`Builds/DuelGenesis/DuelGenesis.exe`

The build tool automatically adds `GenesisPrototype.unity` to Build Settings and applies prototype product settings.

## Production card-data pipeline

The built-in 24-card Genesis set is only placeholder content. The game now supports an external JSON catalog so a larger authorized card dataset can be connected later.

1. Copy `Assets/StreamingAssets/duel_genesis_cards.example.json` to:
   `Assets/StreamingAssets/duel_genesis_cards.json`
2. Replace the example records with the production card records.
3. On startup, matching IDs replace prototype definitions and new IDs are appended to the database.

Each JSON record supports ID, name, card kind, rarity, attribute, type line, level, ATK, DEF, effect text and an optional model resource path.

## Production 3D model pipeline

The default model convention is:

`Assets/Resources/CardModels/<CARD_ID>.prefab`

For example:

`Assets/Resources/CardModels/EXAMPLE001.prefab`

`CardModelRegistry.LoadPrefab(cardId)` will resolve that prefab automatically. The JSON catalog can also specify a custom `modelResource` path.

Only import card art, models, audio or other content you have the rights or permission to use.

## Validation tools

Unity automatically runs lightweight Editor validation after scripts reload. Manual tools are also available under the `Duel Genesis` top menu:

- `Run Automated Smoke Tests`
- `Run Vertical Slice Validation`
- `DEV > Reset All Local Prototype Progress`
- `DEV > Add 50,000 GC In Play Mode`
- `DEV > Re-run Runtime Diagnostics In Play Mode`

## What is intentionally deferred

The vertical slice is being built so external production content can drop in later. These are not required to prove the core game loop:

- Final authorized card database and artwork
- Production 3D monster models, animation, VFX and audio
- Exact full rules coverage for every future card interaction
- Extra Deck summon families that need real card data
- Online accounts, multiplayer networking, matchmaking and server persistence
- Trading and player marketplace
- Expanded Genesis City MMO zones

The next production pass should focus on physical 3D card presentation on the tabletop, targeting/chain UX, generic card-effect interpretation, and replacing prototype visuals with final assets.

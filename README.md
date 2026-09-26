# Duel: Genesis

Duel: Genesis is a 3D card-dueling game set in Genesis City. The current goal is a complete single-player vertical slice with a production-asset pipeline that can connect the supplied DMO card faces, monster prefabs and animations without rebuilding the core game loop.

## Current playable loop

The project currently supports:

- Runtime title screen with Enter Genesis / Controls / Quit flow
- Third-person movement and camera
- Genesis City prototype district with neon runtime visual treatment
- Interactable Genesis Card Shop
- Persistent Genesis Credits (GC)
- Five-card booster opening with rarity rolls
- Persistent card collection and duplicate counts
- 40–60 card Main Deck builder with three-copy validation
- First-run 40-card starter loadout for new saves
- Duelist level, XP, title, pack count and win/loss progression
- Dedicated Duelist Profile panel with XP progress, career stats, collection and economy summary
- Physical duel-table interaction
- 8,000 LP single-player CPU duels
- Opening hands, drawing and deck-out
- Main Phase and Battle Phase flow
- Normal Summons and Tribute Summons
- Attack Position, Defense Position and face-down monster setting
- Flip Summoning and once-per-turn position changes
- Manual battle targeting
- Manual targeting for supported targeted effects such as Quick Charge and Genesis Archmage
- Five Monster Zones and five Spell/Trap Zones
- Graveyard and Banished tracking
- Physical tabletop monster, backrow, Deck, Graveyard and Banished presentation
- Supplied DMO card-face PNG loading with exact-name matching
- Correct frame metadata for Normal, Effect, Fusion, Ritual, Synchro, Xyz, Spell, Trap and Token cards
- Supplied DMO monster prefab and animation lookup by normalized card name
- Battle against ATK or DEF with direct attacks and battle damage
- Multiple working prototype monster effects
- Working prototype Spells and reactive Traps
- Basic CPU deck, summoning, backrow, effects and battle decisions
- Win/loss GC rewards
- Duel phase banners and LP-change presentation feedback
- Pause/resume/quit menu for standalone builds
- Automated Editor smoke tests and runtime diagnostics
- Neon HUD, card rarity colors, duel UI, glowing tabletop zones, animated arena hologram FX and plaza landmarks
- One-click Windows x64 build and Build-and-Run commands

## Quick start

After pulling the latest GitHub changes:

1. Open the project in Unity 6.6.
2. Allow Unity to resolve/import the local DMO character package. The first import is large and can take a while.
3. Wait for script compilation and import to finish.
4. Open `Assets/Scenes/GenesisPrototype.unity`.
5. Press Play.
6. Choose `ENTER GENESIS` on the runtime title screen.
7. A brand-new save receives a legal 40-card starter deck automatically.
8. Walk to the Duel Table and press `E` to duel, or visit the Card Shop to open more packs.

The existing scene does not need to be rebuilt for runtime-system updates. Use `Duel Genesis > Build First Playable Prototype` only if you intentionally want to regenerate the blockout scene.

## Controls

- `Enter` / `Space` — Enter Genesis from the title screen
- `WASD` — move
- Mouse — camera
- `Space` — jump while exploring
- `E` — interact / start duel
- `Q` — leave duel-table seat after returning from a duel
- `C` — collection
- `B` — deck builder
- `P` — Duelist Profile
- `F1` — help / current objective
- `Esc` — pause while exploring; modal screens handle their own input
- Left click in Game view — capture cursor again
- `F9` — Editor-only +50,000 GC testing shortcut

## DMO production asset integration

The repository-level `Cards` and `Characters` folders are now connected to the main game without duplicating the source libraries.

### First-time asset check

Use:

`Duel Genesis > Production Assets > Scan DMO Libraries`

This counts card faces, character prefabs and animation clips, reports card/model name matches, and verifies the supported card-frame source images.

### Build the real card-data catalog

Use:

`Duel Genesis > Production Assets > Build Real Card Catalog from DMO Art`

The editor downloads current public card metadata once, matches it only against PNG names that exist in `Cards/DMO_card_art/cards`, and writes:

`Assets/StreamingAssets/duel_genesis_cards.json`

It also writes an unmatched-name report beside the catalog so old/renamed cards can be handled explicitly later.

**Card-frame rule:** a complete supplied DMO card PNG is authoritative and is always displayed first. Duel: Genesis does not rebuild or recolor that image, so a Fusion card cannot accidentally be shown with an Effect frame, a Trap cannot receive a Spell frame, etc. The `frameKind` field is used only as metadata and as a fallback when a complete face image is unavailable.

Supported fallback `frameKind` values are:

- `NormalMonster`
- `EffectMonster`
- `FusionMonster`
- `RitualMonster`
- `SynchroMonster`
- `XyzMonster`
- `Spell`
- `Trap`
- `Token`
- `Auto`

### Monster models and animations

`Characters` is mounted as the local Unity package `com.hikarimist.dmo-characters`.

The game reads `dmo_manifest.txt`, normalizes the card and character names, then resolves matching prefabs from the supplied `Resources/Models` library. For example, a card name with spaces/apostrophes can still match a prefab name that uses underscores. Matching animation clips are loaded from that character's own `Resources/Animations/<Character>` folder so clips stay on the rig they were authored for.

An explicit `modelResource` in the JSON catalog still takes priority when a card needs a manual override.

## Make a Windows playable build

Use one of:

- `Duel Genesis > Build > Windows Playable`
- `Duel Genesis > Build > Windows Playable and Run`
- `Duel Genesis > Build > Windows Development Build`

The executable is generated at:

`Builds/DuelGenesis/DuelGenesis.exe`

The v0.7 build tool saves open scenes/assets first, builds the Windows player, then copies the supplied DMO card faces and frame/back images into the built player's StreamingAssets folder. Build-and-Run launches only after that copy finishes, so the standalone game can use the same authoritative card PNGs as the Unity Editor.

## Production card-data pipeline

The built-in 24-card Genesis set remains useful as a controlled rules-engine test set. The external JSON catalog can replace/extend it with real production card records.

Each JSON record supports:

- ID and card name
- Monster / Spell / Trap kind
- explicit/fallback frame kind
- rarity
- attribute and type line
- level, ATK and DEF
- effect text
- optional explicit model resource path

On startup, matching IDs replace existing definitions and new IDs are appended to the database.

## Validation tools

Unity automatically runs lightweight Editor validation after scripts reload. Manual tools are also available under the `Duel Genesis` top menu:

- `Run Automated Smoke Tests`
- `Run Vertical Slice Validation`
- `Production Assets > Scan DMO Libraries`
- `Production Assets > Verify Card Frame Sources`
- `Production Assets > Build Real Card Catalog from DMO Art`
- `DEV > Reset All Local Prototype Progress`
- `DEV > Add 50,000 GC In Play Mode`
- `DEV > Re-run Runtime Diagnostics In Play Mode`

## What is intentionally still in progress

The production libraries are now connected, but having the image/model does not by itself implement every printed card rule. Remaining production work includes:

- Expanding the rules/effect engine for the real card catalog
- Full chain-window and response timing rules
- Extra Deck summon families and their rules
- Better material/VFX/animation selection for DMO models
- More advanced CPU strategy for the full card pool
- Online accounts, multiplayer networking, matchmaking and server persistence
- Trading and player marketplace
- Expanded Genesis City MMO zones

Only use/distribute card art, models, audio or other content where you have the necessary rights or permission.

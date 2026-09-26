# Duel: Genesis

Duel: Genesis is a 3D card-dueling game set in Genesis City. The current build connects the supplied DMO card faces, monster prefabs and animations to the playable Unity vertical slice.

## Current playable loop

- Runtime title screen with Enter Genesis / Controls / Quit flow
- Third-person Genesis City exploration
- Genesis Card Shop and persistent Genesis Credits (GC)
- Five-card booster opening using the real production card catalog only
- Persistent card collection and duplicate counts
- 40–60 card Main Deck builder with three-copy validation
- Automatic 40-card production starter deck for new/migrated saves
- Duelist level, XP, profile, packs and win/loss progression
- Physical duel-table interaction
- 8,000 LP single-player CPU duels
- Draw, Main and Battle flow
- Normal / Tribute Summons, attack/defense positions and monster setting
- Five Monster Zones and five Spell/Trap Zones
- Graveyard and Banished tracking
- Physical tabletop monsters, backrow, Deck, Graveyard and Banished presentation
- Supplied DMO card-face artwork, card names and Level/Rank metadata
- Correct Normal, Effect, Fusion, Ritual, Synchro, Xyz, Spell, Trap and Token frame metadata
- Supplied DMO monster prefab and animation lookup by normalized card name
- Windows x64 build and Build-and-Run commands

## Production cards only

The old DG001–DG024 Genesis test cards are retired from the runtime card database. They are not used by booster packs, the starter deck, the collection or deck builder.

When an older development save is loaded, retired DG card entries are automatically removed from its collection and Main Deck. If that leaves the player without a legal deck, Duel: Genesis builds a new 40-card starter using cards from the real imported catalog.

If the production catalog is missing, the game intentionally does **not** fall back to fake cards. The shop refuses the purchase until real card data is loaded.

## First-time real-card setup

After pulling the latest GitHub changes:

1. Open the project in Unity 6.6 and let the DMO character package finish importing.
2. Use `Duel Genesis > Production Assets > Scan DMO Libraries` if you want an asset count/check.
3. Use `Duel Genesis > Production Assets > Build Real Card Catalog from DMO Art`.
4. Wait for the catalog-ready popup.
5. Open `Assets/Scenes/GenesisPrototype.unity` and press Play.

The catalog builder downloads public card metadata once, matches it only against PNG filenames already present in `Cards/DMO_card_art/cards`, and writes:

`Assets/StreamingAssets/duel_genesis_cards.json`

It also writes an unmatched-name report beside the catalog.

## Card visual rule

The supplied DMO card image/art library is associated by normalized card name. The production metadata supplies the card's real name, type, Level/Rank, ATK, DEF and effect text. The corresponding frame type is preserved for Normal, Effect, Fusion, Ritual, Synchro, Xyz, Spell, Trap and Token cards.

The runtime card preview draws the card name and monster Level/Rank information together with the correct card artwork/frame. A Fusion card cannot be displayed as an Effect card, and Spell/Trap frames are kept separate.

## Monster models and animations

`Characters` is mounted as the local Unity package `com.hikarimist.dmo-characters`.

The game reads `dmo_manifest.txt`, normalizes card and character names, then resolves matching prefabs from `Resources/Models`. Matching animation clips are loaded from each character's `Resources/Animations/<Character>` folder. An explicit `modelResource` in the JSON catalog can still override the automatic match.

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
- `Esc` — pause while exploring
- `F9` — Editor-only +50,000 GC testing shortcut

## Windows build

Use one of:

- `Duel Genesis > Build > Windows Playable`
- `Duel Genesis > Build > Windows Playable and Run`
- `Duel Genesis > Build > Windows Development Build`

The executable is generated at:

`Builds/DuelGenesis/DuelGenesis.exe`

The build pipeline copies the DMO card faces/frame resources needed by the standalone player into StreamingAssets.

## Validation tools

- `Duel Genesis > Run Automated Smoke Tests`
- `Duel Genesis > Run Vertical Slice Validation`
- `Duel Genesis > Production Assets > Scan DMO Libraries`
- `Duel Genesis > Production Assets > Verify Card Frame Sources`
- `Duel Genesis > Production Assets > Build Real Card Catalog from DMO Art`
- `Duel Genesis > DEV > Reset All Local Prototype Progress`
- `Duel Genesis > DEV > Add 50,000 GC In Play Mode`
- `Duel Genesis > DEV > Re-run Runtime Diagnostics In Play Mode`

## Still in progress

The real asset libraries are connected, but every printed card effect is not yet implemented by the duel rules engine. Remaining work includes the generic effect/chain system, Extra Deck summon rules, better animation/VFX selection, stronger CPU strategy, networking, trading and expanded Genesis City areas.

Only use or distribute card art, models, audio or other content where you have the necessary rights or permission.

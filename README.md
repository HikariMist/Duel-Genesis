# Duel: Genesis

Duel: Genesis is a 3D online card-dueling game set in Genesis City. Players explore a persistent world, visit card shops, build collections and decks, sit at physical duel tables, and battle using cards represented by miniature 3D monsters inside tabletop arenas.

## Current prototype milestone

The repository now includes the first playable foundation:

- Third-person character movement
- Third-person camera
- Interaction raycast system
- Genesis Credits wallet
- Card shop terminal prototype
- Duel-table sitting / leaving interaction
- One-click editor utility that generates a playable prototype scene

## Build the first playable scene

After pulling the latest GitHub changes into your local project:

1. Open the project in Unity.
2. Wait for Unity to finish importing and compiling.
3. In Unity's top menu choose:
   `Duel Genesis > Build First Playable Prototype`
4. Unity creates:
   `Assets/Scenes/GenesisPrototype.unity`
5. Open that scene if it is not already open.
6. Press Play.

## Prototype controls

- `WASD` — move
- Mouse — rotate camera
- `Space` — jump
- `E` — interact
- `Q` — leave duel-table seat
- `Esc` — release mouse cursor
- Left-click Game view — capture cursor again

## What exists in the generated scene

- Player/Hikari blockout capsule
- Test Genesis City ground
- Genesis Card Shop blockout
- Pack terminal costing 1,000 GC
- Player starts with 5,000 GC
- Duel Table blockout
- Seat interaction
- Basic interaction prompt

This is intentionally a greybox/blockout milestone. Art, networking, full card rules, collection persistence, pack-opening UI, multiplayer and production models come later.

## Next implementation milestones

1. Replace the blockout environment with a first Genesis City plaza.
2. Add a real card shop UI and pack-opening flow.
3. Create the card data model and player collection.
4. Create the deck builder.
5. Upgrade the duel table into the miniature arena system.
6. Implement the duel state machine and physical card placement.
7. Connect monster cards to 3D monster prefabs.
8. Add online multiplayer architecture.

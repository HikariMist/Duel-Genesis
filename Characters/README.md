# DMO character assets

496 characters and 2,844 animation clips extracted from Duel Monsters Online,
converted to native Unity assets. Open the folder as a Unity project with
**Unity 6000.5.4f1**; the first open takes a while because Unity has to build
its import cache for ~3.4 GB of assets.

## Layout

| path | what |
|---|---|
| `Assets/Viewer/DMO/<Character>/` | mesh, material, textures and avatar for one character |
| `Assets/Viewer/Resources/Models/<Character>.prefab` | the rigged character, ready to drag into a scene |
| `Assets/Viewer/Resources/Animations/<Character>/*.anim` | that character's clips |
| `Assets/Viewer/Resources/dmo_manifest.txt` | tab-separated `character<TAB>clip count`, 496 rows |

Mesh assets keep the names they had in the game, which are mostly Chinese, so
the prefab is the thing to go by rather than the mesh filename.

## Using a character

Drag `Assets/Viewer/Resources/Models/<Character>.prefab` into a scene and play a
clip from the matching folder under `Animations/`. There is no viewer scene and
no Animator controller: add an Animator (or an Animation component) and wire the
clips up however suits you.

The clips are **generic**, not humanoid. Every curve is bound to a transform by
its hierarchy path, so a clip only plays correctly on the prefab it came from --
they are not interchangeable between characters, even where two rigs look alike.

## Known gaps

* 16 characters have no clips. They are listed in the manifest with a count of
  0: their animation is stored in Unity's humanoid muscle format and their rigs
  would not build a valid avatar, so nothing was baked for them. The meshes and
  prefabs are complete and usable.
* 6 clips are single-frame poses rather than animation -- zero length, one key
  per curve. They come that way in the game. The rest were checked clip by clip
  against their rigs: every curve binds to a real transform and the rig moves.
* Nine human duellists and NPCs (Bandit Keith, Mai Valentine and so on) were
  deliberately left out to keep the set to monsters.

# Checkpoint history

`checkpoint-full-import` -- every character in the game (505 characters,
1,780 clips, 0 verification failures, 1,253 MB player). Humanoid clips
still out of scope; those characters are present in rest pose with (0) clips.

`checkpoint-generic-verified` -- the 22-character sample, below.

# checkpoint-generic-verified: generic pipeline complete and verified

Tagged `checkpoint-generic-verified`. This is the rollback point taken before
starting work on humanoid clip support.

## What works at this commit

DMO characters extract from the game install, import into Unity as prefabs with
rebuilt meshes, skeletons, materials and textures, and play their animation in
the viewer: click a character, click a clip, watch it.

Verified on a 22-character batch chosen to span rigid props, mounts, multi-mesh
rigs and the extremes of the data (7 to 321 bones, 1 to 24 meshes, up to 118k
vertices):

| | |
|---|---|
| `DmoVerify` | **PASS** -- 58 ok, 1 pose, 0 failures |
| Clips binding | every clip resolves 100% of its paths |
| Rigid props | zero stranded parts |
| Player self-test | PASS, bones move in the built player |
| Extraction | 22 s, 735 MB peak, 52 MB of bundles |
| Unity import | 30 s, 59 clips, 130 MB of assets |

Clip decoder across the whole game: **2054 / 2054 clips decode**, worst
quaternion norm error 1.0e-06.

## What does not work

**Humanoid clips are not supported** -- 21.2% of the game's clips (430 of 2027),
affecting 44 of 536 characters. They bind to the Animator in muscle/DoF space
rather than to transform paths. Those characters' *models* import and render
correctly; they simply appear with `(0)` clips. See the README section
"What this pipeline does not do: humanoid clips".

This is the thing the next piece of work is meant to fix. If that work goes
wrong, roll back to here.

## Rolling back

```bash
cd /c/Users/kyled/Desktop/AnimationViewer
git checkout checkpoint-generic-verified
bash Tools/dmo_extract/regen.sh
```

The generated assets are deliberately not versioned -- roughly 250 MB of purely
derived data. `regen.sh` rebuilds all of it from the committed source in about
a minute and re-runs the verification, so a rollback is complete rather than
partial.

To roll back only the code and keep the currently imported assets, drop the
`regen.sh` line; the assets on disk stay as they are.

## Layout

| | |
|---|---|
| `Tools/dmo_extract/` | the extractor (Python); see its README |
| `Assets/Viewer/Runtime/` | the viewer itself |
| `Assets/Viewer/Editor/DmoImport.cs` | builds prefabs and clips from bundles |
| `Assets/Viewer/Editor/DmoVerify.cs` | binding, structural and motion checks |
| `Assets/Viewer/Editor/DmoSnapshot.cs` | filmstrips and turntables |
| `Tools/dmo_extract/diagnose*.py` | the investigations behind the decoder |

Nothing in this project writes to the DMO install; it is read-only input.

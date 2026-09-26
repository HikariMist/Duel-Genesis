# Where the full run is up to

Live state for the "bring in every character" run. If the session ended
mid-way, this says what is done and what the next command is.

Rollback point if anything here goes wrong: `git checkout checkpoint-generic-verified`
then `bash Tools/dmo_extract/regen.sh` (see CHECKPOINT.md).

## The plan, in order

1. **Extract every character** -- `python extract.py --all`
2. **Import into Unity** -- `DmoImport.Batch`
3. **Verify** -- `DmoVerify.Batch`
4. **Spot-check renders** -- `DmoSnapshot.Batch` (optional; writes a lot of PNGs)
5. **Build + self-test the player**

Humanoid clips are deliberately out of scope for this run. Those characters
come in as models with `(0)` clips, in rest pose, which is expected and is what
was agreed. Humanoid animation is a separate piece of work afterwards.

## Resuming is safe and cheap

`extract.py --all` **skips any character whose `.model` and `.clips` already
exist**, so re-running it continues where it stopped rather than starting over.
Use `--force` to rebuild everything from scratch.

```bash
cd Tools/dmo_extract
python extract.py --all           # continues; prints "N already done"
```

If a run was killed *while writing* a character, that character's `.clips` may
be truncated. Delete the newest pair before resuming:

```bash
NEWEST=$(ls -t out/*.model | head -1); B="${NEWEST%.model}"; rm -f "$B.model" "$B.clips"
```

Then the Unity side (each is safe to re-run; the importer rebuilds from the
bundles on disk):

```bash
UNITY="/c/Program Files/Unity/Hub/Editor/6000.5.4f1/Editor/Unity.exe"
P="C:\\Users\\kyled\\Desktop\\AnimationViewer"
"$UNITY" -batchmode -quit -projectPath "$P" -executeMethod AnimationViewer.EditorTools.DmoImport.Batch -logFile import.log
"$UNITY" -batchmode -quit -nographics -projectPath "$P" -executeMethod AnimationViewer.EditorTools.DmoVerify.Batch -logFile verify.log
```

## Things already fixed during this run

- **Console encoding.** At least one character is named with a `Ω`, which the
  Windows console codepage cannot encode. The `print` itself raised
  `UnicodeEncodeError` and killed the run 155 characters in -- and then killed
  the handler that tried to report the failure. stdout/stderr are now
  reconfigured to UTF-8 with replacement at startup.
- **Resume support**, as above, added for exactly this reason.
- **Bulk import memory.** `DmoImport` now saves, unloads unused assets and
  collects every 25 characters, and logs progress; a few hundred characters
  built in one editor session otherwise stay resident throughout.

## Expected shape of the result

From the 22-character sample, scaled: roughly 500+ characters, ~1,590 usable
(generic) clips, on the order of 1.5-2 GB of Unity assets. Characters that are
purely scenery raise `has no skinned meshes` and are skipped -- that is normal,
not a failure to chase.

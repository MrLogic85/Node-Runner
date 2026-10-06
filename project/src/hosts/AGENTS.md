# AGENTS.md — `src/hosts/`

Routed scene roots that wire screens to managers and the navigator. The
dependency rule is in `docs/ARCHITECTURE.md` → Dependency rules (Scene
roots); routing is in its Navigation section.

## Rules

1. **One `*Host` per routed screen.** The script is the root of
   `project/scenes/hosts/<Name>Host.tscn` and instances its screen from
   `project/scenes/screens/`.
2. **Wire, don't decide.** A host connects screen signals to managers and
   `SceneRouter`; game rules belong in `libs/`, layout in the screen scene.
3. **Nothing depends on a host.** Helpers used only by hosts
   (`CreationActions`, `EvolverTrainingProgressSource`) live here too.
4. **Pause is global.** `TrainingHost` resets `GetTree().Paused` in `_Ready`
   and `_ExitTree`, and its root is `ProcessMode.Always` so the screen
   keeps responding while paused; the creature, `Evolver` and each slot are
   pinned to `Pausable` so training freezes.
5. **A host run on its own (F6) has no route** (`_route is null`) and
   still opens: `TrainingHost` trains the Walker example without saving.

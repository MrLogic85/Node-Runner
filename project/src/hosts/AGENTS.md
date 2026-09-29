# AGENTS.md — `src/hosts/`

**Routed scene roots that wire screens to managers and the navigator.**

The dependency rule lives in `docs/ARCHITECTURE.md` (*Dependency rules →
Scene roots*); routing lives in its *Navigation* section. In short:

1. **One `*Host` per routed screen.** The script is the root of
   `project/scenes/hosts/<Name>Host.tscn` and instances the matching screen
   from `project/scenes/screens/`.
2. **Wire, don't decide.** A host connects screen signals to managers and
   `SceneRouter`. Game rules belong in `libs/`; layout belongs in the screen
   scene.
3. **Nothing depends on a host.** Helpers used only by hosts (for example
   `CreationActions`, `EvolverTrainingProgressSource`) live here too.

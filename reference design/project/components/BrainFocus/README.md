The brain view, opened by the Brain icon on Creation or Training. It shows the network and, on tap, **the weights on the links of one neuron**, which is how a player sees which sensors drive which motors.

The top bar shows the brain's shape as a locked chip ("1 layer · 4 neurons"); it is set in Build (see BrainSetup) and cannot change after Save.

Columns are implied by position; inputs and outputs carry the names of the cores and motors ("Left foot down", "Hip"), never indices. Firing neurons are filled `accent` with a glow; quiet ones are outlined.

**Tap a neuron** and it is ringed in `halo`. Every other link fades to a 1px `line`. Its own links come forward: **thickness is the strength** of the weight, a **solid `accent` line pushes up (positive)** and a **dashed `ink` line pushes down (negative)**, so sign never depends on colour. Each link carries a small pill with its number ("+0.82", "−0.31").

The bottom card lists the same numbers as **diverging bars** around a zero line: *Listens to* (the links coming in) and *Pushes* (the links going out) for a hidden neuron. Right of the title is one sentence made from its strongest link.

**Tap an output** (Hip, Knee) and the card answers the question directly: **which sensors move it**, each as one bar that adds up its influence through the hidden neurons. This is the "why did it do that?" shortcut. Tap empty space to clear the selection. Weights are read-only here.
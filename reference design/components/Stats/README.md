Opened from **Stats** in the BuildLocked overflow menu. It is about the map the creation last trained on (named in the top bar, "Stats · Flat ground"). Numbers on the left, two charts on the right.

**Numbers.** One card of value rows (see Value row in the Component Library), all in view at once: **Generations** and time **Trained**, then under **Records** the **Distance**, **Speed** and **Elevation**. Each record is the **record for its own metric**: the most any shadow reached in any generation on this map. So they may come from different shadows and different generations, and each is the peak of chart 2 for that metric.

**Metric.** One segmented control switches both charts between **Distance**, **Speed** and **Elevation**. There is only ever one metric, and so one y axis, per chart.

**Chart 1, the latest generation over its run.** x is the seconds of the run (0 to the run length), y the chosen metric. The generation's **winner** is the `accent` line, with its final value labelled at the end; the **other shadows** of that generation are drawn faintly behind it in `line-strong`, as in the arena. A small key names the two. The winner is the shadow that went furthest, also when the chart shows speed or elevation: distance is the only thing evolution selects on.

**Chart 2, progress over generations.** x is the generation, y the highest value of the chosen metric that any shadow reached in that generation. One line, no average. It shows whether training is still paying off: a line still climbing is worth training on, a flat one is not. Its peak is the record, marked with a dot and labelled ("record 18.4 m"); nothing else is labelled.

Both charts use the same axes: a faint `line` grid, a few ticks in `readout-sm` `muted`, the axis named in `caption`, and one `accent` line with the glow of anything live.
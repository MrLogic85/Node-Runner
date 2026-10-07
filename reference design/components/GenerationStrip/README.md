One **bar cell** per **shadow** (the copies of the creation that race at once, each with a slightly different brain), under the caption "Generation 37" and a bar that fills as the followed run goes by. Generations count from 1.

**A cell** is square, `control` high, on `panel` with a `line-strong` hairline. Its bar grows from the bottom with the shadow's distance so far, measured against this generation's leader, whose bar is full.

**Only the followed cell is marked**: a 2 px `accent` border and an `accent` bar. The leader is not marked: the lead changes too often to follow with the eye. By default the followed shadow is shadow 1, the previous best, which replays its run; a tap on a cell follows that shadow until the generation ends, then it goes back to shadow 1.

**Order.** Shadow order, shadow 1 on the right. A sort ranks by distance at that moment and holds until the next sort, so cells never jump while the player watches.

**Paging.** The strip has as many cells as fit its width, at least 3. With more shadows than that it pages: a **worse** chevron first, then the shadows, and as the last cell **Sort** on the first page or a **better** chevron on later pages. Only the shadows on the page are drawn in the arena, plus the followed one. A new generation starts on the first page, in shadow order.
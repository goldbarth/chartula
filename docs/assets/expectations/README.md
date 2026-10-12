# Measurements behind What to expect from Chartula

These assets present captured measurements from [chartula-evals](https://github.com/goldbarth/chartula-evals) inside Chartula's documentation.
The page is [What to expect from Chartula](../../what-to-expect.md).
The charts show measured cost, duration and completion; they do not score text quality or confirm checker flags.
This presentation documents the comparison tracked by [#324](https://github.com/goldbarth/chartula/issues/324), alongside the measured-model table and recommendations in the provider and cost guides.

## Data

- `october-runs.csv`: 84 runs, 74 complete, from four release scenarios on October 9, 2026.
- `october-summary.csv`: the 28 scenario/configuration summaries, including the premium configurations omitted from the overview charts.
- `september-runs.csv`: 43 attempts, 42 complete, from the separate September 25, 2026 ServiceDeskLite comparison.
- `september-summary.csv`: the 14 configurations of that comparison.
- `provenance.json`: historical tool versions, price-table references, source paths and SHA-256 checksums of source and captured data files.

The original sources are `sweeps/results/{october,september}/{runs,configurations}.csv` in the sibling evaluation repository.
The captured run files omit raw texts, pull request references, source-folder names and key identifiers.
Empty numeric fields mean unknown, not zero.
Costs without cache use the historical list rate for every input token.
Costs after cache are calculated from reported usage, not verified against invoices.

Cost and duration summaries use complete runs only.
Completion counts retain incomplete and failed attempts.
The overview's six model combinations were measured on all four October scenarios.
The 84-run total also includes premium writers measured on two scenarios.

Each plotted point is a median and each line the observed min-max range.
The completion chart groups complete and incomplete runs; squares show their counts, not chronological order.
All shown settings in the September chart have three complete runs.
The October and September versions and prompts differ and are kept separate.

## Render the charts

```bash
python3 docs/assets/expectations/render.py
python3 docs/assets/expectations/render.py --png
```

The first command requires only Python's standard library and writes SVG files beside the script.
The second also writes PNGs and requires `rsvg-convert`.
The script draws captured summaries; it runs no experiments, calls no model and reads no rubric.
SVG and PNG outputs are generated files and must be updated through the script.
The evaluation repository remains responsible for collecting and comparing runs.

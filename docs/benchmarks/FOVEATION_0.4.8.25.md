# Foveation 0.4.8.25: first-pass benchmark

User-supplied exploratory results for NOVR 0.4.8.25 on an RTX 4080,
Direct3D 11, Deckard MP / Steam Frame at 120 Hz. SteamVR's recommended
resolution was 3028 × 3028 per eye. Each run lasted 30 seconds: two Off
baselines and six presets, with 21,722 frame records in the supplied summary.

The values below are transcribed from the supplied benchmark image. Underlying
frame records are not included here. Percentages are the supplied changes
against the mean of the two Off runs; rounded values may not reproduce them
exactly. Lower is better for these three measurements.

| Mode | Total render GPU, ms/frame | World opaque GPU, ms/both eyes | World opaque pixel-shader invocations, millions/both eyes |
| --- | ---: | ---: | ---: |
| Off, two-run mean | 10.38 | 2.23 | 41.0 |
| Fixed / Mild | 9.48 (−8.7%) | 1.82 (−18.3%) | 30.3 (−26.2%) |
| Fixed / Balanced | 10.12 (−2.5%) | 1.79 (−19.6%) | 26.9 (−34.3%) |
| Fixed / Strong | 9.94 (−4.3%) | 1.67 (−24.9%) | 22.5 (−45.1%) |
| Eye-tracked / Mild | 9.79 (−5.7%) | 1.82 (−18.3%) | 29.7 (−27.6%) |
| Eye-tracked / Balanced | 9.52 (−8.4%) | 1.65 (−26.0%) | 25.9 (−36.9%) |
| Eye-tracked / Strong | 9.43 (−9.2%) | 1.58 (−29.2%) | 22.1 (−46.0%) |

The world opaque measurements show less shader work and lower GPU time in this
test. Total GPU time is not monotonic with strength: Fixed / Mild was faster
than Fixed / Balanced and Fixed / Strong. The two Off total-GPU runs ranged
from 10.02 to 10.75 ms, a reported 7.2% drift. This range is not a confidence
interval. Head pose and workload were not locked, and eye tracking was
user-confirmed rather than continuously logged. These results do not establish
a reliable ranking between fixed and eye-tracked modes or between presets.

World opaques cover only part of rendering. Their shader-invocation and timing
reductions are not FPS gains, and further reducing this pass leaves the rest of
the frame's work. None of the reported total-GPU means reaches the 8.33 ms
budget for 120 Hz. This benchmark does not measure the proposed Extreme or
Maximum profiles, establish visual comfort, or predict results on other scenes
or hardware.

For follow-up comparisons, repeat interleaved Off/preset/Off runs in the same
scene with stable view and workload; record gaze validity, MSAA, actual applied
shading rates and any rate clamp. The proposed stronger profiles retain the
game's MSAA setting: their 4×4 outer rate is available only without MSAA, while
2×/4× MSAA caps it at 2×2. Their smaller protected regions and 2×2 middle rate
still change coverage when capped, but any additional gain needs measurement.

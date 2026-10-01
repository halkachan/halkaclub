# Message font

`k8x12L.ttf` is the current HALKA WORLD message font. It was copied unchanged from the locally installed k8x12L font. Unity imports it with hinted raster rendering at 12 points; the message GUI draws it at an integer 2× size. Original font and use terms: https://littlelimit.net/k8x12.htm and https://littlelimit.net/font.htm (Num Kadoma).

The older subset below remains for project history but is not used by the current scene.

`HalkaMessageSubset.ttf` is a regular-weight subset of Google Fonts' Noto Sans JP (`NotoSansJP[wght].ttf`). It contains only `い`, `し`, and `。` for the ver1.2 stone message. The internal font family was renamed to `HALKA Message`; the source is licensed under the SIL Open Font License 1.1, included as `OFL.txt`.

When future messages need more characters, regenerate this subset from the official font and keep the license file. The source repository is `https://github.com/google/fonts/tree/main/ofl/notosansjp`.

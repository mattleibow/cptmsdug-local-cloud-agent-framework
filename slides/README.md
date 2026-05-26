# Slides — Unifying Local and Cloud AI with MAF

Deck for the Cape Town MS Developer User Group session by [@mattleibow](https://github.com/mattleibow).

## Build

```bash
cd slides
npm install
node build.js
```

Output: `Unifying-Local-Cloud-AI.pptx`

## Render to images (for visual QA)

```bash
soffice --headless --convert-to pdf Unifying-Local-Cloud-AI.pptx
pdftoppm -jpeg -r 150 Unifying-Local-Cloud-AI.pdf slide
```

## Export high-resolution PNGs

```bash
./export-images.sh          # 300 DPI → 4000×2250 px
./export-images.sh 200      # 200 DPI → 2666×1500 px
./export-images.sh 450      # 450 DPI → 6000×3375 px (print quality)
```

PNGs land in `slides/images/` (gitignored).

## Edit content

All slide content + layout lives in `build.js`. Tweak the strings, re-run `node build.js`.

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

## Edit content

All slide content + layout lives in `build.js`. Tweak the strings, re-run `node build.js`.

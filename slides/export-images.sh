#!/usr/bin/env bash
# Export slides as high-resolution PNG images.
# Usage: ./export-images.sh [dpi]   (default 300)
set -euo pipefail
cd "$(dirname "$0")"

DPI="${1:-300}"
PPTX="Unifying-Local-Cloud-AI.pptx"
PDF="Unifying-Local-Cloud-AI.pdf"
OUTDIR="images"

if [[ ! -f "$PPTX" ]]; then
  echo "→ Building $PPTX..."
  node build.js
fi

echo "→ Converting to PDF..."
soffice --headless --convert-to pdf "$PPTX" >/dev/null 2>&1

echo "→ Rendering PNGs at ${DPI} DPI to $OUTDIR/ ..."
mkdir -p "$OUTDIR"
rm -f "$OUTDIR"/slide-*.png
pdftoppm -png -r "$DPI" "$PDF" "$OUTDIR/slide"

# Slide dimensions at this DPI for reference
W_PX=$(( 13333 * DPI / 1000 ))
H_PX=$(( 7500  * DPI / 1000 ))
COUNT=$(ls "$OUTDIR"/slide-*.png | wc -l | tr -d ' ')
echo "✓ Exported $COUNT PNGs at ${W_PX}×${H_PX} px into $OUTDIR/"

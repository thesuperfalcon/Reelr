const SIZE = 400;

// The server takes at most 1 MB; anything a phone camera produces is far larger.
const MAX_INPUT_BYTES = 25 * 1024 * 1024;

function toBlob(canvas: HTMLCanvasElement, type: string): Promise<Blob | null> {
  return new Promise((resolve) => canvas.toBlob(resolve, type, 0.85));
}

// Crops a picture to a centred square and scales it to SIZE pixels, so uploads stay small whatever was picked.
export async function prepareAvatar(file: File): Promise<Blob> {
  if (!file.type.startsWith("image/")) {
    throw new Error("Choose an image file, such as a JPEG or PNG.");
  }
  if (file.size > MAX_INPUT_BYTES) {
    throw new Error("That picture is too large. Choose one under 25 MB.");
  }

  let bitmap: ImageBitmap;
  try {
    bitmap = await createImageBitmap(file);
  } catch {
    throw new Error("That picture could not be read. Try a JPEG or PNG.");
  }

  const side = Math.min(bitmap.width, bitmap.height);
  const size = Math.min(SIZE, side);
  const canvas = document.createElement("canvas");
  canvas.width = size;
  canvas.height = size;
  canvas
    .getContext("2d")!
    .drawImage(bitmap, (bitmap.width - side) / 2, (bitmap.height - side) / 2, side, side, 0, 0, size, size);
  bitmap.close();

  // Browsers without WebP encoding fall back to PNG for toBlob, so ask for JPEG instead in that case.
  const webp = await toBlob(canvas, "image/webp");
  const blob = webp?.type === "image/webp" ? webp : await toBlob(canvas, "image/jpeg");
  if (!blob) {
    throw new Error("That picture could not be prepared. Try another one.");
  }

  return blob;
}

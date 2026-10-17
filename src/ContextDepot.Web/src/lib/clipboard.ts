import copyToClipboard from "copy-to-clipboard";

export async function writeClipboard(value: string) {
  if (navigator.clipboard?.writeText)
    await navigator.clipboard.writeText(value);
  else if (!copyToClipboard(value)) throw new Error("Clipboard unavailable");
}

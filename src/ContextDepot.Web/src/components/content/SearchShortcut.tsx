export function SearchShortcut({ className }: { className?: string }) {
  const apple =
    typeof navigator !== "undefined" &&
    /Mac|iPhone|iPad/.test(navigator.userAgent);
  return (
    <kbd className={className} aria-hidden="true">
      {apple ? "⌘" : "Ctrl"} K
    </kbd>
  );
}

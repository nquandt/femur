# StreamParser & MarkdownParser Performance Refactor Plan

## Goal

Reduce the parse time and allocation gap between Femur and Markdig without breaking
any public API contracts or existing parser behaviour. The 274 parser tests and 200
renderer tests are the ground truth — every step in this plan must leave them green.

Current baseline (.NET 10, `medium.md` ~2.5 KB):

| | Femur | Markdig | Ratio |
|---|---|---|---|
| Parse mean | 102 µs | 41 µs | **2.5×** |
| Parse allocs | 161 KB | 38 KB | **4.2×** |

The root causes are architectural, not algorithmic. The optimisations already applied
(cached regexes, list-based delimiter stack, O(n) inline rebuild, single-pass HTML
escape, etc.) were correct improvements but are dwarfed by:

1. **Character-by-character stream reading** through a virtual dispatch.
2. **Full `List<string>` materialisation** — every line becomes a `string` before
   parsing begins.
3. **`string.Substring()` everywhere** in block parsing — every token extraction
   allocates a new heap string.

Markdig works entirely on a single `string` using `StringSlice` (a
`ReadOnlySpan<char>`-equivalent struct), so it never allocates for sub-strings during
block parsing. That is the structural difference we need to close.

---

## Constraints

- **Public API of `MarkdownParser` must not change.**  
  The static overloads `Parse(string)`, `Parse(byte[])`, `Parse(Stream)` and the
  instance constructor `MarkdownParser(Stream, int, bool)` are the public surface.
  Return types and exception contracts are unchanged.

- **Public API of `StreamParser<T>` must not break existing subclasses.**  
  The abstract methods (`CreateDocument`, `InitializeParsing`, `ProcessCharacter`),
  virtual methods (`Cleanup`, `Dispose(bool)`), and all protected properties/methods
  (`Reader`, `Buffer`, `Position`, `Length`, `TotalCharsRead`, `StringBuilder`,
  `ReadMore`, `GetAbsolutePosition`, `SkipWhitespace`, `ReadUntil`, `ReadUntilAny`)
  must remain. The `TestStreamParser` test harness reflects exactly what must survive.

- **`netstandard2.0` target must be preserved.**  
  New code must either work on `netstandard2.0` or be guarded by `#if` with a
  `netstandard2.0` fallback. The `Compatibility/` shims are the pattern to follow.

- **All 274 parser tests and 200 renderer tests must remain green after every step.**

---

## Why the Current Design Is Slow

### The `ProcessCharacter` loop

`StreamParser.Parse()` loops over `ReadMore()` → `ProcessCharacter(char, doc)` for
every single character. `MarkdownParser.ProcessCharacter` does nothing but:

```csharp
// ~3 branches per char, one StringBuilder.Append per non-newline char
_currentLine.Append(ch);
Position++;
```

For a 20 KB document that is ~20,000 virtual dispatch calls plus ~20,000
`StringBuilder.Append(char)` calls just to accumulate lines. The resulting
`_currentLine.ToString()` call per line adds one heap `string` allocation per line on
top of that.

The base class `Parse()` loop also calls `ReadMore()` before every character, which
checks `Position < Length` (a branch that is almost always true, but still costs
something in the hot path).

### `List<string>` line store

All source lines are materialised as `string` objects in `_lines` before any block
parsing begins. For a 500-line document that is 500 string allocations just to hold
the input. Block parsing then does further `TrimStart()`, `Substring()`, and string
comparisons on each of those strings.

### `string.Substring()` in block parsing

Token extraction in `ParseBlockStructureRange` uses `Substring` everywhere:

- `line.Substring(i)` to strip leading `#` characters from a heading
- `trimmed.Substring(fenceLength).Trim()` for the fenced code block info string
- `trimmed.Substring(1).TrimStart()` to strip `>` from blockquotes
- `text.Substring(codeStart, codeEnd - codeStart)` in inline code span parsing
- … dozens more

Each call allocates a new heap string. Markdig uses `StringSlice` (start + length
offsets into the original string) so token extraction is free.

---

## Refactor Plan

The plan is structured as six sequential phases. Each phase is independently buildable
and testable. Later phases build on earlier ones but do not require them to be complete
first — phases 2–5 can be developed in parallel once phase 1 is done.

---

### Phase 1 — Add a line-oriented reading path to `StreamParser` (no breaking changes)

**Goal:** Let `MarkdownParser` bypass the char-by-char loop entirely by reading whole
lines at once, while leaving the existing `ProcessCharacter` contract untouched for
other subclasses.

#### 1.1 — Add `ReadLineIntoBuffer` to `StreamParser`

Add a new **non-virtual protected method** to `StreamParser<T>`:

```csharp
/// <summary>
/// Reads the next line from the stream into <paramref name="destination"/>.
/// Handles \r, \n, and \r\n. Returns false at end-of-stream.
/// The line content (without the line terminator) is appended to
/// <paramref name="destination"/>.
/// </summary>
protected bool ReadLineIntoBuffer(StringBuilder destination);
```

Internally this uses the existing rented `char[]` `Buffer` and `StreamReader`, so
no new allocations per call. It is simply a more efficient use of the same reading
infrastructure.

This method is **additive** — nothing that currently exists changes.

#### 1.2 — Add `SupportsLineReading` virtual property (default `false`)

```csharp
/// <summary>
/// When overridden to return true, the Parse() loop calls ProcessLine()
/// instead of ProcessCharacter() for each source line.
/// </summary>
protected virtual bool SupportsLineReading => false;

/// <summary>
/// Called once per source line when SupportsLineReading is true.
/// The line content does not include the line terminator.
/// Implementors must not advance Position manually.
/// </summary>
protected virtual void ProcessLine(ReadOnlySpan<char> line, TDocument document)
    => throw new NotImplementedException();
```

The `Parse()` loop becomes:

```csharp
if (SupportsLineReading)
{
    var lineBuffer = new StringBuilder(); // or stack-allocated for short lines
    while (ReadLineIntoBuffer(lineBuffer))
    {
        ProcessLine(lineBuffer.ToString().AsSpan(), document);
        lineBuffer.Clear();
    }
}
else
{
    // existing char-by-char loop, unchanged
    while (ReadMore()) { ProcessCharacter(Buffer[Position], document); }
}
```

Existing subclasses that do not override `SupportsLineReading` continue to use the old
path without any behaviour change.

**API compatibility:** The existing abstract/virtual surface is untouched. This is a
pure addition.

**Tests to run after this phase:** All StreamParser tests + all MarkdownParser tests
(nothing should change yet, since `MarkdownParser` has not been updated).

---

### Phase 2 — Replace `List<string>` with `ReadOnlyMemory<char>` line storage

**Goal:** Eliminate the per-line `string` allocation by storing lines as slices of a
shared buffer rather than independent strings.

#### 2.1 — Add a line-slice buffer struct

Introduce a small internal struct (in `Femur.Markdown.Parser`):

```csharp
/// <summary>
/// Represents a single source line as an offset+length into a shared char buffer,
/// avoiding per-line string allocations.
/// </summary>
internal readonly struct LineSpan
{
    public readonly int Start;   // offset into the shared buffer
    public readonly int Length;

    public LineSpan(int start, int length) { Start = start; Length = length; }

    // Convenience for callers that need a string (e.g. compatibility paths)
    public string ToString(char[] buffer) => new string(buffer, Start, Length);
    public ReadOnlySpan<char> AsSpan(char[] buffer) => buffer.AsSpan(Start, Length);
    public ReadOnlyMemory<char> AsMemory(char[] buffer) => buffer.AsMemory(Start, Length);
}
```

#### 2.2 — Add a growable source buffer to `MarkdownParser`

Replace `_lines: List<string>` and `_currentLine: StringBuilder` with:

```csharp
private char[]? _sourceBuffer;          // rented from ArrayPool; holds entire input
private int _sourceLength;              // valid chars in _sourceBuffer
private List<LineSpan>? _lineSpans;     // slices into _sourceBuffer, one per line
```

During `InitializeParsing`, rent an initial buffer:

```csharp
_sourceBuffer = ArrayPool<char>.Shared.Rent(initialCapacity);
_lineSpans = new List<LineSpan>(64);
```

Return it in `Cleanup` (before `base.Cleanup()`):

```csharp
if (_sourceBuffer != null)
{
    ArrayPool<char>.Shared.Return(_sourceBuffer);
    _sourceBuffer = null;
}
```

#### 2.3 — Override `SupportsLineReading` and `ProcessLine`

```csharp
protected override bool SupportsLineReading => true;

protected override void ProcessLine(ReadOnlySpan<char> line, MarkdownDocumentNode document)
{
    // Grow _sourceBuffer if needed
    EnsureSourceCapacity(_sourceLength + line.Length);

    var start = _sourceLength;
    line.CopyTo(_sourceBuffer.AsSpan(start));
    _sourceLength += line.Length;
    _lineSpans!.Add(new LineSpan(start, line.Length));
}
```

`EnsureSourceCapacity` doubles the rented buffer if the current one is too small
(rent a new one from `ArrayPool`, copy, return the old one).

**No more `ProcessCharacter` override needed once this is in place**, but keep the
override for now (it can simply be a no-op body that satisfies the abstract contract)
until Phase 3 fully switches the parsing over to `_lineSpans`.

#### 2.4 — Migrate block parsing from `List<string>` to `List<LineSpan>`

All methods in `#region Phase 1: Block Structure Parsing` that currently index into
`_lines` as `List<string>` are updated to index `_lineSpans` and use
`span.AsSpan(_sourceBuffer)` to get a `ReadOnlySpan<char>` for each line.

The existing `TryParseAtxHeading(string line, ...)` etc. signatures gain span-based
overloads:

```csharp
private bool TryParseAtxHeading(ReadOnlySpan<char> line, int lineIndex, out HeadingNode? heading);
```

The old `string` overloads are kept temporarily for any internal recursive callers
(blockquote content, list item content) that still swap `_lines`. Those callers are
updated in Phase 3.

**Tests to run after this phase:** All 274 parser tests. The allocations should drop
measurably here (no per-line `string` allocations for the main body of the document).

---

### Phase 3 — Replace `string.Substring()` with `ReadOnlySpan<char>` slicing in block parsing

**Goal:** Eliminate the remaining heap allocations in the hot block-parsing paths.

#### 3.1 — Introduce `StringSlice` or use `ReadOnlyMemory<char>` directly

Two options:

**Option A (simpler, less invasive):** Where a `string` result is currently used only
for comparison or further slicing (not stored on a node), replace `Substring()` with
span operations:

```csharp
// Before:
var info = line.Substring(fenceLength).Trim();

// After:
var info = line.Slice(fenceLength).Trim(); // ReadOnlySpan<char>; no alloc
```

For the ~20% of cases where the value is stored on a node (e.g. `CodeBlockNode.Info`,
`HeadingNode` text, `LinkNode.Url`), a `ToString()` call at the point of storage is
unavoidable — but that is one allocation per node, not one per line scanned.

**Option B (more thorough, more work):** Introduce a `StringSlice` struct similar to
Markdig's, holding a reference to the original source string and a start+length. Node
properties that currently store `string` become `StringSlice`, with lazy `ToString()`
only when the rendered HTML needs a `string`. This is a larger change affecting the
AST node types in `Femur.Markdown.Abstractions` and requires changes to the renderer.
This option is deferred to a future iteration.

**Recommended for this refactor: Option A.** It removes the bulk of allocations in the
scanning/matching paths while keeping the AST nodes exactly as they are today.

#### 3.2 — Block parsing method signature updates

Methods that currently accept `string line` and call `Substring()` internally get
updated to accept `ReadOnlySpan<char>` where the span is sufficient:

| Method | Change |
|---|---|
| `TryParseAtxHeading` | `string` → `ReadOnlySpan<char>`; store final text as `.ToString()` |
| `TryParseFencedCodeBlock` | `string` → `ReadOnlySpan<char>`; info string via `.ToString()` |
| `TryParseThematicBreak` | `string` → `ReadOnlySpan<char>` |
| `IsSetextUnderline` | `string` → `ReadOnlySpan<char>` |
| `IsBlockStart` | `string` → `ReadOnlySpan<char>` |
| `TryParseBlockQuote` | Span for line matching; content list stays as `LineSpan` from Phase 2 |
| `ParseParagraph` | Span for content lines; join only when storing node content |
| `TryParseList` / `ParseListItem` | Span for marker detection |

Methods that build multi-line content (paragraph body, code block body) still
materialise a `string` at the end — one allocation per node rather than one per
character of scanning.

#### 3.3 — `_lines` / `_lineSpans` swap pattern in nested block parsers

The current pattern for blockquote and list item parsing is:

```csharp
var savedLines = _lines;
_lines = contentLines; // List<string>
// ... parse ...
_lines = savedLines;
```

After Phase 2, this becomes:

```csharp
var savedSpans = _lineSpans;
var savedBuffer = _sourceBuffer;
_lineSpans = subContentSpans;          // List<LineSpan> sliced from a temp buffer
_sourceBuffer = subContentBuffer;
// ... parse ...
_lineSpans = savedSpans;
_sourceBuffer = savedBuffer;
```

The fenced div recursive parse (currently `new MarkdownParser(MemoryStream(...))`,
already eliminated in the previous optimisation pass by swapping `_lines`) continues
to work via the same swap pattern but now swaps `_lineSpans` + `_sourceBuffer`.

**Tests to run after this phase:** All 274 parser tests. Allocation numbers should
approach Markdig's range for block-only parsing.

---

### Phase 4 — Replace `ProcessCharacter` loop in `StreamParser` with a line-reading loop using `StreamReader.ReadLine()`-equivalent

**Goal:** Remove the per-character virtual dispatch overhead entirely for
`MarkdownParser`, while keeping the `ProcessCharacter` path available for other
`StreamParser` subclasses.

This phase lands the Phase 1 addition into `MarkdownParser`. Once `SupportsLineReading`
returns `true` and `ProcessLine` is implemented (Phase 2), the `Parse()` loop in
`StreamParser` bypasses the char-by-char path for `MarkdownParser`.

The char-by-char path should be **kept in `StreamParser`** — it is the contract for
the abstract `ProcessCharacter` and must remain for other (non-markdown) subclasses.

#### 4.1 — Implement `ReadLineIntoBuffer` efficiently

The implementation reads directly from the rented `char[]` `Buffer` without any
additional allocation:

```csharp
protected bool ReadLineIntoBuffer(StringBuilder destination)
{
    if (!ReadMore()) return false;

    while (true)
    {
        // Scan the current buffer for a newline
        var start = Position;
        while (Position < Length)
        {
            var ch = Buffer[Position];
            if (ch == '\n') { Position++; return true; }
            if (ch == '\r')
            {
                // peek for \r\n
                if (Position + 1 < Length)
                {
                    if (Buffer[Position + 1] == '\n') Position++;
                }
                else
                {
                    // \r at end of buffer — flush what we have, refill, check for \n
                    destination.Append(Buffer, start, Position - start);
                    Position++;
                    if (!ReadMore()) return true; // stream ended after \r
                    if (Buffer[Position] == '\n') Position++;
                    return true;
                }
                Position++;
                return true;
            }
            Position++;
        }
        // Append the non-newline chars we scanned in this buffer fill
        destination.Append(Buffer, start, Position - start);
        if (!ReadMore()) return destination.Length > 0 || /* empty final line */ true;
    }
}
```

This reads in chunks matching the existing `Buffer` size (default 4 KB) rather than
one character at a time.

#### 4.2 — Update `ParseBlockStructureRange` to avoid the `Parse()`-loop overhead

After this phase, `MarkdownParser.ProcessCharacter` becomes a stub (or is removed in
a future cleanup — kept for now as the abstract method must be implemented):

```csharp
// Satisfies the abstract contract; never called when SupportsLineReading = true
protected override void ProcessCharacter(char ch, MarkdownDocumentNode document)
{
    // No-op — line reading path is used instead
}
```

**Tests to run after this phase:** All 274 parser tests. Time measurements should show
the biggest single improvement here — the virtual dispatch overhead is eliminated.

---

### Phase 5 — Remove per-call `string` allocations in inline parsing

**Goal:** Reduce allocations in Phase 2 (inline) parsing, which is currently the
second-largest source of allocations after line materialisation.

Inline parsing currently:

1. Takes a `string text` representing the raw text of a paragraph or heading.
2. Calls `text.Substring(...)` to extract code spans, link text, URLs, etc.
3. Allocates new `MarkdownTextNode` objects with `string` content for every text run.

The approach:

#### 5.1 — Pass `ReadOnlySpan<char>` into `ParseInlineText`

Change the signature from:

```csharp
private List<Node> ParseInlineText(string text, int baseOffset, string originalText)
```

to:

```csharp
private List<Node> ParseInlineText(ReadOnlySpan<char> text, int baseOffset, ReadOnlySpan<char> originalText)
```

Internally, token boundaries are tracked as `(start, length)` integers into the span.
A `MarkdownTextNode` is only allocated when a text run is complete, calling
`text.Slice(start, length).ToString()` at that single point.

#### 5.2 — Span-based code span scanning

The backtick scan in `ParseInlineText` currently calls `text.IndexOf('`', searchStart)`
on a `string`. On a `ReadOnlySpan<char>` this becomes:

```csharp
var remaining = text.Slice(searchStart);
var idx = remaining.IndexOf('`');
```

No allocation.

#### 5.3 — Span-based link and image parsing

Bracket scanning (`[`, `]`, `(`, `)`) currently calls `text.IndexOf(']', ...)`.
On a span this is a direct scan with no allocation.

#### 5.4 — Compatibility note

`ReadOnlySpan<char>` cannot be stored as a field or captured in a lambda. The existing
`CommonMarkDelimiterProcessor` currently takes a `string _text`. It will need to either:
- Accept a `ReadOnlyMemory<char>` (storable as a field on `netstandard2.0`), or
- Continue to receive a `string` (call `.ToString()` once at the delimiter processor
  boundary — one allocation per paragraph, acceptable).

The simplest safe approach: pass `ReadOnlySpan<char>` to `ParseInlineText` but
materialise a `string` at the boundary with `CommonMarkDelimiterProcessor`. This still
saves all the `Substring` allocations inside `ParseInlineText` itself.

---

### Phase 6 — Measure, tune, and clean up

**Goal:** Validate the gains, remove dead code, and tidy the implementation.

#### 6.1 — Run benchmarks, compare to baseline

Re-run the full BenchmarkDotNet suite. Expected targets after all phases:

| Scenario | Before | Target | Markdig |
|---|---|---|---|
| Parse medium (time) | 102 µs | < 60 µs | 41 µs |
| Parse medium (allocs) | 161 KB | < 60 KB | 38 KB |
| Parse large (time) | 868 µs | < 500 µs | 365 µs |
| Parse large (allocs) | 1,188 KB | < 450 KB | 299 KB |

Femur will likely remain somewhat slower than Markdig because it builds a full
mutable `List<Node>`-based AST (vs Markdig's read-only struct-heavy representation),
and it supports features Markdig's common-mark pipeline does not (smart punctuation,
fenced divs). But the 4× allocation gap should close to 1.5–2×.

#### 6.2 — Remove `ProcessCharacter` dead code in `MarkdownParser`

Once `SupportsLineReading = true` and `ProcessLine` is the active path, the
`ProcessCharacter` override can be reduced to an empty stub with a comment. The
`_currentLine: StringBuilder` field and the `\r\n` handling logic inside
`ProcessCharacter` can be removed entirely (that logic moves into `ReadLineIntoBuffer`
in Phase 4).

#### 6.3 — Evaluate removing `_lines: List<string>` entirely

If Phase 2 and 3 are fully complete, `_lines` is no longer needed. Remove the field
and the guard in `ParseBlockStructure` that null-checks it.

#### 6.4 — Consider `ArrayPool`-rented `List<LineSpan>` replacement

`List<LineSpan>` still allocates its internal array. For very small documents this
is fine. For hot-path production use, a stack-allocated `Span<LineSpan>` (with an
`ArrayPool` fallback for large documents) could be used. This is an optional micro-
optimisation and should only be pursued if benchmarks show `List<LineSpan>` allocation
is a measurable contributor after all other phases are complete.

---

## Implementation Order and Sequencing

```
Phase 1 (StreamParser additions)
    │
    ├── Phase 2 (LineSpan buffer, line store)    ← can start immediately after Phase 1
    │       │
    │       └── Phase 3 (Span<char> block parsing) ← depends on Phase 2
    │
    └── Phase 4 (ProcessLine loop in Parse())    ← depends on Phase 1 & 2
            │
            └── Phase 5 (Span<char> inline parsing) ← depends on Phase 3 & 4
                    │
                    └── Phase 6 (measure, clean up)
```

Phases 2 and 4 can be developed in parallel by different contributors. Phase 3 depends
on Phase 2 being stable. Phase 5 depends on Phases 3 and 4.

---

## Risk Areas and Mitigations

### `\r\n` handling
The current `ProcessCharacter` has careful `\r\n` collapse logic. This moves into
`ReadLineIntoBuffer` in Phase 4. The **same test cases** that currently cover Windows
line endings will validate the new implementation. No new tests needed — just run them.

### `netstandard2.0` span support
`ReadOnlySpan<char>` is available on `netstandard2.1+` and `net5+`. On
`netstandard2.0` it is available via the `System.Memory` NuGet package, which
`Femur.Parsing` already implicitly depends on (the `Compatibility/` shims reference
`ReadOnlySpan<char>` and `StringBuilder.Append(ReadOnlySpan<char>)` already). Verify
the `<PackageReference>` for `System.Memory` is explicit in the `netstandard2.0`
target, or add it.

### `ArrayPool` source buffer growth
The source buffer that holds all input chars must handle documents of arbitrary size.
The growth strategy (double on overflow, return old array to pool) is straightforward
but must be tested with documents larger than the initial capacity (e.g. the existing
`large.md` test file at ~20 KB is a good stress case).

### Nested block parser `_lineSpans` swap
The swap pattern (save/restore `_lineSpans` and `_sourceBuffer`) used by blockquote
and list item parsers is the same pattern as the current `_lines` swap. The existing
tests cover nested blockquotes, lists within blockquotes, and fenced code blocks —
these will catch any regression in the swap logic.

### `CommonMarkDelimiterProcessor` string boundary
The delimiter processor takes a `string` for the current paragraph text. After
Phase 5, this becomes a single `text.Slice(paragraphStart, paragraphLength).ToString()`
call at the boundary — one allocation per paragraph. This is intentional and acceptable
for this iteration.

---

## What This Plan Does NOT Include

- **Changing the public AST node types** (`HeadingNode`, `ParagraphNode`, etc.). Their
  string properties remain as `string`. A `StringSlice`-based AST is a much larger
  undertaking (it affects the renderer, the walker, and every consumer of the AST) and
  is outside scope for this refactor.

- **Changing `StreamParser`'s char-by-char contract** for non-markdown subclasses. The
  `ProcessCharacter` abstract method, all protected properties, and all utility methods
  remain exactly as they are.

- **Source location tracking accuracy**. `SourceLocation` (offset, line, column) is
  currently approximated in several places. This plan does not improve or regress that.

- **Streaming (no-AST) parser path** (`MarkdownStreamingParser`). That path has its
  own architecture and is unaffected.

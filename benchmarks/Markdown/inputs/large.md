## Document Section 1

# Femur Markdown Parser

A _streaming_ CommonMark 0.31.2 parser built on top of **Femur.Parsing**.
It processes documents in two phases:

1. **Block structure** — line-by-line pass that identifies headings, lists,
   code blocks, block quotes, and paragraphs.
2. **Inline structure** — character-by-character pass that handles emphasis,
   links, images, and code spans.

## Features

Femur supports every construct defined in the core CommonMark specification:

- ATX headings (`# H1` through `###### H6`)
- Setext headings (underlined with `=` or `-`)
- Thematic breaks (`---`, `***`, `___`)
- Fenced code blocks (triple backtick or `~~~`)
- Indented code blocks (4-space indent)
- Block quotes (`>`)
- Ordered and unordered lists
- Hard and soft line breaks
- Inline code spans (`` `code` ``)
- Emphasis (`*em*`, `_em_`) and strong (`**strong**`, `__strong__`)
- Links (`[text](url "title")`) and reference links
- Images (`![alt](url)`)
- Raw HTML blocks

## Usage

```csharp
using var stream = new MemoryStream(Encoding.UTF8.GetBytes(markdown));
var parser = new MarkdownParser(stream);
var document = parser.Parse();

var renderer = new MarkdownHtmlRenderer();
var html = renderer.Render(document);
```

## Design Goals

The parser is **allocation-conscious**: it uses a sliding 4 KB buffer and
avoids materialising the full source string until Phase 2 inline parsing.

> "A strong foundation for scalable .NET applications."
> — Femur tagline

---

### Setext Heading
------------------

The renderer escapes HTML-special characters in all text content:
`<script>alert('xss')</script>` becomes
`&lt;script&gt;alert(&#39;xss&#39;)&lt;/script&gt;`.

Hard line break →  
Next line after hard break.

Soft line break:
Continues on the same rendered line.

## Indented Code Block

    public sealed class MarkdownParser : StreamParser<MarkdownDocumentNode>
    {
        public MarkdownDocumentNode Parse() { /* ... */ }
    }

## Block Quote

> Block quotes can contain **inline markup** and even nested structure.
> They continue as long as each line starts with `>`.
>
> A blank `>` line keeps the quote open.

## Mixed List

1. First ordered item with `code`
2. Second item — contains a [link](https://femur.dev)
3. Third item

- Bullet one: _italic text_
- Bullet two: **bold text**
- Bullet three: `code span`

## Reference Links

This paragraph uses a [reference link][femur] defined below.

[femur]: https://femur.dev "Femur Documentation"

## Image

![Femur logo](https://femur.dev/logo.png "The Femur logo")

---

*End of medium sample.*


## Document Section 2

# Femur Markdown Parser

A _streaming_ CommonMark 0.31.2 parser built on top of **Femur.Parsing**.
It processes documents in two phases:

1. **Block structure** — line-by-line pass that identifies headings, lists,
   code blocks, block quotes, and paragraphs.
2. **Inline structure** — character-by-character pass that handles emphasis,
   links, images, and code spans.

## Features

Femur supports every construct defined in the core CommonMark specification:

- ATX headings (`# H1` through `###### H6`)
- Setext headings (underlined with `=` or `-`)
- Thematic breaks (`---`, `***`, `___`)
- Fenced code blocks (triple backtick or `~~~`)
- Indented code blocks (4-space indent)
- Block quotes (`>`)
- Ordered and unordered lists
- Hard and soft line breaks
- Inline code spans (`` `code` ``)
- Emphasis (`*em*`, `_em_`) and strong (`**strong**`, `__strong__`)
- Links (`[text](url "title")`) and reference links
- Images (`![alt](url)`)
- Raw HTML blocks

## Usage

```csharp
using var stream = new MemoryStream(Encoding.UTF8.GetBytes(markdown));
var parser = new MarkdownParser(stream);
var document = parser.Parse();

var renderer = new MarkdownHtmlRenderer();
var html = renderer.Render(document);
```

## Design Goals

The parser is **allocation-conscious**: it uses a sliding 4 KB buffer and
avoids materialising the full source string until Phase 2 inline parsing.

> "A strong foundation for scalable .NET applications."
> — Femur tagline

---

### Setext Heading
------------------

The renderer escapes HTML-special characters in all text content:
`<script>alert('xss')</script>` becomes
`&lt;script&gt;alert(&#39;xss&#39;)&lt;/script&gt;`.

Hard line break →  
Next line after hard break.

Soft line break:
Continues on the same rendered line.

## Indented Code Block

    public sealed class MarkdownParser : StreamParser<MarkdownDocumentNode>
    {
        public MarkdownDocumentNode Parse() { /* ... */ }
    }

## Block Quote

> Block quotes can contain **inline markup** and even nested structure.
> They continue as long as each line starts with `>`.
>
> A blank `>` line keeps the quote open.

## Mixed List

1. First ordered item with `code`
2. Second item — contains a [link](https://femur.dev)
3. Third item

- Bullet one: _italic text_
- Bullet two: **bold text**
- Bullet three: `code span`

## Reference Links

This paragraph uses a [reference link][femur] defined below.

[femur]: https://femur.dev "Femur Documentation"

## Image

![Femur logo](https://femur.dev/logo.png "The Femur logo")

---

*End of medium sample.*


## Document Section 3

# Femur Markdown Parser

A _streaming_ CommonMark 0.31.2 parser built on top of **Femur.Parsing**.
It processes documents in two phases:

1. **Block structure** — line-by-line pass that identifies headings, lists,
   code blocks, block quotes, and paragraphs.
2. **Inline structure** — character-by-character pass that handles emphasis,
   links, images, and code spans.

## Features

Femur supports every construct defined in the core CommonMark specification:

- ATX headings (`# H1` through `###### H6`)
- Setext headings (underlined with `=` or `-`)
- Thematic breaks (`---`, `***`, `___`)
- Fenced code blocks (triple backtick or `~~~`)
- Indented code blocks (4-space indent)
- Block quotes (`>`)
- Ordered and unordered lists
- Hard and soft line breaks
- Inline code spans (`` `code` ``)
- Emphasis (`*em*`, `_em_`) and strong (`**strong**`, `__strong__`)
- Links (`[text](url "title")`) and reference links
- Images (`![alt](url)`)
- Raw HTML blocks

## Usage

```csharp
using var stream = new MemoryStream(Encoding.UTF8.GetBytes(markdown));
var parser = new MarkdownParser(stream);
var document = parser.Parse();

var renderer = new MarkdownHtmlRenderer();
var html = renderer.Render(document);
```

## Design Goals

The parser is **allocation-conscious**: it uses a sliding 4 KB buffer and
avoids materialising the full source string until Phase 2 inline parsing.

> "A strong foundation for scalable .NET applications."
> — Femur tagline

---

### Setext Heading
------------------

The renderer escapes HTML-special characters in all text content:
`<script>alert('xss')</script>` becomes
`&lt;script&gt;alert(&#39;xss&#39;)&lt;/script&gt;`.

Hard line break →  
Next line after hard break.

Soft line break:
Continues on the same rendered line.

## Indented Code Block

    public sealed class MarkdownParser : StreamParser<MarkdownDocumentNode>
    {
        public MarkdownDocumentNode Parse() { /* ... */ }
    }

## Block Quote

> Block quotes can contain **inline markup** and even nested structure.
> They continue as long as each line starts with `>`.
>
> A blank `>` line keeps the quote open.

## Mixed List

1. First ordered item with `code`
2. Second item — contains a [link](https://femur.dev)
3. Third item

- Bullet one: _italic text_
- Bullet two: **bold text**
- Bullet three: `code span`

## Reference Links

This paragraph uses a [reference link][femur] defined below.

[femur]: https://femur.dev "Femur Documentation"

## Image

![Femur logo](https://femur.dev/logo.png "The Femur logo")

---

*End of medium sample.*


## Document Section 4

# Femur Markdown Parser

A _streaming_ CommonMark 0.31.2 parser built on top of **Femur.Parsing**.
It processes documents in two phases:

1. **Block structure** — line-by-line pass that identifies headings, lists,
   code blocks, block quotes, and paragraphs.
2. **Inline structure** — character-by-character pass that handles emphasis,
   links, images, and code spans.

## Features

Femur supports every construct defined in the core CommonMark specification:

- ATX headings (`# H1` through `###### H6`)
- Setext headings (underlined with `=` or `-`)
- Thematic breaks (`---`, `***`, `___`)
- Fenced code blocks (triple backtick or `~~~`)
- Indented code blocks (4-space indent)
- Block quotes (`>`)
- Ordered and unordered lists
- Hard and soft line breaks
- Inline code spans (`` `code` ``)
- Emphasis (`*em*`, `_em_`) and strong (`**strong**`, `__strong__`)
- Links (`[text](url "title")`) and reference links
- Images (`![alt](url)`)
- Raw HTML blocks

## Usage

```csharp
using var stream = new MemoryStream(Encoding.UTF8.GetBytes(markdown));
var parser = new MarkdownParser(stream);
var document = parser.Parse();

var renderer = new MarkdownHtmlRenderer();
var html = renderer.Render(document);
```

## Design Goals

The parser is **allocation-conscious**: it uses a sliding 4 KB buffer and
avoids materialising the full source string until Phase 2 inline parsing.

> "A strong foundation for scalable .NET applications."
> — Femur tagline

---

### Setext Heading
------------------

The renderer escapes HTML-special characters in all text content:
`<script>alert('xss')</script>` becomes
`&lt;script&gt;alert(&#39;xss&#39;)&lt;/script&gt;`.

Hard line break →  
Next line after hard break.

Soft line break:
Continues on the same rendered line.

## Indented Code Block

    public sealed class MarkdownParser : StreamParser<MarkdownDocumentNode>
    {
        public MarkdownDocumentNode Parse() { /* ... */ }
    }

## Block Quote

> Block quotes can contain **inline markup** and even nested structure.
> They continue as long as each line starts with `>`.
>
> A blank `>` line keeps the quote open.

## Mixed List

1. First ordered item with `code`
2. Second item — contains a [link](https://femur.dev)
3. Third item

- Bullet one: _italic text_
- Bullet two: **bold text**
- Bullet three: `code span`

## Reference Links

This paragraph uses a [reference link][femur] defined below.

[femur]: https://femur.dev "Femur Documentation"

## Image

![Femur logo](https://femur.dev/logo.png "The Femur logo")

---

*End of medium sample.*


## Document Section 5

# Femur Markdown Parser

A _streaming_ CommonMark 0.31.2 parser built on top of **Femur.Parsing**.
It processes documents in two phases:

1. **Block structure** — line-by-line pass that identifies headings, lists,
   code blocks, block quotes, and paragraphs.
2. **Inline structure** — character-by-character pass that handles emphasis,
   links, images, and code spans.

## Features

Femur supports every construct defined in the core CommonMark specification:

- ATX headings (`# H1` through `###### H6`)
- Setext headings (underlined with `=` or `-`)
- Thematic breaks (`---`, `***`, `___`)
- Fenced code blocks (triple backtick or `~~~`)
- Indented code blocks (4-space indent)
- Block quotes (`>`)
- Ordered and unordered lists
- Hard and soft line breaks
- Inline code spans (`` `code` ``)
- Emphasis (`*em*`, `_em_`) and strong (`**strong**`, `__strong__`)
- Links (`[text](url "title")`) and reference links
- Images (`![alt](url)`)
- Raw HTML blocks

## Usage

```csharp
using var stream = new MemoryStream(Encoding.UTF8.GetBytes(markdown));
var parser = new MarkdownParser(stream);
var document = parser.Parse();

var renderer = new MarkdownHtmlRenderer();
var html = renderer.Render(document);
```

## Design Goals

The parser is **allocation-conscious**: it uses a sliding 4 KB buffer and
avoids materialising the full source string until Phase 2 inline parsing.

> "A strong foundation for scalable .NET applications."
> — Femur tagline

---

### Setext Heading
------------------

The renderer escapes HTML-special characters in all text content:
`<script>alert('xss')</script>` becomes
`&lt;script&gt;alert(&#39;xss&#39;)&lt;/script&gt;`.

Hard line break →  
Next line after hard break.

Soft line break:
Continues on the same rendered line.

## Indented Code Block

    public sealed class MarkdownParser : StreamParser<MarkdownDocumentNode>
    {
        public MarkdownDocumentNode Parse() { /* ... */ }
    }

## Block Quote

> Block quotes can contain **inline markup** and even nested structure.
> They continue as long as each line starts with `>`.
>
> A blank `>` line keeps the quote open.

## Mixed List

1. First ordered item with `code`
2. Second item — contains a [link](https://femur.dev)
3. Third item

- Bullet one: _italic text_
- Bullet two: **bold text**
- Bullet three: `code span`

## Reference Links

This paragraph uses a [reference link][femur] defined below.

[femur]: https://femur.dev "Femur Documentation"

## Image

![Femur logo](https://femur.dev/logo.png "The Femur logo")

---

*End of medium sample.*


## Document Section 6

# Femur Markdown Parser

A _streaming_ CommonMark 0.31.2 parser built on top of **Femur.Parsing**.
It processes documents in two phases:

1. **Block structure** — line-by-line pass that identifies headings, lists,
   code blocks, block quotes, and paragraphs.
2. **Inline structure** — character-by-character pass that handles emphasis,
   links, images, and code spans.

## Features

Femur supports every construct defined in the core CommonMark specification:

- ATX headings (`# H1` through `###### H6`)
- Setext headings (underlined with `=` or `-`)
- Thematic breaks (`---`, `***`, `___`)
- Fenced code blocks (triple backtick or `~~~`)
- Indented code blocks (4-space indent)
- Block quotes (`>`)
- Ordered and unordered lists
- Hard and soft line breaks
- Inline code spans (`` `code` ``)
- Emphasis (`*em*`, `_em_`) and strong (`**strong**`, `__strong__`)
- Links (`[text](url "title")`) and reference links
- Images (`![alt](url)`)
- Raw HTML blocks

## Usage

```csharp
using var stream = new MemoryStream(Encoding.UTF8.GetBytes(markdown));
var parser = new MarkdownParser(stream);
var document = parser.Parse();

var renderer = new MarkdownHtmlRenderer();
var html = renderer.Render(document);
```

## Design Goals

The parser is **allocation-conscious**: it uses a sliding 4 KB buffer and
avoids materialising the full source string until Phase 2 inline parsing.

> "A strong foundation for scalable .NET applications."
> — Femur tagline

---

### Setext Heading
------------------

The renderer escapes HTML-special characters in all text content:
`<script>alert('xss')</script>` becomes
`&lt;script&gt;alert(&#39;xss&#39;)&lt;/script&gt;`.

Hard line break →  
Next line after hard break.

Soft line break:
Continues on the same rendered line.

## Indented Code Block

    public sealed class MarkdownParser : StreamParser<MarkdownDocumentNode>
    {
        public MarkdownDocumentNode Parse() { /* ... */ }
    }

## Block Quote

> Block quotes can contain **inline markup** and even nested structure.
> They continue as long as each line starts with `>`.
>
> A blank `>` line keeps the quote open.

## Mixed List

1. First ordered item with `code`
2. Second item — contains a [link](https://femur.dev)
3. Third item

- Bullet one: _italic text_
- Bullet two: **bold text**
- Bullet three: `code span`

## Reference Links

This paragraph uses a [reference link][femur] defined below.

[femur]: https://femur.dev "Femur Documentation"

## Image

![Femur logo](https://femur.dev/logo.png "The Femur logo")

---

*End of medium sample.*


## Document Section 7

# Femur Markdown Parser

A _streaming_ CommonMark 0.31.2 parser built on top of **Femur.Parsing**.
It processes documents in two phases:

1. **Block structure** — line-by-line pass that identifies headings, lists,
   code blocks, block quotes, and paragraphs.
2. **Inline structure** — character-by-character pass that handles emphasis,
   links, images, and code spans.

## Features

Femur supports every construct defined in the core CommonMark specification:

- ATX headings (`# H1` through `###### H6`)
- Setext headings (underlined with `=` or `-`)
- Thematic breaks (`---`, `***`, `___`)
- Fenced code blocks (triple backtick or `~~~`)
- Indented code blocks (4-space indent)
- Block quotes (`>`)
- Ordered and unordered lists
- Hard and soft line breaks
- Inline code spans (`` `code` ``)
- Emphasis (`*em*`, `_em_`) and strong (`**strong**`, `__strong__`)
- Links (`[text](url "title")`) and reference links
- Images (`![alt](url)`)
- Raw HTML blocks

## Usage

```csharp
using var stream = new MemoryStream(Encoding.UTF8.GetBytes(markdown));
var parser = new MarkdownParser(stream);
var document = parser.Parse();

var renderer = new MarkdownHtmlRenderer();
var html = renderer.Render(document);
```

## Design Goals

The parser is **allocation-conscious**: it uses a sliding 4 KB buffer and
avoids materialising the full source string until Phase 2 inline parsing.

> "A strong foundation for scalable .NET applications."
> — Femur tagline

---

### Setext Heading
------------------

The renderer escapes HTML-special characters in all text content:
`<script>alert('xss')</script>` becomes
`&lt;script&gt;alert(&#39;xss&#39;)&lt;/script&gt;`.

Hard line break →  
Next line after hard break.

Soft line break:
Continues on the same rendered line.

## Indented Code Block

    public sealed class MarkdownParser : StreamParser<MarkdownDocumentNode>
    {
        public MarkdownDocumentNode Parse() { /* ... */ }
    }

## Block Quote

> Block quotes can contain **inline markup** and even nested structure.
> They continue as long as each line starts with `>`.
>
> A blank `>` line keeps the quote open.

## Mixed List

1. First ordered item with `code`
2. Second item — contains a [link](https://femur.dev)
3. Third item

- Bullet one: _italic text_
- Bullet two: **bold text**
- Bullet three: `code span`

## Reference Links

This paragraph uses a [reference link][femur] defined below.

[femur]: https://femur.dev "Femur Documentation"

## Image

![Femur logo](https://femur.dev/logo.png "The Femur logo")

---

*End of medium sample.*


## Document Section 8

# Femur Markdown Parser

A _streaming_ CommonMark 0.31.2 parser built on top of **Femur.Parsing**.
It processes documents in two phases:

1. **Block structure** — line-by-line pass that identifies headings, lists,
   code blocks, block quotes, and paragraphs.
2. **Inline structure** — character-by-character pass that handles emphasis,
   links, images, and code spans.

## Features

Femur supports every construct defined in the core CommonMark specification:

- ATX headings (`# H1` through `###### H6`)
- Setext headings (underlined with `=` or `-`)
- Thematic breaks (`---`, `***`, `___`)
- Fenced code blocks (triple backtick or `~~~`)
- Indented code blocks (4-space indent)
- Block quotes (`>`)
- Ordered and unordered lists
- Hard and soft line breaks
- Inline code spans (`` `code` ``)
- Emphasis (`*em*`, `_em_`) and strong (`**strong**`, `__strong__`)
- Links (`[text](url "title")`) and reference links
- Images (`![alt](url)`)
- Raw HTML blocks

## Usage

```csharp
using var stream = new MemoryStream(Encoding.UTF8.GetBytes(markdown));
var parser = new MarkdownParser(stream);
var document = parser.Parse();

var renderer = new MarkdownHtmlRenderer();
var html = renderer.Render(document);
```

## Design Goals

The parser is **allocation-conscious**: it uses a sliding 4 KB buffer and
avoids materialising the full source string until Phase 2 inline parsing.

> "A strong foundation for scalable .NET applications."
> — Femur tagline

---

### Setext Heading
------------------

The renderer escapes HTML-special characters in all text content:
`<script>alert('xss')</script>` becomes
`&lt;script&gt;alert(&#39;xss&#39;)&lt;/script&gt;`.

Hard line break →  
Next line after hard break.

Soft line break:
Continues on the same rendered line.

## Indented Code Block

    public sealed class MarkdownParser : StreamParser<MarkdownDocumentNode>
    {
        public MarkdownDocumentNode Parse() { /* ... */ }
    }

## Block Quote

> Block quotes can contain **inline markup** and even nested structure.
> They continue as long as each line starts with `>`.
>
> A blank `>` line keeps the quote open.

## Mixed List

1. First ordered item with `code`
2. Second item — contains a [link](https://femur.dev)
3. Third item

- Bullet one: _italic text_
- Bullet two: **bold text**
- Bullet three: `code span`

## Reference Links

This paragraph uses a [reference link][femur] defined below.

[femur]: https://femur.dev "Femur Documentation"

## Image

![Femur logo](https://femur.dev/logo.png "The Femur logo")

---

*End of medium sample.*


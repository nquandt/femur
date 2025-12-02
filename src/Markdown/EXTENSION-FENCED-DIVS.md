# Extension: Fenced Divs

## Overview

Fenced divs are a generic block container extension that allows wrapping content within semantic div-like containers. This extension implements the Pandoc `fenced_divs` syntax, enabling more flexible document structure while remaining readable in plain text.

## Syntax

### Basic Structure

A fenced div is delimited by lines containing three or more consecutive colons (`:::`). The opening fence must be followed by attributes (a closing fence without attributes is simply `::`):

```markdown
::: {#id .class1 .class2}
Content goes here
:::
```

### Attributes

Fenced div attributes follow Pandoc's attribute syntax and can include:

- **ID**: Specified with `#` prefix (e.g., `#special`)
- **Classes**: Specified with `.` prefix (e.g., `.sidebar .highlight`)  
- **Key-Value Pairs**: Standard key=value format (e.g., `lang=csharp`)

#### Examples

```markdown
::: {#special .sidebar}
This div has an ID and a class.
:::

::: {.warning}
Important notice
:::

::: {#code lang="csharp"}
Code example
:::
```

### Nesting

Fenced divs can be nested. Outer divs typically use longer fence markers for visual clarity, though any opening fence with attributes starts a new div:

```markdown
::::: {#outer .container}
This is the outer div.

::: {#inner .nested}
This is a nested div.
:::

Back to outer div.
:::::
```

Closing fences can have any number of colons (3+) and do not need to match the opening fence length:

```markdown
:::::::: {#parent}
Outer content

::: {#child}
Inner content
:::

More outer content
::::
```

## Use Cases

### Creating Reusable Containers

```markdown
::: {.callout}
**Note:** This is an important note that should stand out.
:::
```

### Code Examples with Metadata

```markdown
::: {#example-1 .code-example lang="csharp"}
```csharp
public class HelloWorld
{
    public static void Main()
    {
        Console.WriteLine("Hello, World!");
    }
}
```
:::
```

### Semantic Sections

```markdown
::: {.intro}
# Introduction

This section introduces the topic...
:::

::: {.content}
# Main Content

Detailed content goes here...
:::
```

### Complex Nested Structures

```markdown
::::: {.article}

::: {.header}
# Article Title
*By Author Name*
:::

::: {.body}
## Section 1
Content of section 1

## Section 2  
Content of section 2
:::

::: {.footer}
Published: 2024-12-02
:::

:::::
```

## Block Content

Fenced divs can contain any block-level content, including:

- Paragraphs
- Headings
- Lists (ordered and unordered)
- Code blocks (both fenced and indented)
- Block quotes
- Nested divs
- Thematic breaks
- HTML blocks

```markdown
::: {.section}
# Heading

Paragraph text.

- List item 1
- List item 2

> A block quote

```code
code block
```
:::
```

## Parsed Attributes

When a fenced div is parsed, the attributes are broken down into structured components:

- `Attributes`: The raw attribute string as written
- `ParsedAttributes`: Structured representation containing:
  - `Id`: The identifier (if specified with `#`)
  - `Classes`: List of CSS classes
  - `KeyValueAttributes`: Dictionary of custom key-value pairs

### Accessing Parsed Attributes

```csharp
var div = document.Children.OfType<FencedDivNode>().First();

// Raw attributes
string raw = div.Attributes; // "{#special .sidebar}"

// Parsed attributes  
string? id = div.ParsedAttributes.Id; // "special"
var classes = div.ParsedAttributes.Classes; // ["sidebar"]
var kvPairs = div.ParsedAttributes.KeyValueAttributes; // {}
```

## Renderer Considerations

When rendering fenced divs, renderers have several options:

1. **HTML**: Generate `<div>` elements with corresponding classes and attributes
2. **Markdown**: Preserve the original fenced div syntax
3. **Custom**: Use the parsed attributes for custom formatting logic
4. **Semantic**: Generate semantic HTML5 elements based on classes (e.g., `<section>`, `<article>`)

### Example HTML Output

```markdown
::: {#sidebar .highlight .important lang="csharp"}
Content
:::
```

Could render as:

```html
<div id="sidebar" class="highlight important" data-lang="csharp">
  <p>Content</p>
</div>
```

## Closing Fence Rules

- Closing fences require at least 3 colons
- Closing fences must be empty after the colons (no attributes)
- Closing fence colon count does NOT need to match opening fence
- Additional colons after the required 3 are allowed (for visual hierarchy)

```markdown
::: {.outer}
Content
:::  <- 3 colons (minimum)

:::: {.middle}
Content  
:::::  <- 5 colons (visual clarity, not required)

:::::: {.inner}
Content
:::  <- 3 colons (still valid)
```

## Differences from Pandoc

This implementation follows Pandoc's fenced_divs syntax closely with these notes:

- Attributes must be present on opening fences (no lazy divs)
- Attribute parsing supports standard Pandoc format: `{#id .class key=value}`
- Closing fence colons are not strictly required to match opening (Pandoc allows matching closing markers)

## Notes

- Fenced divs are parsed before code blocks, so `::: ` will not be treated as a code fence
- Empty divs are allowed if they have valid opening and closing fences
- The rendering of fenced divs depends on the output format and renderer implementation
- When used in block quote or list contexts, indentation rules still apply

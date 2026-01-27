# SimpleTemplates Example Project

This project demonstrates a complete CHTML template project structure with various features.

## Project Structure

```
Templates/
├── global.chtml              # Global props definition
├── components/               # Reusable components
│   ├── header/
│   ├── button/
│   ├── card/
│   ├── blog-post/
│   ├── layout/
│   ├── alert/
│   └── container/
└── pages/                    # Page templates
    ├── index.chtml          # Home page (/)
    ├── about/
    ├── blog/
    │   ├── index.chtml      # Blog listing (/blog)
    │   └── [slug]/         # Dynamic blog post (/blog/{slug})
    ├── products/
    │   └── [category]/
    │       └── [id]/        # Nested dynamic routes (/products/{category}/{id})
    └── contact/
```

## Features Demonstrated

### 1. Global Props (`global.chtml`)
Defines properties available to all templates:
- `Language`: Site language
- `SiteName`: Site name
- `Theme`: Current theme

### 2. Components with Props
- **Header**: Simple props (`Title`, `ShowNavigation`)
- **Button**: Props with nullable types (`Href`) and computed props (`ButtonClass`)
- **Card**: Props with RenderPipe for children
- **BlogPost**: Props with DateTime and RenderPipe
- **Alert**: Computed props example
- **Layout**: Full page layout component

### 3. Pages with Routes
- **Static routes**: `/`, `/about`, `/blog`, `/contact`
- **Dynamic routes**: `/blog/[slug]`, `/products/[category]/[id]`
- Route parameters are automatically extracted from file paths

### 4. Front Matter Features
- **Props**: Input properties for components/pages
- **ComputedProps**: Properties computed from input props (requires code-beside implementation)

## Usage

1. Build the project:
   ```bash
   dotnet build
   ```

2. Templates are automatically generated to `.generated/` folder before compilation

3. Generated classes are in `Templates.Generated` namespace:
   - Components: `Templates.Generated.Components.*`
   - Pages: `Templates.Generated.Pages.*`
   - Global Props: `Templates.Generated.GlobalProps`

## Example Component Usage

```csharp
// Render a header component
await Templates.Generated.Components.Header.RenderAsync(
    renderContext,
    new Templates.Generated.Components.HeaderProps 
    { 
        Title = "My Site",
        ShowNavigation = true
    }
);
```

## Example Page Usage

```csharp
// Render the home page
await Templates.Generated.Pages.Index.RenderAsync(
    renderContext,
    new Templates.Generated.Pages.IndexProps 
    { 
        WelcomeMessage = "Welcome to our site!"
    }
);
```

## Notes

- Components with `ComputedProps` require a partial class implementation in a `.partial.cs` file
- Route parameters from bracket notation (`[slug]`) are automatically added as props
- Global props are available via `globalProps` in the render context




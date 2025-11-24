# Femur.Parsers.Abstractions

Core abstractions and base classes for the Femur parser family.

## Overview

This package provides the foundational abstractions and infrastructure for implementing streaming parsers in the Femur parser library. It includes:

- **StreamParser<TDocument>**: Base class for all streaming parsers using the Template Method pattern
- **Node hierarchy**: Abstract classes for building Abstract Syntax Trees (AST)
- **Buffer management**: Efficient streaming with sliding buffers and position tracking
- **Common utilities**: Source location tracking and parsing helpers

## Key Classes

### StreamParser<TDocument>

Base class for all streaming parsers. Provides:

- **Buffer Management**: Handles reading chunks from streams efficiently (default 4KB)
- **Position Tracking**: Maintains both buffer-relative and absolute byte positions
- **Template Method Pattern**: Defines parsing algorithm that subclasses customize
- **Resource Management**: Proper disposal of stream and buffer resources

#### Abstract Methods (Implemented by Subclasses)

```csharp
protected abstract TDocument CreateDocument();
protected abstract void InitializeParsing();
protected abstract void ProcessCharacter(char character);
protected abstract void Cleanup();
```

### Node Hierarchy

- **Node**: Base class for all AST nodes, provides `SourceLocation` tracking
- **NodeType**: Enumeration of possible node types
- **SourceLocation**: Tracks start position and length of nodes in source

## Architecture

### Parsing Flow

1. `Parse(stream)` - Entry point, creates stream reader
2. `CreateDocument()` - Creates root document node
3. `InitializeParsing()` - Initializes parser state
4. **Main loop** - For each character:
   - `ReadMore()` - Ensures buffer has data
   - `ProcessCharacter()` - Routes to appropriate handler
5. `Cleanup()` - Processes any remaining content and cleans up resources

### Buffer Management

The parser uses a sliding window approach for efficient memory usage:

- Reads stream in chunks (default 4KB)
- Tracks position within current buffer
- Tracks total characters read (absolute position)
- Allows subclasses to look ahead in buffer

## Usage

This is primarily used as a base package by other Femur parsers:

- `Femur.Parsers.HtmlParser` - HTML 2.0 parser
- `Femur.Parsers.XmlParser` - XML parser
- `Femur.Parsers.ChtmlParser` - Component HTML parser
- `Femur.Parsers.Markdown` - CommonMark 0.31.2 Markdown parser

## Features

- ✅ Efficient streaming with minimal memory overhead
- ✅ Accurate source location tracking for all nodes
- ✅ Template Method pattern for extensibility
- ✅ UTF-8 text support
- ✅ Proper resource disposal

using Femur.Parsing.Nodes;

namespace Femur.Parsing;

/// <summary>
/// Callback delegate invoked when a new node is created during parsing.
/// Allows consumers to track and aggregate node data without re-walking the AST.
/// </summary>
/// <param name="node">The newly created node</param>
public delegate void NodeCreatedCallback(Node node);

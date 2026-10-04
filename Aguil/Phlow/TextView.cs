using Aguil.Editor;

namespace Aguil.Phlow;

public class TextView(string text, string? grammar = null) : AlPhlowView(ReadOnlyText.Create(text, grammar));
using System.Numerics;
using Avalonia.Controls;

namespace Aguil.Phlow;

public abstract class AlPhlowView(Control content)
{
    public string Title { get; set; } = "";
    public BigInteger Priority { get; set; } = 100;
    public Control Content { get; } = content;
}
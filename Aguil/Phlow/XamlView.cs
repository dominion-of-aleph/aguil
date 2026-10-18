using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Aguil.Phlow;

public sealed class XamlView(string content) : AlPhlowView(Load(content))
{
    public static Control Load(string content)
    {
        return AvaloniaRuntimeXamlLoader.Parse<ContentControl>(
            $"""
             <ContentControl xmlns="https://github.com/avaloniaui"
                             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                             xmlns:al="using:Aguil.Bindings">
                 {content}
             </ContentControl>
             """, typeof(XamlView).Assembly);
    }
}
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Aguil.Phlow;

public static class XamlView
{
    public static Control Load(string content, object? data = null)
    {
        var host = AvaloniaRuntimeXamlLoader.Parse<ContentControl>(
            $$"""
              <ContentControl xmlns="https://github.com/avaloniaui"
                              xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                              xmlns:al="using:Aguil.Bindings"
                              xmlns:phlow="using:Aguil.Phlow"
                              xmlns:editor="clr-namespace:Aguil.Editor;assembly=Aguil"
                              xmlns:keys="clr-namespace:Aguil.Editor;assembly=Aguil.Editor"
                              xmlns:ae="clr-namespace:AvaloniaEdit;assembly=AvaloniaEdit">
                  <ContentControl.Styles>
                      <Style Selector="ae|TextEditor">
                          <Setter Property="VerticalScrollBarVisibility" Value="Auto" />
                          <Setter Property="HorizontalScrollBarVisibility" Value="Auto" />
                          <Setter Property="editor:Code.Keymap" Value="{x:Static keys:Keymaps.defaults}" />
                      </Style>
                  </ContentControl.Styles>
                  {{content}}
              </ContentControl>
              """, typeof(XamlView).Assembly);
        if (data is not null) host.DataContext = data;
        return host;
    }
}

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Aguil.Elements;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Styling;
using Value = Aguil.Core.AlValues.AlValue;

namespace Aguil.Phlow;

public class ColumnedList(IEnumerable items, IEnumerable<TableViewColumn> columns) : AlPhlowView(Layout(items, columns))
{
    private static TableView Layout(IEnumerable items, IEnumerable<TableViewColumn> columns)
    {
        var table = new TableView { ItemsSource = items };
        table.Columns.AddRange(columns);
        table.Styles.Add(new Style(s => s.OfType<TableViewRow>())
        {
            Setters =
            {
                new Setter(InspectionTarget.TargetProperty, new Binding(".")
                {
                    Converter = new FuncValueConverter<Value, Value?>(ElementBuilder.Target)
                }),
                new Setter(TemplatedControl.PaddingProperty, new Thickness(0)),
                new Setter(Control.MinHeightProperty, 0d)
            }
        });
        table.Styles.Add(new Style(s => s.OfType<TableViewCell>())
        {
            Setters = { new Setter(TemplatedControl.PaddingProperty, new Thickness(0)) }
        });
        if (table.Columns.All(column => column.Header is null))
            table.Styles.Add(new Style(s => s.OfType<TableView>().Template().OfType<TableViewColumnHeadersPresenter>())
            {
                Setters = { new Setter(Visual.IsVisibleProperty, false) }
            });
        return table;
    }
}
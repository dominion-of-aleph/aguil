using Aguil.Core;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Aguil.Bindings;

public sealed partial class SlotSource : ObservableObject
{
    [ObservableProperty] public partial Slot? Slot { get; set; }
}
using Aguil.Core;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Aguil.Bindings;

public sealed partial class ReferenceSource : ObservableObject
{
    [ObservableProperty] public partial AlMutableObjectSlot? Reference { get; set; }
}
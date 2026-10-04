using System.Collections.Generic;
using Aguil.Views;
using Microsoft.FSharp.Collections;
using Value = Aguil.Core.AlValues.AlValue;

namespace Aguil.Phlow;

public class EmbeddedInspector(IEnumerable<FSharpMap<Value, Value>> views)
    : AlPhlowView(new Inspector { Views = views });

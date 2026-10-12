using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aguil.Core;
using Aguil.Core.MCP;
using Avalonia.Controls;

namespace Aguil.Phlow;

public class CSharpForward(Control content) : AlPhlowView(content)
{
    // Exported targets remain valid for this application session.
    private static readonly Dictionary<string, AlPhlowView> References = [];

    public static async Task<AlValues.AlValue> ToAl(AlPhlowView view, AlMcpClient al)
    {
        // round trip to properly encode the string
        var title = AlValues.AlValue.Flat(AlValues.AlValue.NewAlText(view.Title));
        var target = Guid.NewGuid().ToString("N");
        var result = await al.QueryAl(FormattableString.Invariant($$"""
                                                                    new(:phlow_csharp_forward, %{view: "aguil_native", title: {{title}},
                                                                        priority: {{view.Priority}}, target: "{{target}}"}, content)
                                                                    """), null);
        References.Add(target, view);
        return Evaluation.binding("content", result);
    }

    public static AlPhlowView Resolve(string target)
    {
        return References[target];
    }
}
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

    public static async Task<AlValues.AlValue> ToAl(AlPhlowView view, AlMcpClient al, string branch)
    {
        var target = Guid.NewGuid().ToString("N");
        var result = await al.QueryAlWith(
            "new phlow_csharp_forward #{view => \"aguil_native\", title => Title, priority => Priority, target => Target} Content.",
            branch,
            new Dictionary<string, AlValues.AlValue>
            {
                ["Title"] = AlValues.AlValue.NewAlText(view.Title),
                ["Priority"] = AlValues.AlValue.NewAlInteger(view.Priority),
                ["Target"] = AlValues.AlValue.NewAlText(target)
            });
        References.Add(target, view);
        var content = Evaluation.binding("Content", result);
        await al.ReleaseContext(result.Context);
        return content;
    }

    public static AlPhlowView Resolve(string target)
    {
        return References[target];
    }
}
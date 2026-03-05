using UnityEditor;
using UnityEngine;

[ToolbarSectionAttribute("None")]
public class NoneToolbar : IEditorToolbar
{
    public bool ShouldShow()
    {
        return true;
    }

    public void OnGUI()
    {
        // Empty menu to show clear space
    }
}


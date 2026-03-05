using UnityEditor;
using UnityEngine;

namespace EditorToolbar
{
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
}

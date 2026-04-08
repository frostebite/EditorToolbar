#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace EditorToolbar
{
    /// <summary>
    /// Toolbar GUI handler registration for custom toolbar elements.
    /// </summary>
    public static class ToolbarExtenderIntegration
    {
        // These handlers are populated by GenericToolbar and used by the toolbar elements
        public static readonly List<Action> LeftToolbarGUI = new List<Action>();
        public static readonly List<Action> RightToolbarGUI = new List<Action>();
    }
}
#endif

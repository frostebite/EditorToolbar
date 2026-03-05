using System;

namespace EditorToolbar
{
    /// <summary>
    /// Marks a static class as a toolbar workspace provider.
    /// The class should have static methods:
    /// - DrawWorkspaceUI() - Called to render the left-side workspace UI (profile, scene, etc.)
    /// - DrawRuntimeStatus() - Called during play mode to render runtime status
    ///
    /// This attribute enables reflection-based discovery, allowing the generic toolbar system
    /// to find and use project-specific workspace providers without direct references.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ToolbarWorkspaceProviderAttribute : Attribute
    {
        /// <summary>
        /// Priority for ordering when multiple providers exist. Higher priority = called first.
        /// Default is 0.
        /// </summary>
        public int Priority { get; set; } = 0;
    }
}

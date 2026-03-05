using System;

namespace EditorToolbar
{
    /// <summary>
    /// Marks a static class as a framework detector for toolbar sections.
    /// The class should have a static method:
    /// - DetectFramework(Type type) returning (string Framework, string Module)
    ///
    /// Framework detectors are discovered via reflection and sorted by Priority.
    /// Higher priority detectors are tried first. If a detector returns a non-null/non-empty
    /// framework, that result is used. Otherwise, the next detector is tried.
    ///
    /// This attribute enables project-specific framework detection without modifying
    /// the generic toolbar system.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class FrameworkDetectorAttribute : Attribute
    {
        /// <summary>
        /// Priority for ordering detectors. Higher priority = tried first.
        /// Default is 0. Use positive values for project-specific detectors
        /// that should override the default detector.
        /// </summary>
        public int Priority { get; set; } = 0;
    }
}

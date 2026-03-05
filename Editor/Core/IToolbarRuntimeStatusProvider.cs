namespace EditorToolbar
{
    /// <summary>
    /// Optional interface for toolbar sections to provide runtime status information
    /// that will be displayed in the main toolbar area during play mode.
    /// </summary>
    public interface IToolbarRuntimeStatusProvider
    {
        /// <summary>
        /// Get the runtime status string to display in the main toolbar.
        /// Return null or empty string if no status should be displayed.
        /// </summary>
        string GetRuntimeStatus();
    }
}

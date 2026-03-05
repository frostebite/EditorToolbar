namespace EditorToolbar
{
    public interface IEditorToolbar
    {
        void OnGUI();

        /// <summary>
        /// Determines if this section should be shown in the toolbar.
        /// Useful for conditionally showing sections based on runtime state or preferences.
        /// </summary>
        bool ShouldShow() => true;
    }
}

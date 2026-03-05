using System;

namespace EditorToolbar
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ToolbarSectionAttribute : Attribute
    {
        public string Name { get; }
        public ToolbarSectionAttribute(string name)
        {
            Name = name;
        }
    }
}

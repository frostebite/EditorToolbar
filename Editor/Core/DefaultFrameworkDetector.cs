using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Default framework detector that uses generic path patterns.
/// Detects frameworks by finding the deepest "Submodules" folder in the asset path
/// and using the next path segment as the framework name. Also checks for Plugins
/// and Shared code patterns.
///
/// This detector has Priority -100 so project-specific detectors can override it.
/// </summary>
[FrameworkDetector(Priority = -100)]
public static class DefaultFrameworkDetector
{
    /// <summary>
    /// Detects framework and module from a type using generic path patterns.
    /// </summary>
    public static (string Framework, string Module) DetectFramework(Type type)
    {
        // Types from the EditorToolbar assembly itself are built-in sections
        if (type.Assembly == typeof(DefaultFrameworkDetector).Assembly)
            return ("Built-In", "");

        // Try to find the script file using AssetDatabase
        try
        {
            var guids = AssetDatabase.FindAssets($"{type.Name} t:MonoScript");
            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    var result = ExtractFromPath(assetPath);
                    if (!string.IsNullOrEmpty(result.Framework))
                        return result;
                }
            }
        }
        catch
        {
            // Fall through to namespace-based detection
        }

        // Fallback: try to extract from namespace
        if (!string.IsNullOrEmpty(type.Namespace))
        {
            // Use first segment of namespace as framework
            var parts = type.Namespace.Split('.');
            if (parts.Length > 0)
            {
                return (parts[0].ToLowerInvariant(), "");
            }
        }

        // Fallback: try assembly name
        try
        {
            var assemblyName = type.Assembly.GetName().Name;
            // Remove common suffixes
            assemblyName = assemblyName.Replace(".Editor", "").Replace(".Runtime", "");
            if (!string.IsNullOrEmpty(assemblyName))
            {
                return (assemblyName.ToLowerInvariant(), "");
            }
        }
        catch
        {
            // Fall through to default
        }

        return ("shared", "");
    }

    private static (string Framework, string Module) ExtractFromPath(string assetPath)
    {
        assetPath = assetPath.Replace('\\', '/');

        // Find the deepest "Submodules" folder and use the next segment as the framework name.
        // This works for any project structure: _Game/Submodules/X, _Engine/Submodules/X,
        // MyProject/Submodules/X, or any other path containing a Submodules directory.
        int deepestSubmodulesIndex = -1;
        const string submodulesToken = "/Submodules/";
        int searchFrom = 0;
        while (true)
        {
            var idx = assetPath.IndexOf(submodulesToken, searchFrom, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) break;
            deepestSubmodulesIndex = idx;
            searchFrom = idx + submodulesToken.Length;
        }

        if (deepestSubmodulesIndex >= 0)
        {
            var afterSubmodules = assetPath.Substring(deepestSubmodulesIndex + submodulesToken.Length);
            var moduleName = ExtractFirstPathSegment(afterSubmodules);
            if (!string.IsNullOrEmpty(moduleName))
            {
                return (moduleName.ToLowerInvariant(), "");
            }
        }

        // Check for Shared code
        if (assetPath.Contains("/Shared/", StringComparison.OrdinalIgnoreCase))
        {
            return ("shared", "");
        }

        // Check for Plugins/{PluginName}/
        var pluginsIndex = assetPath.IndexOf("/Plugins/", StringComparison.OrdinalIgnoreCase);
        if (pluginsIndex >= 0)
        {
            var afterPlugins = assetPath.Substring(pluginsIndex + "/Plugins/".Length);
            var pluginName = ExtractFirstPathSegment(afterPlugins);
            if (!string.IsNullOrEmpty(pluginName))
            {
                return ("plugin", pluginName.ToLowerInvariant());
            }
        }

        return ("", ""); // Empty = not detected, try next detector
    }

    private static string ExtractFirstPathSegment(string path)
    {
        if (string.IsNullOrEmpty(path)) return "";

        var slashIndex = path.IndexOf('/');
        if (slashIndex > 0)
        {
            return path.Substring(0, slashIndex);
        }
        return path;
    }
}

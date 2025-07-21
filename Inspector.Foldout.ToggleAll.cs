using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// TODO : Redo the logs
public static class ProjectBrowserToggleAll
{
    public const bool doDebug = true;

    private static readonly System.Type _projectBrowserType = typeof(EditorWindow).Assembly.GetType("UnityEditor.ProjectBrowser");
    private static EditorWindow _projectWindow;
    private static object _folderTree;
    private static object _treeData;

    private static List<int> _idsToExpandOrCollapse;
    private static bool _expandOperation;

    private static StringBuilder _log;

    [MenuItem("Assets/Expand All", priority = 1)]
    private static void ExpandAllFolders() => ExpandOrCollapseSelected(true);

    [MenuItem("Assets/Collapse All", priority = 2)]
    private static void CollapseAllFolders() => ExpandOrCollapseSelected(false);

    private static void ExpandOrCollapseSelected(bool expand)
    {
        _expandOperation = expand;

        if (doDebug)
            _log = new StringBuilder($"=== ProjectBrowser {(expand ? "Expand" : "Collapse")} All Log ===\n");

        var selected = Selection.activeObject;
        if (selected == null)
        {
            Debug.LogWarning("No folder selected.");
            return;
        }

        string assetPath = AssetDatabase.GetAssetPath(selected);
        string guid = AssetDatabase.AssetPathToGUID(assetPath);

        if (doDebug)
        {
            _log.AppendLine($"{Timestamp()} Selected asset path: {assetPath}");
            _log.AppendLine($"{Timestamp()} Selected GUID: {guid}");
        }

        _projectWindow = _projectBrowserType
            .GetField("s_LastInteractedProjectBrowser", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            ?.GetValue(null) as EditorWindow;

        if (_projectWindow == null)
        {
            var windows = Resources.FindObjectsOfTypeAll(_projectBrowserType);
            if (windows.Length > 0)
                _projectWindow = windows[0] as EditorWindow;
        }

        if (_projectWindow == null)
        {
            if (doDebug) _log.AppendLine($"{Timestamp()} ❌ ProjectBrowser not found!");
            DumpLog();
            return;
        }

        if (doDebug) _log.AppendLine($"{Timestamp()} ✅ ProjectBrowser found: {_projectWindow}");

        _folderTree = null;
        _folderTree ??= _projectWindow.GetType().GetField("m_FolderTree", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(_projectWindow);
        _folderTree ??= _projectWindow.GetType().GetField("m_AssetTree", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(_projectWindow);
        _folderTree ??= _projectWindow.GetType().GetField("m_TreeView", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(_projectWindow);

        if (_folderTree == null)
        {
            if (doDebug) _log.AppendLine($"{Timestamp()} ❌ FolderTree not found!");
            DumpLog();
            return;
        }

        if (doDebug) _log.AppendLine($"{Timestamp()} ✅ FolderTree found: {_folderTree.GetType().Name}");

        _treeData = _folderTree.GetType()
            .GetProperty("data", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            ?.GetValue(_folderTree, null);

        if (_treeData == null)
        {
            if (doDebug) _log.AppendLine($"{Timestamp()} ❌ FolderTree data not found!");
            DumpLog();
            return;
        }

        if (doDebug) _log.AppendLine($"{Timestamp()} ✅ FolderTree data type: {_treeData.GetType()}");
        
        _idsToExpandOrCollapse = new List<int>();
        GatherFolderIDs(assetPath, _idsToExpandOrCollapse);

        if (doDebug)
        {
            _log.AppendLine($"{Timestamp()} IDs to {(expand ? "expand" : "collapse")}: {_idsToExpandOrCollapse.Count}");
            foreach (var id in _idsToExpandOrCollapse)
                _log.AppendLine($"{Timestamp()}   {id} → {AssetDatabase.GetAssetPath(id)}");
        }

        ApplyExpandOrCollapse();
        DumpLog();
    }

    private static void GatherFolderIDs(string folderPath, List<int> idList)
    {
        if (!AssetDatabase.IsValidFolder(folderPath))
            return;

        var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(folderPath);
        if (obj == null) return;

        int id = obj.GetInstanceID();
        if (!idList.Contains(id))
            idList.Add(id);

        string[] subFolders = AssetDatabase.GetSubFolders(folderPath);
        foreach (var sub in subFolders)
            GatherFolderIDs(sub, idList);
    }

    private static void ApplyExpandOrCollapse()
    {
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        var currentIDs = _treeData.GetType().GetMethod("GetExpandedIDs", flags)
            ?.Invoke(_treeData, null) as IList<int>;

        var newIDs = new List<int>(currentIDs);

        if (_expandOperation)
        {
            foreach (var id in _idsToExpandOrCollapse)
            {
                if (!newIDs.Contains(id))
                    newIDs.Add(id);
            }
        }
        else
        {
            foreach (var id in _idsToExpandOrCollapse)
                newIDs.Remove(id);
        }

        _treeData.GetType().GetMethod("SetExpandedIDs", flags)
            ?.Invoke(_treeData, new object[] { newIDs.ToArray() });

        _folderTree.GetType().GetMethod("BuildRootAndRows", flags)?.Invoke(_folderTree, null);
        _projectWindow.Repaint();

        if (doDebug)
        {
            _log.AppendLine($"{Timestamp()} ✅ {( _expandOperation ? "Expand" : "Collapse" )} operation complete.");
        }
    }

    private static void DumpLog()
    {
        if (doDebug && _log != null)
            Debug.Log(_log.ToString());
    }

    private static string Timestamp()
    {
        return $"[{System.DateTime.Now:HH:mm:ss.fff}]";
    }
}
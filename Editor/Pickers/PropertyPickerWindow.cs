using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TweenAnimator.Editor
{
    public class PropertyPickerWindow : EditorWindow
    {
        private Action<TweenPropertyBinding> _onSelected;
        private List<DiscoveredProperty> _all;
        private List<DiscoveredProperty> _filtered;

        // Left column selection
        private string _selectedObject = "";
        private Vector2 _leftScroll;
        private Vector2 _rightScroll;

        public static void Show(Transform root, Action<TweenPropertyBinding> onSelected)
        {
            var w = CreateInstance<PropertyPickerWindow>();
            w.titleContent = new GUIContent("Add Property");
            w._onSelected = onSelected;
            w._all = ComponentPropertyScanner.Scan(root);
            w._filtered = new List<DiscoveredProperty>(w._all);
            w.minSize = new Vector2(500, 350);
            w.ShowUtility();
        }

        private void OnGUI()
        {
            // Gather unique object paths for left column
            var objects = _all
                .Select(p => string.IsNullOrEmpty(p.HierarchyPath) ? "(self)" : p.HierarchyPath)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            GUILayout.BeginHorizontal();

            // ── Left column: object list ──────────────────────────────────────
            GUILayout.BeginVertical(GUILayout.Width(180));
            GUILayout.Label("Object", EditorStyles.boldLabel);
            _leftScroll = GUILayout.BeginScrollView(_leftScroll);

            foreach (var obj in objects)
            {
                bool sel = _selectedObject == obj;
                GUIStyle style = sel ? EditorStyles.selectionRect : EditorStyles.label;

                if (GUILayout.Button(obj, sel ? EditorStyles.boldLabel : EditorStyles.label,
                        GUILayout.ExpandWidth(true)))
                {
                    _selectedObject = sel ? "" : obj;
                    RefreshFilter();
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            // Divider
            EditorGUI.DrawRect(GUILayoutUtility.GetRect(1, position.height), new Color(0.2f, 0.2f, 0.2f, 1f));

            // ── Right column: property list ───────────────────────────────────
            GUILayout.BeginVertical();
            GUILayout.Label("Property", EditorStyles.boldLabel);
            _rightScroll = GUILayout.BeginScrollView(_rightScroll);

            foreach (var prop in _filtered)
            {
                string label = string.IsNullOrEmpty(_selectedObject)
                    ? $"{(string.IsNullOrEmpty(prop.HierarchyPath) ? "(self)" : prop.HierarchyPath)}  /  {prop.ComponentShortName}  ›  {prop.DisplayName}"
                    : $"{prop.ComponentShortName}  ›  {prop.DisplayName}";

                if (GUILayout.Button(label, EditorStyles.label, GUILayout.ExpandWidth(true)))
                    Select(prop);
            }

            if (_filtered.Count == 0)
                GUILayout.Label("No properties found.", EditorStyles.miniLabel);

            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }

        private void RefreshFilter()
        {
            if (string.IsNullOrEmpty(_selectedObject))
            {
                _filtered = new List<DiscoveredProperty>(_all);
                return;
            }

            string matchPath = _selectedObject == "(self)" ? "" : _selectedObject;
            _filtered = _all.Where(p => p.HierarchyPath == matchPath).ToList();
        }

        private void Select(DiscoveredProperty prop)
        {
            var binding = new TweenPropertyBinding
            {
                hierarchyPath = prop.HierarchyPath,
                componentTypeName = prop.ComponentTypeName,
                propertyName = prop.PropertyName,
                axis = PropertyAxis.None
            };
            _onSelected?.Invoke(binding);
            Close();
        }
    }
}
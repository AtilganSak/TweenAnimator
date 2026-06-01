using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TweenAnimator.Editor
{
    public class DiscoveredProperty
    {
        public string       HierarchyPath;
        public string       ComponentTypeName;
        public string       ComponentShortName;
        public string       PropertyName;
        public string       DisplayName;
        public PropertyType ValueType;
        public GameObject   OwnerObject;
    }

    public static class ComponentPropertyScanner
    {
        public static List<DiscoveredProperty> Scan(Transform root)
        {
            var results = new List<DiscoveredProperty>();
            ScanRecursive(root, root, results);
            return results;
        }

        private static void ScanRecursive(Transform root, Transform current, List<DiscoveredProperty> results)
        {
            // AnimationUtility.CalculateTransformPath gives the correct relative path
            string path = AnimationUtility.CalculateTransformPath(current, root);

            foreach (var component in current.GetComponents<Component>())
            {
                if (component == null) continue;
                string typeName = component.GetType().FullName;

                var descriptors = PropertyAccessorRegistry.GetSupportedProperties(typeName);
                foreach (var desc in descriptors)
                {
                    results.Add(new DiscoveredProperty
                    {
                        HierarchyPath      = path,
                        ComponentTypeName  = typeName,
                        ComponentShortName = component.GetType().Name,
                        PropertyName       = desc.PropertyName,
                        DisplayName        = desc.DisplayName,
                        ValueType          = desc.ValueType,
                        OwnerObject        = current.gameObject
                    });
                }
            }

            foreach (Transform child in current)
                ScanRecursive(root, child, results);
        }
    }
}

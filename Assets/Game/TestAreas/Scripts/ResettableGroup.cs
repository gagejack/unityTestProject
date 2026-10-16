using System.Collections.Generic;
using UnityEngine;

namespace Game.TestAreas
{
    /// <summary>
    /// Remembers its children as they are when Play mode starts and can put them all back,
    /// so destroyed targets and mobs return without restarting Play mode.
    /// </summary>
    public sealed class ResettableGroup : MonoBehaviour
    {
        static readonly List<ResettableGroup> all = new List<ResettableGroup>();
        GameObject templates;

        public static void ResetAll()
        {
            foreach (var group in all.ToArray()) if (group) group.ResetChildren();
        }

        private void Awake()
        {
            // Copies made under an inactive parent never run Awake, so they stay untouched templates.
            templates = new GameObject(name + " (reset templates)");
            templates.SetActive(false);
            templates.hideFlags = HideFlags.HideInHierarchy;
            foreach (Transform child in transform)
            {
                var copy = Instantiate(child.gameObject, templates.transform);
                copy.name = child.name;
                copy.transform.SetLocalPositionAndRotation(child.localPosition, child.localRotation);
            }
        }

        private void OnEnable() => all.Add(this);
        private void OnDisable() => all.Remove(this);
        private void OnDestroy() { if (templates) Destroy(templates); }

        public void ResetChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
            foreach (Transform template in templates.transform)
            {
                var copy = Instantiate(template.gameObject, transform);
                copy.name = template.name;
                copy.transform.SetLocalPositionAndRotation(template.localPosition, template.localRotation);
            }
        }
    }
}

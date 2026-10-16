using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.TestAreas
{
    /// <summary>Shows a floating number wherever something takes damage, and a red number on kills.</summary>
    public sealed class DamagePopups : MonoBehaviour
    {
        [Tooltip("Seconds a number stays visible.")]
        [SerializeField, Min(0.1f)] private float lifetime = 0.9f;
        [Tooltip("Meters a number rises over its lifetime.")]
        [SerializeField, Min(0f)] private float rise = 0.8f;
        [Tooltip("Text size in the world.")]
        [SerializeField, Min(0.01f)] private float characterSize = 0.06f;

        struct Popup { public TextMesh text; public float born; public Vector3 start; }
        readonly List<Popup> live = new List<Popup>();
        Font font;

        private void OnEnable()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            CombatEvents.Damaged += Show;
        }

        private void OnDisable()
        {
            CombatEvents.Damaged -= Show;
            foreach (var p in live) if (p.text) Destroy(p.text.gameObject);
            live.Clear();
        }

        private void Show(DamageReport report)
        {
            var go = new GameObject("Damage " + report.Amount.ToString("0.#"));
            go.transform.SetParent(transform, false);
            var text = go.AddComponent<TextMesh>();
            text.font = font;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            text.fontSize = 64;
            text.characterSize = characterSize;
            text.anchor = TextAnchor.MiddleCenter;
            text.text = report.Killed ? report.Amount.ToString("0.#") + " KO" : report.Amount.ToString("0.#");
            text.color = report.Killed ? new Color(0.85f, 0.1f, 0.05f) : new Color(0.08f, 0.1f, 0.14f);
            Vector3 start = report.Position + Vector3.up * 2.1f + Random.insideUnitSphere * 0.15f;
            live.Add(new Popup { text = text, born = Time.time, start = start });
        }

        private void LateUpdate()
        {
            var view = Camera.main ? Camera.main.transform : null;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var p = live[i];
                float age = (Time.time - p.born) / lifetime;
                if (age >= 1f || !p.text) { if (p.text) Destroy(p.text.gameObject); live.RemoveAt(i); continue; }
                p.text.transform.position = p.start + Vector3.up * (rise * age);
                // Face the camera so the number reads correctly from any angle.
                if (view) p.text.transform.rotation = Quaternion.LookRotation(p.text.transform.position - view.position);
                var c = p.text.color; c.a = 1f - age * age; p.text.color = c;
            }
        }
    }
}

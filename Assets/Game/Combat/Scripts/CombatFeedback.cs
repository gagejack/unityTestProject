using UnityEngine;
using UnityEngine.UI;

namespace StarterGame
{
    // Resolve pending shots after the animation, support hand, and block grounding.
    [DefaultExecutionOrder(220)]
    public sealed class CombatFeedback : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float soundVolume = 0.8f;
        [SerializeField] private AudioClip meleeSwingSound;
        [SerializeField] private AudioClip meleeImpactSound;
        [Header("Hitmarker sound")]
        [SerializeField] private AudioClip[] hitmarkerSounds = new AudioClip[0];
        [SerializeField] private int selectedHitmarker;
        [SerializeField] private bool hitmarkerEnabled = true;
        [SerializeField, Range(0f, 1f)] private float hitmarkerVolume = 0.65f;
        private const string HitmarkerChoiceKey = "StarterGame.Hitmarker.Choice";
        private const string HitmarkerEnabledKey = "StarterGame.Hitmarker.Enabled";
        private const string HitmarkerVolumeKey = "StarterGame.Hitmarker.Volume";
        private AudioSource hitmarkerSource;
        private float nextHitmarkerSoundAt;
        public int HitmarkerSoundCount => hitmarkerSounds == null ? 0 : hitmarkerSounds.Length;
        public int SelectedHitmarker => selectedHitmarker;
        public bool HitmarkerEnabled => hitmarkerEnabled;
        public float HitmarkerVolume => hitmarkerVolume;
        public AudioClip SelectedHitmarkerClip => HitmarkerSoundCount > 0 ? hitmarkerSounds[Mathf.Clamp(selectedHitmarker, 0, HitmarkerSoundCount - 1)] : null;
        [Header("Punch sound")]
        [SerializeField] private AudioClip[] punchSounds = new AudioClip[0];
        [SerializeField] private int selectedPunch;
        [SerializeField] private bool punchEnabled = true;
        [SerializeField] private bool randomizePunch;
        [SerializeField, Range(0f, 1f)] private float punchVolume = 0.65f;
        private const string PunchChoiceKey = "StarterGame.Punch.Choice";
        private const string PunchEnabledKey = "StarterGame.Punch.Enabled";
        private const string PunchVolumeKey = "StarterGame.Punch.Volume";
        private const string PunchRandomKey = "StarterGame.Punch.Random";
        private AudioSource punchSource;
        private AudioSource firingSource;
        private GunAudioVolume firingVolume;
        private WeaponSettings firingWeapon;
        private int lastPunch = -1;
        public int PunchSoundCount => punchSounds == null ? 0 : punchSounds.Length;
        public int SelectedPunch => selectedPunch;
        public bool PunchEnabled => punchEnabled;
        public bool PunchRandomized => randomizePunch;
        public float PunchVolume => punchVolume;
        public AudioClip SelectedPunchClip => PunchSoundCount > 0 ? punchSounds[Mathf.Clamp(selectedPunch, 0, PunchSoundCount - 1)] : null;
        private WeaponCombatController weapons;
        private Image[] hitMarker;
        private float markerUntil;
        private int lastShot = -1, lastTail = -1, lastHandling = -1;
        private static Material effectMaterial;
        private static Material lineMaterial;

        private void Awake() { weapons = GetComponent<WeaponCombatController>(); }
        private void OnEnable()
        {
            selectedHitmarker = Mathf.Clamp(PlayerPrefs.GetInt(HitmarkerChoiceKey, selectedHitmarker), 0, Mathf.Max(0, HitmarkerSoundCount - 1));
            hitmarkerEnabled = PlayerPrefs.GetInt(HitmarkerEnabledKey, hitmarkerEnabled ? 1 : 0) != 0;
            hitmarkerVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(HitmarkerVolumeKey, hitmarkerVolume));
            selectedPunch = Mathf.Clamp(PlayerPrefs.GetInt(PunchChoiceKey, selectedPunch), 0, Mathf.Max(0, PunchSoundCount - 1));
            punchEnabled = PlayerPrefs.GetInt(PunchEnabledKey, punchEnabled ? 1 : 0) != 0;
            punchVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(PunchVolumeKey, punchVolume));
            randomizePunch = PlayerPrefs.GetInt(PunchRandomKey, randomizePunch ? 1 : 0) != 0;
        }

        private void OnDisable()
        {
            StopFiringSound();
            if (hitmarkerSource) hitmarkerSource.Stop();
            if (punchSource) punchSource.Stop();
            PlayerPrefs.Save();
        }
        private void LateUpdate()
        {
            if (weapons) weapons.ResolvePendingShot();
            if (firingWeapon)
            {
                if (!weapons || !weapons.HasWeapon || weapons.CurrentWeapon != firingWeapon || weapons.IsReloading ||
                    Time.timeScale <= 0f || Time.time - weapons.LastShotTime > Mathf.Max(0.1f, weapons.FireRate * 1.5f))
                    StopFiringSound();
                else if (firingSource) firingSource.transform.position = weapons.MuzzlePosition;
            }
            if (hitMarker == null) return;
            float alpha = Mathf.Clamp01((markerUntil - Time.time) / 0.06f);
            foreach (var stroke in hitMarker) if (stroke) stroke.color = new Color(1f, 0.9f, 0.55f, alpha);
        }

        public void CreateHitMarker(Transform canvas)
        {
            hitMarker = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject("Hit confirmation " + i, typeof(Image));
                go.transform.SetParent(canvas, false);
                var rect = go.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
                float angle = 45f + 90f * i;
                rect.anchoredPosition = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * 11f;
                rect.sizeDelta = new Vector2(8f, 2f);
                rect.localRotation = Quaternion.Euler(0, 0, angle);
                hitMarker[i] = go.GetComponent<Image>();
                hitMarker[i].raycastTarget = false;
                hitMarker[i].color = Color.clear;
            }
        }

        public void ConfirmHit(bool playSound = true)
        {
            markerUntil = Time.time + 0.14f;
            if (!playSound || !hitmarkerEnabled || !SelectedHitmarkerClip || Time.unscaledTime < nextHitmarkerSoundAt) return;
            // Shotgun pellets and several targets hit by one blast share one cue.
            nextHitmarkerSoundAt = Time.unscaledTime + 0.045f;
            var source = HitmarkerSource();
            source.clip = SelectedHitmarkerClip;
            source.PlayOneShot(SelectedHitmarkerClip);
        }

        public void SelectHitmarker(int index, bool preview = true, bool remember = true)
        {
            if (HitmarkerSoundCount == 0) return;
            selectedHitmarker = ((index % HitmarkerSoundCount) + HitmarkerSoundCount) % HitmarkerSoundCount;
            if (remember) PlayerPrefs.SetInt(HitmarkerChoiceKey, selectedHitmarker);
            if (preview) PreviewHitmarker();
        }

        public void SetHitmarkerEnabled(bool value, bool remember = true)
        {
            hitmarkerEnabled = value;
            if (!value && hitmarkerSource) hitmarkerSource.Stop();
            if (remember) PlayerPrefs.SetInt(HitmarkerEnabledKey, value ? 1 : 0);
        }

        public void SetHitmarkerVolume(float value, bool remember = true)
        {
            hitmarkerVolume = Mathf.Clamp01(value);
            if (hitmarkerSource) hitmarkerSource.volume = hitmarkerVolume * soundVolume;
            if (remember) PlayerPrefs.SetFloat(HitmarkerVolumeKey, hitmarkerVolume);
        }

        public bool PreviewHitmarker()
        {
            if (!Application.isPlaying || !hitmarkerEnabled || !SelectedHitmarkerClip) return false;
            var source = HitmarkerSource();
            source.Stop();
            source.clip = SelectedHitmarkerClip;
            source.Play();
            return true;
        }

        private AudioSource HitmarkerSource()
        {
            return FeedbackSource(ref hitmarkerSource, "Hitmarker feedback", hitmarkerVolume);
        }

        public void SelectPunch(int index, bool preview = true, bool remember = true)
        {
            if (PunchSoundCount == 0) return;
            selectedPunch = ((index % PunchSoundCount) + PunchSoundCount) % PunchSoundCount;
            if (remember) PlayerPrefs.SetInt(PunchChoiceKey, selectedPunch);
            if (preview) PreviewPunch();
        }

        public void SetPunchEnabled(bool value, bool remember = true)
        {
            punchEnabled = value;
            if (!value && punchSource) punchSource.Stop();
            if (remember) PlayerPrefs.SetInt(PunchEnabledKey, value ? 1 : 0);
        }

        public void SetPunchVolume(float value, bool remember = true)
        {
            punchVolume = Mathf.Clamp01(value);
            if (punchSource) punchSource.volume = punchVolume * soundVolume;
            if (remember) PlayerPrefs.SetFloat(PunchVolumeKey, punchVolume);
        }

        public void SetPunchRandomized(bool value, bool remember = true)
        {
            randomizePunch = value;
            if (remember) PlayerPrefs.SetInt(PunchRandomKey, value ? 1 : 0);
        }

        public bool PreviewPunch()
        {
            if (!Application.isPlaying || !punchEnabled || !SelectedPunchClip) return false;
            var source = FeedbackSource(ref punchSource, "Punch feedback", punchVolume);
            source.Stop();
            source.clip = SelectedPunchClip;
            lastPunch = selectedPunch;
            source.Play();
            return true;
        }

        private AudioSource FeedbackSource(ref AudioSource source, string childName, float volume)
        {
            if (!source)
            {
                var child = transform.Find(childName);
                if (!child)
                {
                    child = new GameObject(childName, typeof(AudioSource)).transform;
                    child.SetParent(transform, false);
                }
                source = child.GetComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
                source.dopplerLevel = 0f;
            }
            source.volume = volume * soundVolume;
            source.pitch = 1f;
            return source;
        }
        public void PlayGunSound(AudioClip clip, Vector3 position, float volume = 0.6f, bool vary = true, float delay = 0f, float pitch = 1f)
        {
            PlaySound(clip, position, volume, vary, delay, pitch, true);
        }

        public void PlaySound(AudioClip clip, Vector3 position, float volume = 0.6f, bool vary = true, float delay = 0f, float pitch = 1f, bool gunSound = false)
        {
            if (!clip || soundVolume <= 0f) return;
            var go = new GameObject("Combat sound " + clip.name);
            go.transform.position = position;
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume * soundVolume);
            if (gunSound) go.AddComponent<GunAudioVolume>();
            source.pitch = Mathf.Clamp(pitch, 0.25f, 2f) * (vary ? Random.Range(0.95f, 1.05f) : 1f);
            source.spatialBlend = 0.65f;
            source.minDistance = 2f;
            source.maxDistance = 45f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.PlayDelayed(delay);
            Destroy(go, delay + clip.length / source.pitch + 0.1f);
        }

        private static AudioClip Variation(AudioClip[] clips, ref int previous)
        {
            if (clips == null || clips.Length == 0) return null;
            bool skipPrevious = clips.Length > 1 && previous >= 0 && previous < clips.Length;
            int index = Random.Range(0, clips.Length - (skipPrevious ? 1 : 0));
            if (skipPrevious && index >= previous) index++;
            previous = Mathf.Clamp(index, 0, clips.Length - 1);
            return clips[previous];
        }

        public void Handling(AudioClip[] clips, Vector3 position, float pitch = 1f)
        {
            PlayGunSound(Variation(clips, ref lastHandling), position, 0.38f, false, 0f, pitch);
        }

        public void Shot(WeaponSettings settings, Vector3 muzzle, Vector3 direction)
        {
            if (settings.loopFireSound)
            {
                if (settings.fireSound && soundVolume > 0f)
                {
                    if (!firingSource)
                    {
                        var child = new GameObject("Continuous weapon fire", typeof(AudioSource));
                        child.transform.SetParent(transform, false);
                        firingSource = child.GetComponent<AudioSource>();
                        firingSource.playOnAwake = false;
                        firingSource.loop = true;
                        firingSource.spatialBlend = 0.65f;
                        firingSource.minDistance = 2f;
                        firingSource.maxDistance = 45f;
                        firingSource.rolloffMode = AudioRolloffMode.Linear;
                        firingSource.dopplerLevel = 0f;
                        firingVolume = child.AddComponent<GunAudioVolume>();
                    }
                    if (firingWeapon != settings || firingSource.clip != settings.fireSound) StopFiringSound();
                    firingWeapon = settings;
                    firingSource.transform.position = muzzle;
                    firingSource.clip = settings.fireSound;
                    firingVolume.SetBaseVolume(settings.soundVolume * soundVolume);
                    firingSource.pitch = settings.shotPitch;
                    if (!firingSource.isPlaying) firingSource.Play();
                }
            }
            else
            {
                StopFiringSound();
                var shot = Variation(settings.shotVariations, ref lastShot);
                PlayGunSound(shot ? shot : settings.fireSound, muzzle, settings.soundVolume, false, 0f, settings.shotPitch);
                PlayGunSound(Variation(settings.shotTails, ref lastTail), muzzle, settings.soundVolume * 0.65f, false, 0.04f, settings.shotPitch);
            }
            var flash = Effect("Muzzle flash", muzzle, settings.flashSize * new Vector3(0.5f, 0.5f, 1.6f), settings.flashColor, 0.045f);
            flash.transform.rotation = Quaternion.LookRotation(direction);
        }

        public void StopFiringSound()
        {
            if (firingSource) firingSource.Stop();
            firingWeapon = null;
        }

        public void Impact(Vector3 position, Vector3 normal, bool damageable, AudioClip clip = null)
        {
            PlayGunSound(clip ? clip : meleeImpactSound, position, 0.35f);
            ImpactEffects(position, normal, damageable);
        }

        public void MeleeImpact(Vector3 position, Vector3 normal, bool damageable)
        {
            if (PunchSoundCount == 0) PlaySound(meleeImpactSound, position, 0.35f);
            else if (punchEnabled && SelectedPunchClip)
            {
                // Skip the last played effect when several clips are available.
                var clip = randomizePunch ? Variation(punchSounds, ref lastPunch) : SelectedPunchClip;
                if (!randomizePunch) lastPunch = selectedPunch;
                var source = FeedbackSource(ref punchSource, "Punch feedback", punchVolume);
                source.clip = clip;
                source.PlayOneShot(clip);
            }
            ImpactEffects(position, normal, damageable, false);
        }

        private void ImpactEffects(Vector3 position, Vector3 normal, bool damageable, bool playHitmarkerSound = true)
        {
            Color color = damageable ? new Color(1f, 0.55f, 0.15f) : new Color(0.75f, 0.85f, 0.95f);
            for (int i = 0; i < 5; i++)
            {
                var go = Effect("Impact spark", position + normal * 0.015f, Vector3.one * Random.Range(0.025f, 0.055f), color, 0.15f);
                go.GetComponent<CombatTransient>().Velocity = (normal + Random.insideUnitSphere) * Random.Range(0.6f, 1.8f);
            }
            if (damageable) ConfirmHit(playHitmarkerSound);
        }

        public void MeleeSwing() { PlaySound(meleeSwingSound, transform.position, 0.3f); }

        public static GameObject Effect(string name, Vector3 position, Vector3 scale, Color color, float duration)
        {
            if (!effectMaterial)
            {
                effectMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                effectMaterial.hideFlags = HideFlags.HideAndDontSave;
            }
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = scale;
            var collider = go.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = effectMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(properties);
            go.AddComponent<CombatTransient>().Initialize(duration);
            return go;
        }

        public static void Tracer(Vector3 from, Vector3 to)
        {
            if (!lineMaterial)
            {
                lineMaterial = new Material(Shader.Find("Sprites/Default"));
                lineMaterial.hideFlags = HideFlags.HideAndDontSave;
            }
            var go = new GameObject("Muzzle-origin tracer");
            var line = go.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.startWidth = 0.018f;
            line.endWidth = 0.004f;
            line.sharedMaterial = lineMaterial;
            line.startColor = new Color(1f, 0.8f, 0.3f, 0.85f);
            line.endColor = new Color(1f, 0.8f, 0.3f, 0.2f);
            Destroy(go, 0.055f);
        }
    }
}

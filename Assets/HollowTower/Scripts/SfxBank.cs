using System.Collections.Generic;
using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// All the game's sound clips in one place (assigned by the floor 1 monster builder) and small helpers to play them,
    /// plus the two loops that run all the time: the building's low drone and the dread that rises when a monster is
    /// close to whoever the camera is in (its lurking breath, then a heartbeat once it is very near). The dread is heard
    /// for invisible monsters too: everyone can hear it coming, only P3 can see it.
    /// </summary>
    public class SfxBank : MonoBehaviour
    {
        public static SfxBank Instance { get; private set; }

        [Header("Players")]
        public AudioClip[] footsteps;
        public AudioClip[] runSteps;
        public AudioClip jump, land, torchOn, torchOff, fallScream, fallImpact, deathGasp, itemPickup;

        [Header("World")]
        public AudioClip doorOpen, doorClose, doorLocked, wallGrind, socketPower, drone;

        [Header("Monster")]
        public AudioClip monsterLurk, monsterScreech, monsterRoar, monsterAttack, heartbeat;
        [Tooltip("Heavy panting after sprinting too long (PlayerVitals).")]
        public AudioClip breathing;
        public AudioClip[] monsterSteps;

        [Header("Mix")]
        [Range(0f, 1f)] public float droneVolume = 0.18f;
        public float dreadRange = 18f;

        AudioSource droneSrc, lurkSrc, heartSrc;
        readonly List<AudioSource> pool = new List<AudioSource>();

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            droneSrc = Loop(drone, droneVolume);
            lurkSrc = Loop(monsterLurk, 0f);
            heartSrc = Loop(heartbeat, 0f);
        }

        AudioSource Loop(AudioClip c, float vol)
        {
            if (c == null) return null;
            var s = gameObject.AddComponent<AudioSource>();
            s.clip = c; s.loop = true; s.volume = vol; s.spatialBlend = 0f; s.playOnAwake = false;
            s.Play();
            return s;
        }

        void Update()
        {
            // dread: how close is the nearest hunting thing to the eyes we're looking through
            var view = PlayerParty.Active;
            float near = float.MaxValue;
            if (view != null)
                foreach (var m in NeedleMonster.All)
                    if (m != null && m.isActiveAndEnabled) near = Mathf.Min(near, Vector3.Distance(m.transform.position, view.position));
            float dread = Mathf.Clamp01(1f - near / dreadRange);
            if (lurkSrc != null) lurkSrc.volume = Mathf.MoveTowards(lurkSrc.volume, dread * 0.55f, Time.deltaTime * 0.5f);
            if (heartSrc != null)
            {
                float h = Mathf.Clamp01((dread - 0.45f) / 0.55f);
                heartSrc.volume = Mathf.MoveTowards(heartSrc.volume, h * 0.7f, Time.deltaTime * 0.6f);
                heartSrc.pitch = 0.9f + h * 0.35f;
            }
        }

        /// <summary>A one-shot at a point in the world (3D, fades with distance).</summary>
        public static void At(AudioClip clip, Vector3 pos, float volume = 1f, float pitch = 1f, float maxDistance = 25f)
        {
            if (clip == null || Instance == null) return;
            var s = Instance.Free();
            s.transform.position = pos;
            s.clip = clip; s.volume = volume; s.pitch = pitch;
            s.spatialBlend = 1f; s.rolloffMode = AudioRolloffMode.Linear; s.minDistance = 1.5f; s.maxDistance = maxDistance;
            s.Play();
        }

        /// <summary>A one-shot heard flat (interface sounds, your own body).</summary>
        public static void Flat(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null || Instance == null) return;
            var s = Instance.Free();
            s.transform.localPosition = Vector3.zero;
            s.clip = clip; s.volume = volume; s.pitch = pitch; s.spatialBlend = 0f;
            s.Play();
        }

        public static AudioClip Pick(AudioClip[] clips)
        {
            return clips == null || clips.Length == 0 ? null : clips[Random.Range(0, clips.Length)];
        }

        AudioSource Free()
        {
            foreach (var s in pool) if (!s.isPlaying) return s;
            var go = new GameObject("Sfx");
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.dopplerLevel = 0f;
            pool.Add(src);
            return src;
        }
    }
}

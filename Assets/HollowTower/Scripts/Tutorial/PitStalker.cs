using System.Collections;
using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// The tutorial's hidden-pit monster (the collage game's PitMonsterJumpscare, which was never brought over, so its
    /// scream never played). It breathes in its chamber under the hidden pit; when a player drops in (PitHazards.PitScare)
    /// it screams, the lights come up on it, it stares for a moment, then lunges with a roar. The player is put back at
    /// the edge as for any tutorial pit, and it sinks back to its rest pose.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class PitStalker : MonoBehaviour
    {
        [Header("Chamber under the hidden pit (world x/z)")]
        public Vector2 chamberMin = new Vector2(54f, 42f);
        public Vector2 chamberMax = new Vector2(61.5f, 58f);

        [Header("Timing")]
        public float stare = 0.8f;
        public float lungeSpeed = 1.5f;      // Monster8_Jumpscare peaks ~3.8 s in at speed 1
        public float hold = 0.5f;
        public float returnSpeed = -1.6f;
        [Tooltip("How far in front of it a fallen player is put to see it.")]
        public float viewDistance = 3.4f;

        [Header("Audio")]
        public AudioClip pitFallClip;        // chiiri-monster-15
        public AudioClip roarClip;           // SfxBank.monsterRoar if left empty

        const float LungePeak = 3.8f;
        Animator anim;
        AudioSource action, voice;
        Quaternion homeRot;
        Coroutine run;

        void Awake()
        {
            anim = GetComponent<Animator>();
            homeRot = transform.rotation;
            var src = GetComponents<AudioSource>();
            // the prefab's three sources: [0] action, [1] breathing loop, [2] the pit-fall voice
            action = src.Length > 0 ? src[0] : gameObject.AddComponent<AudioSource>();
            voice = src.Length > 2 ? src[2] : gameObject.AddComponent<AudioSource>();
            if (pitFallClip == null) pitFallClip = voice.clip;
            Setup(action, 0.5f, 3f, 40f);
            Setup(voice, 0.35f, 3.5f, 16f);
        }

        static void Setup(AudioSource s, float blend, float min, float max)
        {
            s.playOnAwake = false; s.loop = false; s.volume = 1f;
            s.spatialBlend = blend; s.minDistance = min; s.maxDistance = max;
        }

        void OnEnable() { PitHazards.PitScare += Claim; }
        void OnDisable() { PitHazards.PitScare -= Claim; }

        float Claim(RemasteredPlayerController body, Vector3 at)
        {
            if (at.x < chamberMin.x || at.x > chamberMax.x || at.z < chamberMin.y || at.z > chamberMax.y) return 0f;
            if (run != null) StopCoroutine(run);
            run = StartCoroutine(Scare(body));
            return stare + LungePeak / lungeSpeed + hold;
        }

        IEnumerator Scare(RemasteredPlayerController body)
        {
            if (pitFallClip != null) { voice.Stop(); voice.clip = pitFallClip; voice.volume = 1f; voice.Play(); }
            // the fall drops them mid-hole, which is where it stands: put them a few steps in front of it, looking up at it
            transform.rotation = homeRot;
            Vector3 spot = transform.position + homeRot * Vector3.forward * viewDistance;
            spot.x = Mathf.Clamp(spot.x, chamberMin.x + 0.6f, chamberMax.x - 0.6f);
            spot.z = Mathf.Clamp(spot.z, chamberMin.y + 0.6f, chamberMax.y - 0.6f);
            spot.y = transform.position.y;
            body.transform.position = spot;
            Vector3 head = transform.position + Vector3.up * 1.5f;   // it crouches low with its head forward
            float eyeY = spot.y + body.standingCamY;
            float lookUp = Mathf.Atan2(head.y - eyeY, Vector3.Distance(new Vector3(head.x, 0f, head.z), new Vector3(spot.x, 0f, spot.z))) * Mathf.Rad2Deg;
            body.FaceTowards(head, -lookUp);
            Face(spot);
            anim.SetBool("IsJumpscaring", false);
            anim.SetFloat("AnimSpeed", 1f);
            anim.CrossFade("Idle", 0.2f);
            yield return new WaitForSeconds(stare);

            anim.SetBool("IsJumpscaring", true);
            anim.SetFloat("AnimSpeed", lungeSpeed);
            anim.Play("Jumpscare", 0, 0f);
            var roar = roarClip != null ? roarClip : (SfxBank.Instance != null ? SfxBank.Instance.monsterRoar : null);
            if (roar != null) action.PlayOneShot(roar, 1f);
            yield return new WaitForSeconds(LungePeak / lungeSpeed);

            anim.SetFloat("AnimSpeed", 0f);   // frozen at the peak while the screen goes dark
            yield return new WaitForSeconds(hold + 0.5f);

            // they are back up at the edge: let the voice die away and sink back into the pit
            for (float t = 0f; t < 1f; t += Time.deltaTime / 1.2f) { voice.volume = 1f - t; yield return null; }
            voice.Stop();
            anim.SetFloat("AnimSpeed", returnSpeed);
            float back = LungePeak / Mathf.Abs(returnSpeed);
            for (float t = 0f; t < back; t += Time.deltaTime)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, homeRot, Time.deltaTime * 2f);
                yield return null;
            }
            transform.rotation = homeRot;
            anim.SetBool("IsJumpscaring", false);
            anim.SetFloat("AnimSpeed", 1f);
            anim.CrossFade("Idle", 0.25f);
            run = null;
        }

        void Face(Vector3 p)
        {
            Vector3 d = p - transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d);
        }
    }
}

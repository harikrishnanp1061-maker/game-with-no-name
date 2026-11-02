using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// A player's body sounds: footsteps paced by speed (quieter crouching, heavier sprinting), the jump, and the landing
    /// after a real drop. Heard in 3D, so teammates standing about hear you walk past. The steered player's own
    /// footsteps are what the monsters listen for (see NeedleMonster.Hear).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerSfx : MonoBehaviour
    {
        public float strideWalk = 2.1f;
        public float strideSprint = 2.9f;

        CharacterController cc;
        RemasteredPlayerController body;
        Net.NetBody net;
        float travelled;
        bool wasGrounded = true;
        float airborneFrom;
        Vector3 lastPos, remoteVel;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            body = GetComponent<RemasteredPlayerController>();
            net = GetComponent<Net.NetBody>();
            lastPos = transform.position;
        }

        // the user's running/jogging recordings, looped while that gait lasts: running indoors; on the campus outside,
        // running and (much louder) jogging on grass. Walking indoors keeps the single steps.
        AudioSource gait; float outdoorCheck; bool outdoors;

        void Gait(float speed, bool grounded, bool sprint, bool crouch)
        {
            if (gait == null)
            {
                gait = gameObject.AddComponent<AudioSource>();
                gait.loop = true; gait.playOnAwake = false; gait.rolloffMode = AudioRolloffMode.Linear; gait.minDistance = 2f; gait.maxDistance = 26f; gait.dopplerLevel = 0f;
            }
            if (Time.time > outdoorCheck)
            {
                outdoorCheck = Time.time + 0.3f;
                outdoors = Campus.CampusObjective.Instance != null && !Campus.CampusGiant.Indoors(transform.position);
            }
            AudioClip want = null; float vol = 0f;
            if (grounded && speed > 0.4f && !crouch)
            {
                if (outdoors) { want = UserSfx.Get(sprint ? "outdoor_run" : "outdoor_jog"); vol = sprint ? 0.9f : 0.75f; }
                else if (sprint) { want = UserSfx.Get("indoor_run"); vol = 0.7f; }
            }
            if (want != null && gait.clip != want) { gait.clip = want; gait.time = Random.Range(0f, Mathf.Max(0f, want.length - 1f)); gait.Play(); }
            gait.spatialBlend = PlayerParty.Instance != null && PlayerParty.Instance.Current == body ? 0f : 1f;
            gait.volume = Mathf.MoveTowards(gait.volume, want != null ? vol : 0f, Time.deltaTime * 4f);
            if (gait.volume <= 0.01f && gait.isPlaying && want == null) { gait.Stop(); gait.clip = null; }
            looping = want != null;
        }
        bool looping;

        void Update()
        {
            var bank = SfxBank.Instance;
            // someone else's body moves by network updates, not by its CharacterController (whose velocity stays zero):
            // pace their steps from how far it actually travelled and use the grounded flag they send
            bool remote = body != null && body.remote;
            Vector3 moved = (transform.position - lastPos) / Mathf.Max(Time.deltaTime, 0.0001f);
            lastPos = transform.position;
            if (remote) remoteVel = Vector3.Lerp(remoteVel, moved, 1f - Mathf.Exp(-10f * Time.deltaTime));
            if (bank == null || cc == null || (!remote && !cc.enabled) || (body != null && body.IsDead)) return;
            Vector3 v = remote ? remoteVel : cc.velocity;
            float speed = new Vector2(v.x, v.z).magnitude;
            bool grounded = remote ? (net == null || net.Grounded) : cc.isGrounded;

            Gait(speed, grounded, speed > 5.5f, speed < 2.6f);
            if (grounded && speed > 0.4f)
            {
                bool sprint = speed > 5.5f, crouch = speed < 2.6f;
                travelled += speed * Time.deltaTime;
                float stride = sprint ? strideSprint : strideWalk;
                if (travelled >= stride)
                {
                    travelled = 0f;
                    float vol = sprint ? 0.75f : crouch ? 0.18f : 0.42f;
                    if (!looping) SfxBank.At(SfxBank.Pick(sprint && bank.runSteps != null && bank.runSteps.Length > 0 ? bank.runSteps : bank.footsteps), transform.position, vol, Random.Range(0.92f, 1.08f), sprint ? 22f : 14f);
                    // footsteps are heard by every monster, through the host (a client's steps reach it too)
                    if (body != null && body.isControlled) Net.NetWorld.Noise(transform.position, sprint ? 10f : crouch ? 1.5f : 4.5f);
                }
            }

            if (wasGrounded && !grounded)
            {
                airborneFrom = Time.time;
                if (v.y > 2f) SfxBank.At(bank.jump, transform.position, 0.5f);
            }
            else if (!wasGrounded && grounded && Time.time - airborneFrom > 0.35f)
            {
                SfxBank.At(bank.land, transform.position, 0.6f);
                if (body != null && body.isControlled) Net.NetWorld.Noise(transform.position, 6f);
            }
            wasGrounded = grounded;
        }
    }
}

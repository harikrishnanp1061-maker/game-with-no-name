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
        float travelled;
        bool wasGrounded = true;
        float airborneFrom;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            body = GetComponent<RemasteredPlayerController>();
        }

        void Update()
        {
            var bank = SfxBank.Instance;
            if (bank == null || cc == null || !cc.enabled || (body != null && body.IsDead)) return;
            Vector3 v = cc.velocity;
            float speed = new Vector2(v.x, v.z).magnitude;
            bool grounded = cc.isGrounded;

            if (grounded && speed > 0.4f)
            {
                bool sprint = speed > 5.5f, crouch = speed < 2.6f;
                travelled += speed * Time.deltaTime;
                float stride = sprint ? strideSprint : strideWalk;
                if (travelled >= stride)
                {
                    travelled = 0f;
                    float vol = sprint ? 0.75f : crouch ? 0.18f : 0.42f;
                    SfxBank.At(SfxBank.Pick(sprint && bank.runSteps != null && bank.runSteps.Length > 0 ? bank.runSteps : bank.footsteps), transform.position, vol, Random.Range(0.92f, 1.08f), sprint ? 22f : 14f);
                    if (body != null && body.isControlled) NeedleMonster.Hear(transform.position, sprint ? 10f : crouch ? 1.5f : 4.5f);
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
                if (body != null && body.isControlled) NeedleMonster.Hear(transform.position, 6f);
            }
            wasGrounded = grounded;
        }
    }
}

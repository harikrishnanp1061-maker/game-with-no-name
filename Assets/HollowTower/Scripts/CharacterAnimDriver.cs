using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// Feeds the Survivor animator (Assets/Characters/Survivor) without jitter: the body's velocity goes in as a damped
    /// local-space vector for the 2D directional blend trees, "grounded" only drops after a short airborne moment (the
    /// CharacterController flickers on steps and slopes), and the held-item arm layer fades in and out. Clips past their
    /// recorded speed are sped up through LocoMul so the feet never slide. Used by RemasteredPlayerController for bodies
    /// steered on this machine and by NetBody for everyone else's.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class CharacterAnimDriver : MonoBehaviour
    {
        [Header("Clip speeds at the edge of each blend tree (normalised humanoid m/s); faster plays the cycle faster")]
        public float runClipSpeed = 3.6f;
        public float backClipSpeed = 1.5f;
        public float strafeClipSpeed = 1.5f;
        public float crouchClipSpeed = 1.2f;
        public float crouchSideClipSpeed = 1.0f;
        public float velocityDamp = 0.18f;
        public float airDelay = 0.18f;

        static readonly int VelX = Animator.StringToHash("VelX"), VelZ = Animator.StringToHash("VelZ"),
            Speed = Animator.StringToHash("Speed"), LocoMul = Animator.StringToHash("LocoMul"),
            Grounded = Animator.StringToHash("IsGrounded"), Crouching = Animator.StringToHash("IsCrouching"),
            HasTorch = Animator.StringToHash("HasTorch"), JumpHash = Animator.StringToHash("Jump"),
            CrouchW = Animator.StringToHash("CrouchW"), AirW = Animator.StringToHash("AirW");

        [Tooltip("Seconds the stand/crouch and ground/air blends take to settle (eased, never linear).")]
        public float stanceTime = 0.11f, airBlendTime = 0.07f;
        [Tooltip("Most a cycle is sped up past its clip speed, so fast strafing never turns into a frantic shuffle.")]
        public float maxCadence = 2.2f;

        Animator anim;
        float airTime, mul = 1f, holdWeight, jumpPending, crouchW, crouchVel, airW, airVel;
        Vector3 blendVel, blendAcc;
        int holdLayer = -1;

        void Awake()
        {
            anim = GetComponent<Animator>();
            anim.applyRootMotion = false;
            holdLayer = anim.GetLayerIndex("HoldItem");
        }

        /// <param name="worldVelocity">how the body is moving (m/s, world space)</param>
        public void Drive(Vector3 worldVelocity, bool grounded, bool crouching, bool holdingItem, bool dead)
        {
            if (anim == null || anim.runtimeAnimatorController == null) return;
            float dt = Time.deltaTime;
            if (dead)
            {
                blendVel = Vector3.SmoothDamp(blendVel, Vector3.zero, ref blendAcc, velocityDamp, Mathf.Infinity, dt);
                anim.SetFloat(VelX, blendVel.x);
                anim.SetFloat(VelZ, blendVel.z);
                FadeHold(false, dt);
                return;
            }

            // world m/s -> the avatar's normalised units, in the body's own frame
            float scale = Mathf.Max(0.01f, anim.humanScale * transform.lossyScale.y);
            Vector3 local = transform.InverseTransformDirection(worldVelocity);
            local.y = 0f;
            local /= scale;
            float speed = local.magnitude;
            if (speed < 0.08f) { local = Vector3.zero; speed = 0f; }

            float f = speed > 0f ? local.z / speed : 1f;   // 1 forward, 0 sideways, -1 backward
            airTime = grounded ? 0f : airTime + dt;
            bool inAir = airTime >= airDelay;
            crouchW = Mathf.SmoothDamp(crouchW, crouching && !inAir ? 1f : 0f, ref crouchVel, stanceTime, Mathf.Infinity, dt);
            airW = Mathf.SmoothDamp(airW, inAir ? 1f : 0f, ref airVel, airBlendTime, Mathf.Infinity, dt);

            float side = Mathf.Lerp(strafeClipSpeed, crouchSideClipSpeed, crouchW);
            float top = f >= 0f ? Mathf.Lerp(side, Mathf.Lerp(runClipSpeed, crouchClipSpeed, crouchW), f)
                                : Mathf.Lerp(side, Mathf.Lerp(backClipSpeed, crouchClipSpeed, crouchW), -f);
            float wantMul = inAir ? 1f : Mathf.Min(speed > top ? speed / top : 1f, maxCadence);
            mul = Mathf.Lerp(mul, wantMul, 1f - Mathf.Exp(-8f * dt));
            Vector3 clamped = speed > top ? local * (top / speed) : local;

            // critically damped (not exponential): starting or stopping eases in instead of kicking on the first frame
            blendVel = Vector3.SmoothDamp(blendVel, clamped, ref blendAcc, velocityDamp, Mathf.Infinity, dt);
            anim.SetFloat(VelX, blendVel.x);
            anim.SetFloat(VelZ, blendVel.z);
            anim.SetFloat(Speed, speed * scale, velocityDamp, dt);
            anim.SetFloat(LocoMul, mul);

            anim.SetFloat(CrouchW, crouchW);
            anim.SetFloat(AirW, airW);
            if (jumpPending > 0f && (jumpPending -= dt) <= 0f) anim.ResetTrigger(JumpHash);   // never taken: don't fire later
            anim.SetBool(Grounded, !inAir);
            anim.SetBool(Crouching, crouching);
            anim.SetBool(HasTorch, holdingItem);
            FadeHold(holdingItem && !crouching, dt);
        }

        public void Jump()
        {
            if (anim != null) anim.SetTrigger(JumpHash);
            jumpPending = 0.25f;
            airTime = airDelay;   // straight into the air states, no wait
        }

        void FadeHold(bool on, float dt)
        {
            if (holdLayer < 0) return;
            holdWeight = Mathf.MoveTowards(holdWeight, on ? 1f : 0f, dt * 3f);
            anim.SetLayerWeight(holdLayer, Mathf.SmoothStep(0f, 1f, holdWeight));
        }
    }
}

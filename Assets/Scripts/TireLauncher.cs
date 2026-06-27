using UnityEngine;
using UnityEngine.InputSystem;

namespace LearnToSpin
{
    /// <summary>
    /// Core launch loop with agency in both phases:
    ///   CHARGE  — the engine has a "power band". Release inside it for a PERFECT launch;
    ///             over-rev past it and the engine overheats and LOSES power. (skill: timing)
    ///   RUN     — steer left/right to dodge hazards and line up ramps, and HOLD the boost
    ///             to drain a reserve that pumps fresh spin into the wheel. (skill: management)
    /// Upgrades feed the tunables on this component.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class TireLauncher : MonoBehaviour
    {
        public enum State { Ready, Charging, Launched, Stopped }

        [Header("Engine — charge")]
        public float maxSpin = 110f;
        [Tooltip("Peak of the power band — releasing here gives the strongest launch.")]
        public float idealSpin = 90f;
        [Tooltip("Rev speed, rad/sec per second. Higher = tighter (harder) release window.")]
        public float chargeRate = 70f;
        public float overheatPenalty = 1.6f;
        [Range(0.01f, 0.2f)] public float perfectWindow = 0.06f;
        public float perfectBonus = 1.25f;

        [Header("Steering (in-run)")]
        [Tooltip("Top sideways speed, m/s.")]
        public float maxLateralSpeed = 7f;
        [Tooltip("How quickly we reach that sideways speed, m/s per second.")]
        public float steerAccel = 28f;
        [Tooltip("Half-width of the drivable lane, metres from centre.")]
        public float laneHalfWidth = 13f;

        [Header("Boost — hold to drain")]
        public float boostReserve = 100f;
        [Tooltip("Reserve spent per second while held.")]
        public float boostDrainRate = 45f;
        [Tooltip("Spin pumped into the wheel per second while boosting, rad/sec per second.")]
        public float boostSpinRate = 110f;
        [Tooltip("Forward acceleration added while boosting on the ground, m/s^2.")]
        public float boostGroundKick = 8f;

        [Header("Bounce — landing skip")]
        [Tooltip("Upgrade-driven restitution (0..1). Higher = a stronger forward skip on landing.")]
        [Range(0f, 1f)] public float bounciness = 0.55f;
        [Tooltip("Minimum downward impact speed (m/s) that counts as a landing worth bouncing.")]
        public float minBounceSpeed = 1.5f;
        [Tooltip("Fraction of the bounce that stays vertical. Lower = flatter, more forward skips.")]
        [Range(0f, 1f)] public float bounceUpKeep = 0.65f;
        [Tooltip("How much of the impact bounciness converts into FORWARD carry on landing.")]
        public float bounceForwardGain = 1.05f;
        [Tooltip("Hard cap on the upward hop (m/s) so maxed bounciness can't just launch skyward.")]
        public float maxBounceUp = 9f;

        [Header("Rolling / run end")]
        [Tooltip("Gentle constant decel while rolling, m/s^2 — light enough to keep the glide.")]
        public float rollingResistance = 0.8f;
        [Tooltip("Forward speed under which the slow-down brake kicks in (m/s).")]
        public float slowSpeed = 10f;
        [Tooltip("Extra decel once below slowSpeed, so the tire bleeds speed faster down the tail.")]
        public float slowBrake = 2.5f;
        [Tooltip("Extra decel once below crawlSpeed, to kill the slow tail.")]
        public float tailBrake = 5f;
        [Tooltip("Forward speed under which the tail brake (and spin bleed) kick in.")]
        public float crawlSpeed = 4f;
        [Tooltip("End the run when forward speed stays below this (m/s).")]
        public float stopSpeed = 1.0f;
        public float stopGrace = 0.8f;
        [Tooltip("Seconds for the tire to topple flat onto its side once the run ends.")]
        public float toppleDuration = 0.6f;

        // ---- UI-facing state ----
        public State CurrentState { get; private set; } = State.Ready;
        public float ChargeNormalized => Mathf.Clamp01(_charge / Mathf.Max(0.01f, maxSpin));
        public float IdealNormalized => Mathf.Clamp01(idealSpin / Mathf.Max(0.01f, maxSpin));
        public float PerfectHalfWidthNormalized => idealSpin * perfectWindow / Mathf.Max(0.01f, maxSpin);
        public float BoostNormalized => Mathf.Clamp01(_reserve / Mathf.Max(0.01f, boostReserve));
        public bool IsBoosting { get; private set; }
        public bool LastWasPerfect { get; private set; }
        public float PerfectFlash { get; private set; }
        public float Distance { get; private set; }
        public float BestDistance { get; private set; }
        /// <summary>Top speed (m/s) and max height (m above the start) reached this run — drive the payout.</summary>
        public float TopSpeed { get; private set; }
        public float MaxHeight { get; private set; }
        /// <summary>Longest single uninterrupted stretch (s) the tire stayed airborne this run — a payout stat.</summary>
        public float LongestAirTime { get; private set; }
        /// <summary>Length (s) of the CURRENT airborne stretch (0 while grounded) — drives the trick meter.</summary>
        public float CurrentAirTime { get; private set; }
        /// <summary>Current altitude (m above the launch point) — drives the live HUD readout.</summary>
        public float Height => transform.position.y - _startPos.y;
        public float Speed => _rb != null ? _rb.linearVelocity.magnitude : 0f;
        public bool IsGrounded() => Physics.Raycast(transform.position, Vector3.down, _groundCheck);

        /// <summary>Fires once when a run settles (State → Stopped). The director awards money + opens the shop.</summary>
        public event System.Action RunEnded;

        // Air detection grace: how long after the last real ground/ramp CONTACT the tire still
        // counts as grounded. Debounces physics flicker (and a per-step gap between contacts) so a
        // genuine jump reads as continuous air instead of getting chopped. Contact-based, not a
        // down-raycast — flying low over hazards/ramp tops no longer falsely "lands" you.
        const float AirGrace = 0.12f;

        Rigidbody _rb;
        float _charge;
        float _reserve;
        float _airStint;       // running length of the current airborne stretch
        float _lastGroundTime;  // Time.time of the most recent upward-surface contact
        float _startZ;
        float _stopTimer;
        float _radius = 0.5f;
        float _groundCheck = 0.62f;
        Vector3 _startPos;
        Quaternion _startRot;

        // Run-end topple (kinematic): the sphere collider can't physically tip, so we animate the
        // wheel falling onto its side and settling to the ground over toppleDuration.
        bool _toppling;
        float _toppleT;
        Vector3 _toppleFromPos, _toppleToPos;
        Quaternion _toppleFromRot, _toppleToRot;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.maxAngularVelocity = 1000f; // PhysX clamps to 7 rad/s by default — would gut the launch.
            var col = GetComponent<SphereCollider>();
            if (col != null) { _radius = col.radius; _groundCheck = col.radius + 0.12f; }
            _startPos = transform.position;
            _startRot = transform.rotation;
            _startZ = _startPos.z;
            ResetToReady();
        }

        void Update()
        {
            if (PerfectFlash > 0f) PerfectFlash -= Time.deltaTime;

            var kb = Keyboard.current;
            if (kb == null) return;

            switch (CurrentState)
            {
                case State.Ready:
                    if (kb.spaceKey.isPressed) CurrentState = State.Charging;
                    break;

                case State.Charging:
                    AudioManager.Instance.SetRevving(true);
                    _charge = Mathf.Min(maxSpin, _charge + chargeRate * Time.deltaTime);
                    transform.Rotate(Vector3.right, _charge * Time.deltaTime * Mathf.Rad2Deg, Space.Self);
                    if (!kb.spaceKey.isPressed) Launch();
                    break;

                case State.Launched:
                    Distance = Mathf.Max(Distance, transform.position.z - _startZ);
                    TopSpeed = Mathf.Max(TopSpeed, Speed);
                    MaxHeight = Mathf.Max(MaxHeight, transform.position.y - _startPos.y);
                    TrackAirTime();
                    break;

                case State.Stopped:
                    TrackTopple();
                    break;
            }

            if (kb.rKey.wasPressedThisFrame) ResetToReady();
        }

        void FixedUpdate()
        {
            if (CurrentState != State.Launched) return;
            float dt = Time.fixedDeltaTime;
            var kb = Keyboard.current;

            Steer(kb, dt);
            Boost(kb, dt);
            ApplyRollingResistance(dt);

            // Cache the grounded state so we don't raycast multiple times
            bool isGrounded = IsGrounded();

            // ==========================================
            // AUDIO UPDATES
            // ==========================================
            if (AudioManager.Instance != null)
            {
                bool isRollingWood = false;

                if (isGrounded && Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, _groundCheck))
                {
                    if (hit.collider.name.StartsWith("Ramp"))
                    {
                        isRollingWood = true;
                    }
                }

                bool isMoving = Speed > 1.0f;
                AudioManager.Instance.SetRolling(isGrounded && !isRollingWood && isMoving, 
                                                 isGrounded && isRollingWood && isMoving);

                AudioManager.Instance.SetAirWhoosh(!isGrounded, Mathf.Clamp01(Speed / 50f));

                // NEW: Play the revving sound whenever the player is actively boosting
                AudioManager.Instance.SetRevving(IsBoosting); 
            }
            // ==========================================

            // End on FORWARD speed only (so wiggling sideways can't keep the run alive),
            // and only while grounded and not actively boosting.
            bool slow = isGrounded && !IsBoosting
                        && _rb.linearVelocity.z < stopSpeed
                        && _rb.angularVelocity.magnitude < 3f;

            _stopTimer = slow ? _stopTimer + dt : 0f;
            if (_stopTimer >= stopGrace)
            {
                CurrentState = State.Stopped;
                IsBoosting = false;
                BestDistance = Mathf.Max(BestDistance, Distance);

                // Let the audio manager know we stopped moving
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.SetRolling(false, false);
                    AudioManager.Instance.SetAirWhoosh(false, 0f);

                    // NEW: Ensure the revving stops (and fades out) when the run ends
                    AudioManager.Instance.SetRevving(false); 
                }

                // Play the topple first; RunEnded (payout + shop) fires once it has settled flat.
                BeginTopple();
            }
        }

        /// <summary>
        /// Custom landing bounce. PhysX restitution would reflect the impact straight up the
        /// contact normal (pure height, no distance); instead we split the downward impact into a
        /// CAPPED vertical hop plus a forward skip that keeps scaling with bounciness — so maxing
        /// the upgrade carries the tire further down the lane instead of launching it skyward.
        /// </summary>
        void OnCollisionEnter(Collision c)
        {
            if (CurrentState != State.Launched) return;

            if (TouchedGround(c)) _lastGroundTime = Time.time;

            // Only treat roughly-upward contacts (the ground/ramps) as landings.
            Vector3 n = c.GetContact(0).normal;
            if (n.y < 0.5f) return;

            Vector3 v = _rb.linearVelocity;
            float vDown = -v.y;                 // positive while falling onto the surface
            if (vDown < minBounceSpeed) return; // tiny taps just roll

            // No landing/impact SFX on ordinary ground bounces — the impact sound is reserved
            // for hazard hits (see Hazard.cs). Landing a trick still cues its own sound from
            // TrickMeterUI.

            float impact = bounciness * vDown;
            v.y = Mathf.Min(maxBounceUp, impact * bounceUpKeep); // limited hop
            v.z += impact * bounceForwardGain;                   // converted into forward carry
            _rb.linearVelocity = v;
        }

        /// <summary>
        /// Light resistance at speed (keeps the glide), strong tail brake below crawlSpeed
        /// that also bleeds the leftover spin so grip can't keep nudging the tire forever.
        /// </summary>
        void ApplyRollingResistance(float dt)
        {
            if (IsBoosting || !IsGrounded()) return;

            Vector3 v = _rb.linearVelocity;
            if (v.z <= 0f) return;

            bool crawling = v.z < crawlSpeed;
            // Light glide at speed; ramp up the braking below slowSpeed, then hammer it at a crawl.
            float dec = rollingResistance
                        + (v.z < slowSpeed ? slowBrake : 0f)
                        + (crawling ? tailBrake : 0f);
            v.z = Mathf.Max(0f, v.z - dec * dt);
            _rb.linearVelocity = v;

            if (crawling)
            {
                // bleed spin at a matching rate (ω = v / R) so the wheel actually settles
                Vector3 av = _rb.angularVelocity;
                av.x = Mathf.MoveTowards(av.x, 0f, (tailBrake / Mathf.Max(0.2f, _radius)) * dt);
                _rb.angularVelocity = av;
            }
        }

        void Steer(Keyboard kb, float dt)
        {
            float h = 0f;
            if (kb != null)
            {
                if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) h -= 1f;
                if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) h += 1f;
            }

            // drive lateral velocity directly so steering stays crisp regardless of ground friction
            Vector3 v = _rb.linearVelocity;
            v.x = Mathf.MoveTowards(v.x, h * maxLateralSpeed, steerAccel * dt);
            _rb.linearVelocity = v;

            // keep inside the lane
            if (Mathf.Abs(_rb.position.x) > laneHalfWidth)
            {
                Vector3 p = _rb.position;
                p.x = Mathf.Clamp(p.x, -laneHalfWidth, laneHalfWidth);
                _rb.position = p;
                v = _rb.linearVelocity; v.x = 0f; _rb.linearVelocity = v;
            }
        }

        void Boost(Keyboard kb, float dt)
        {
            bool want = kb != null && kb.spaceKey.isPressed && _reserve > 0f;
            IsBoosting = want;
            if (!want) return;

            _reserve = Mathf.Max(0f, _reserve - boostDrainRate * dt);
            _rb.angularVelocity += new Vector3(boostSpinRate * dt, 0f, 0f);
            if (IsGrounded())
                _rb.AddForce(Vector3.forward * boostGroundKick, ForceMode.Acceleration);
        }

        /// <summary>
        /// Grow the current airborne stretch while the wheel is off the ground, banking the longest
        /// stretch of the run. "Airborne" = no upward-surface contact for longer than <see cref="AirGrace"/>
        /// (kept fresh by OnCollision Enter/Stay), so the streak only resets on a real touchdown.
        /// </summary>
        void TrackAirTime()
        {
            bool airborne = Time.time - _lastGroundTime > AirGrace;
            if (airborne)
            {
                _airStint += Time.deltaTime;
                CurrentAirTime = _airStint;
                LongestAirTime = Mathf.Max(LongestAirTime, _airStint);
            }
            else
            {
                _airStint = 0f;
                CurrentAirTime = 0f;
            }
        }

        /// <summary>Keep the ground timer fresh every physics step the wheel is rolling/resting on a
        /// floor or ramp — this is what makes the airborne test a clean "haven't touched down lately".</summary>
        void OnCollisionStay(Collision c)
        {
            if (CurrentState == State.Launched && TouchedGround(c)) _lastGroundTime = Time.time;
        }

        /// <summary>True if any contact in the collision faces roughly upward (ground/ramp), as
        /// opposed to a wall/fence/hazard side we can clip without it counting as a landing.</summary>
        static bool TouchedGround(Collision c)
        {
            for (int i = 0; i < c.contactCount; i++)
                if (c.GetContact(i).normal.y > 0.5f) return true;
            return false;
        }

        float EffectiveSpin(float charge, out bool perfect)
        {
            float eff = charge <= idealSpin
                ? charge
                : Mathf.Max(idealSpin * 0.4f, idealSpin - overheatPenalty * (charge - idealSpin));
            perfect = Mathf.Abs(charge - idealSpin) <= idealSpin * perfectWindow;
            if (perfect) eff *= perfectBonus;
            return eff;
        }

        void Launch()
        {
            float eff = EffectiveSpin(_charge, out bool perfect);
            LastWasPerfect = perfect;
            PerfectFlash = perfect ? 1.5f : 0f;

            AudioManager.Instance.SetRevving(false);
            AudioManager.Instance.PlayTireLaunch();
            if (perfect) {
                AudioManager.Instance.PlayLaunchPerfect();
            }
            CurrentState = State.Launched;
            _reserve = boostReserve;
            _lastGroundTime = Time.time; // on the ground at lift-off — don't read the launch frame as air
            _rb.isKinematic = false;
            _rb.angularVelocity = new Vector3(eff, 0f, 0f); // around the axle → grips ground → forward
            _stopTimer = 0f;
        }

        /// <summary>
        /// Push upgraded/equipped-tire tunables onto the launcher and re-arm. Called by TireBuilder
        /// right after the component is added, so the first Ready state already reflects the shop.
        /// </summary>
        public void ApplyTuning(float newMaxSpin, float newIdealSpin, float newBoostReserve, float newBounciness)
        {
            maxSpin = newMaxSpin;
            idealSpin = newIdealSpin;
            boostReserve = newBoostReserve;
            bounciness = newBounciness;
            ResetToReady();
        }

        /// <summary>
        /// Kick off the end-of-run topple: freeze physics (the sphere collider can't tip on its own)
        /// and animate the wheel rotating 90° onto its side while sinking to rest on the ground.
        /// </summary>
        void BeginTopple()
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic = true;

            _toppleFromPos = transform.position;
            _toppleFromRot = transform.rotation;
            // Tip about the forward (Z) axis so the axle swings from horizontal to vertical and the
            // wheel lands flat; drop the centre to roughly the lying half-thickness above the floor.
            _toppleToRot = Quaternion.AngleAxis(90f, Vector3.forward) * transform.rotation;
            _toppleToPos = new Vector3(transform.position.x, _radius * 0.2f, transform.position.z);
            _toppleT = 0f;
            _toppling = true;
        }

        /// <summary>
        /// Advance the topple animation (smoothstepped) until the wheel has settled flat, then fire
        /// RunEnded so the payout/shop only appears after the tire has finished falling over.
        /// </summary>
        void TrackTopple()
        {
            if (!_toppling) return;
            _toppleT += Time.deltaTime / Mathf.Max(0.05f, toppleDuration);
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_toppleT));
            transform.SetPositionAndRotation(
                Vector3.Lerp(_toppleFromPos, _toppleToPos, k),
                Quaternion.Slerp(_toppleFromRot, _toppleToRot, k));
            if (_toppleT >= 1f)
            {
                _toppling = false;
                RunEnded?.Invoke();
            }
        }

        void ResetToReady()
        {
            _toppling = false;
            CurrentState = State.Ready;
            _charge = 0f;
            _reserve = boostReserve;
            _stopTimer = 0f;
            Distance = 0f;
            TopSpeed = 0f;
            MaxHeight = 0f;
            LongestAirTime = 0f;
            CurrentAirTime = 0f;
            _airStint = 0f;
            _lastGroundTime = Time.time;
            IsBoosting = false;
            LastWasPerfect = false;
            PerfectFlash = 0f;

            _rb.isKinematic = false;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic = true;
            transform.SetPositionAndRotation(_startPos, _startRot);
        }
    }
}

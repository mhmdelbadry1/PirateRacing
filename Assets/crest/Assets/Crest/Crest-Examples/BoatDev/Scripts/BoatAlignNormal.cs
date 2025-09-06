// Modified BoatAlignNormal (full) — crest buoyancy + optional ML-Agent control
// Keeps original buoyancy / drag / orientation behaviour, and accepts throttle/steer
// from an external controller (e.g. ShipAgent). If useAgentControls is false, it
// falls back to the original player keyboard inputs when _playerControlled==true.

#if CREST_UNITY_INPUT && ENABLE_INPUT_SYSTEM
#define INPUT_SYSTEM_ENABLED
#endif

using Crest.Internal;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Crest.Examples
{
    [AddComponentMenu(Crest.Internal.Constants.MENU_PREFIX_EXAMPLE + "Boat Align Normal")]
    public class BoatAlignNormal : FloatingObjectBase
    {
        [SerializeField, HideInInspector]
#pragma warning disable 414
        int _version = 0;
#pragma warning restore 414

        [Header("Buoyancy Force")]
        [Tooltip("Height offset from transform center to bottom of boat (if any).")]
        public float _bottomH = 0f;
        [Tooltip("Strength of buoyancy force per meter of submersion in water.")]
        public float _buoyancyCoeff = 1.5f;
        [Tooltip("Strength of torque applied to match boat orientation to water normal.")]
        public float _boyancyTorque = 8f;
        [Tooltip("Approximate hydrodynamics of 'surfing' down waves."), Crest.Range(0, 1)]
        public float _accelerateDownhill = 0f;
        [Tooltip("Clamps the buoyancy force to this value. Useful for handling fully submerged objects. Enter 'Infinity' to disable.")]
        public float _maximumBuoyancyForce = Mathf.Infinity;

        [Header("Engine Power")]
        [Tooltip("Vertical offset for where engine force should be applied.")]
        public float _forceHeightOffset = -0.3f;
        public float _enginePower = 11f;
        public float _turnPower = 1.3f;

        [Header("Wave Response")]
        [Tooltip("Width dimension of boat. The larger this value, the more filtered/smooth the wave response will be.")]
        public float _boatWidth = 3f;
        public override float ObjectWidth => _boatWidth;

        [Tooltip("Computes a separate normal based on boat length to get more accurate orientations, at the cost of an extra collision sample.")]
        public bool _useBoatLength = false;
        [Tooltip("Length dimension of boat. Only used if Use Boat Length is enabled."), Predicated("_useBoatLength"), DecoratedField]
        public float _boatLength = 3f;

        [Header("Drag")]
        public float _dragInWaterUp = 3f;
        public float _dragInWaterRight = 2f;
        public float _dragInWaterForward = 1f;

        [Header("Controls")]
        [Tooltip("Allow player keyboard input (legacy).")]
        public bool _playerControlled = false;
        [Tooltip("Used to automatically add throttle input (bias).")]
        public float _throttleBias = 0f;
        [Tooltip("Used to automatically add turning input (bias).")]
        public float _steerBias = 0f;

        [Header("Agent Controls")]
        [Tooltip("If true, use AgentThrottle/AgentSteer values instead of player keyboard.")]
        public bool useAgentControls = true;

        [HideInInspector] public float AgentThrottle = 0f; // [-1..1] from external controller
        [HideInInspector] public float AgentSteer = 0f;    // [-1..1] from external controller

        [Header("Debug")]
        [SerializeField]
        bool _debugDraw = false;

        bool _inWater;
        public override bool InWater => _inWater;

        public override Vector3 Velocity => _rb != null ? _rb.LinearVelocity() : Vector3.zero;

        Rigidbody _rb;

        SampleHeightHelper _sampleHeightHelper = new SampleHeightHelper();
        SampleHeightHelper _sampleHeightHelperLengthwise = new SampleHeightHelper();
        SampleFlowHelper _sampleFlowHelper = new SampleFlowHelper();

        void Start()
        {
            _rb = GetComponent<Rigidbody>();
            if (_rb == null)
            {
                Debug.LogWarning("[BoatAlignNormal] Rigidbody not found on boat. Add a Rigidbody component.");
            }

            // Ensure not accidentally controlled by player when using agent controls:
            if (useAgentControls)
            {
                _playerControlled = false;
            }
        }


        void FixedUpdate()
        {
            if (OceanRenderer.Instance == null)
            {
                return;
            }

            if (_rb == null) return;

            UnityEngine.Profiling.Profiler.BeginSample("BoatAlignNormal.FixedUpdate");

            // Sample surface height + normal + surface velocity
            _sampleHeightHelper.Init(transform.position, _boatWidth, true);
            var height = OceanRenderer.Instance.SeaLevel;
            _sampleHeightHelper.Sample(out Vector3 disp, out var normal, out var waterSurfaceVel);
            height += disp.y;

            if (_debugDraw)
            {
                var surfPos = transform.position;
                surfPos.y = height;
                VisualiseCollisionArea.DebugDrawCross(surfPos, normal, 1f, Color.red);
            }

            // Sample surface flow and incorporate into water velocity
            {
                _sampleFlowHelper.Init(transform.position, _boatWidth);
                _sampleFlowHelper.Sample(out var surfaceFlow);
                waterSurfaceVel += new Vector3(surfaceFlow.x, 0, surfaceFlow.y);
            }

            if (_debugDraw)
            {
                Debug.DrawLine(transform.position + 5f * Vector3.up, transform.position + 5f * Vector3.up + waterSurfaceVel,
                    new Color(1, 1, 1, 0.6f));
            }

            var velocityRelativeToWater = Velocity - waterSurfaceVel;

            // compute depth of bottom (positive => submerged)
            float bottomDepth = height - transform.position.y - _bottomH;
            _inWater = bottomDepth > 0f;
            if (!_inWater)
            {
                UnityEngine.Profiling.Profiler.EndSample();
                return;
            }

            // Buoyancy (cubic with depth for a strong restoring force)
            var buoyancy = -Physics.gravity.normalized * _buoyancyCoeff * bottomDepth * bottomDepth * bottomDepth;
            if (_maximumBuoyancyForce < Mathf.Infinity)
            {
                buoyancy = Vector3.ClampMagnitude(buoyancy, _maximumBuoyancyForce);
            }
            _rb.AddForce(buoyancy, ForceMode.Acceleration);

            // downhill surf force
            if (_accelerateDownhill > 0f)
            {
                _rb.AddForce(new Vector3(normal.x, 0f, normal.z) * -Physics.gravity.y * _accelerateDownhill, ForceMode.Acceleration);
            }

            // apply directional drag relative to water motion
            var forcePosition = _rb.worldCenterOfMass + _forceHeightOffset * Vector3.up;
            _rb.AddForceAtPosition(Vector3.up * Vector3.Dot(Vector3.up, -velocityRelativeToWater) * _dragInWaterUp, forcePosition, ForceMode.Acceleration);
            _rb.AddForceAtPosition(transform.right * Vector3.Dot(transform.right, -velocityRelativeToWater) * _dragInWaterRight, forcePosition, ForceMode.Acceleration);
            _rb.AddForceAtPosition(transform.forward * Vector3.Dot(transform.forward, -velocityRelativeToWater) * _dragInWaterForward, forcePosition, ForceMode.Acceleration);

            // --- Controls: decide source (agent vs player) ---
            float forward = _throttleBias;
            float sideways = _steerBias;

            if (useAgentControls)
            {
                // Agent gives -1..1 values for throttle & steer
                forward += Mathf.Clamp(AgentThrottle, -1f, 1f);
                sideways += Mathf.Clamp(AgentSteer, -1f, 1f);
            }
            else if (_playerControlled)
            {
#if INPUT_SYSTEM_ENABLED
                float rawForward = !Application.isFocused ? 0 : ((Keyboard.current.wKey.isPressed ? 1 : 0) + (Keyboard.current.sKey.isPressed ? -1 : 0));
                float reverseMultiplier = (rawForward < 0f ? -1f : 1f);
                float rawRight = !Application.isFocused ? 0 :
                    ((Keyboard.current.aKey.isPressed ? reverseMultiplier * -1f : 0) + (Keyboard.current.dKey.isPressed ? reverseMultiplier * 1f : 0));
#else
                float rawForward = Input.GetAxis("Vertical");
                float reverseMultiplier = (rawForward < 0f ? -1f : 1f);
                float rawRight = (Input.GetKey(KeyCode.A) ? reverseMultiplier * -1f : 0) + (Input.GetKey(KeyCode.D) ? reverseMultiplier * 1f : 0);
#endif
                forward += rawForward;
                sideways += rawRight;
            }

            // apply engine / turning forces
            _rb.AddForceAtPosition(transform.forward * _enginePower * forward, forcePosition, ForceMode.Acceleration);
            _rb.AddTorque(transform.up * _turnPower * sideways, ForceMode.Acceleration);

            // orientation correction to water normal
            FixedUpdateOrientation(normal);

            UnityEngine.Profiling.Profiler.EndSample();
        }

        // Align to water normal. One normal by default, but can use a separate normal based on boat length vs width.
        void FixedUpdateOrientation(Vector3 normalSideways)
        {
            Vector3 normal = normalSideways, normalLongitudinal = Vector3.up;

            if (_useBoatLength)
            {
                _sampleHeightHelperLengthwise.Init(transform.position, _boatLength, true);
                if (_sampleHeightHelperLengthwise.Sample(out _, out normalLongitudinal))
                {
                    var F = transform.forward;
                    F.y = 0f;
                    F.Normalize();
                    normal -= Vector3.Dot(F, normal) * F;

                    var R = transform.right;
                    R.y = 0f;
                    R.Normalize();
                    normalLongitudinal -= Vector3.Dot(R, normalLongitudinal) * R;
                }
            }

            if (_debugDraw) Debug.DrawLine(transform.position, transform.position + 5f * normal, Color.green);
            if (_debugDraw && _useBoatLength) Debug.DrawLine(transform.position, transform.position + 5f * normalLongitudinal, Color.yellow);

            var torqueWidth = Vector3.Cross(transform.up, normal);
            _rb.AddTorque(torqueWidth * _boyancyTorque, ForceMode.Acceleration);
            if (_useBoatLength)
            {
                var torqueLength = Vector3.Cross(transform.up, normalLongitudinal);
                _rb.AddTorque(torqueLength * _boyancyTorque, ForceMode.Acceleration);
            }
        }
    }
}

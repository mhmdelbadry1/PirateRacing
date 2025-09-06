// BoatAlignNormal — Crest buoyancy + ML-Agents or Player input
// Keeps all Crest floating physics. Input can come from an Agent or keyboard.
// Attach this to your boat prefab with Rigidbody + Crest Ocean setup.

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
        [SerializeField, HideInInspector] int _version = 0;

        [Header("Buoyancy Force")]
        [Tooltip("Height offset from transform center to bottom of boat (if any).")]
        public float _bottomH = 0f;
        [Tooltip("Strength of buoyancy force per meter of submersion in water.")]
        public float _buoyancyCoeff = 1.5f;
        [Tooltip("Strength of torque applied to match boat orientation to water normal.")]
        public float _boyancyTorque = 8f;
        [Tooltip("Approximate hydrodynamics of 'surfing' down waves."), Crest.Range(0, 1)]
        public float _accelerateDownhill = 0f;
        [Tooltip("Clamps the buoyancy force to this value. Enter 'Infinity' to disable.")]
        public float _maximumBuoyancyForce = Mathf.Infinity;

        [Header("Engine Power")]
        [Tooltip("Vertical offset for where engine force should be applied.")]
        public float _forceHeightOffset = -0.3f;
        public float _enginePower = 50f; // Increased from 11f to 50f for better movement
        public float _turnPower = 8f;    // Increased from 1.3f to 8f for better steering

        [Header("Wave Response")]
        [Tooltip("Width dimension of boat.")]
        public float _boatWidth = 3f;
        public override float ObjectWidth => _boatWidth;

        [Tooltip("Use boat length to compute orientation more accurately.")]
        public bool _useBoatLength = false;
        [Predicated("_useBoatLength"), DecoratedField] public float _boatLength = 3f;

        [Header("Drag")]
        public float _dragInWaterUp = 3f;
        public float _dragInWaterRight = 2f;
        public float _dragInWaterForward = 1f;

        [Header("Controls")]
        [Tooltip("Allow player keyboard input (WASD).")]
        public bool _playerControlled = false;
        public float _throttleBias = 0f;
        public float _steerBias = 0f;

        [Header("Agent Controls")]
        [Tooltip("If true, use AgentThrottle/AgentSteer instead of player input.")]
        public bool useAgentControls = true;

        [HideInInspector] public float AgentThrottle = 0f; // [-1..1]
        [HideInInspector] public float AgentSteer = 0f;    // [-1..1]

        [Header("Debug")]
        [SerializeField] bool _debugDraw = false;

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
                Debug.LogWarning("[BoatAlignNormal] Rigidbody missing on boat!");
            }

            // If agent is enabled → force disable player control
            if (useAgentControls)
                _playerControlled = false;
        }

        void FixedUpdate()
        {
            if (OceanRenderer.Instance == null || _rb == null) return;

            UnityEngine.Profiling.Profiler.BeginSample("BoatAlignNormal.FixedUpdate");

            // --- Sample water surface ---
            _sampleHeightHelper.Init(transform.position, _boatWidth, true);
            float height = OceanRenderer.Instance.SeaLevel;
            _sampleHeightHelper.Sample(out Vector3 disp, out var normal, out var waterSurfaceVel);
            height += disp.y;

            _sampleFlowHelper.Init(transform.position, _boatWidth);
            _sampleFlowHelper.Sample(out var surfaceFlow);
            waterSurfaceVel += new Vector3(surfaceFlow.x, 0, surfaceFlow.y);

            var velocityRelativeToWater = Velocity - waterSurfaceVel;

            // --- Depth ---
            float bottomDepth = height - transform.position.y - _bottomH;
            _inWater = bottomDepth > 0f;
            if (!_inWater)
            {
                UnityEngine.Profiling.Profiler.EndSample();
                return;
            }

            // --- Forces ---
            var buoyancy = -Physics.gravity.normalized * _buoyancyCoeff * bottomDepth * bottomDepth * bottomDepth;
            if (_maximumBuoyancyForce < Mathf.Infinity)
                buoyancy = Vector3.ClampMagnitude(buoyancy, _maximumBuoyancyForce);
            _rb.AddForce(buoyancy, ForceMode.Acceleration);

            if (_accelerateDownhill > 0f)
                _rb.AddForce(new Vector3(normal.x, 0f, normal.z) * -Physics.gravity.y * _accelerateDownhill, ForceMode.Acceleration);

            var forcePosition = _rb.worldCenterOfMass + _forceHeightOffset * Vector3.up;
            _rb.AddForceAtPosition(Vector3.up * Vector3.Dot(Vector3.up, -velocityRelativeToWater) * _dragInWaterUp, forcePosition, ForceMode.Acceleration);
            _rb.AddForceAtPosition(transform.right * Vector3.Dot(transform.right, -velocityRelativeToWater) * _dragInWaterRight, forcePosition, ForceMode.Acceleration);
            _rb.AddForceAtPosition(transform.forward * Vector3.Dot(transform.forward, -velocityRelativeToWater) * _dragInWaterForward, forcePosition, ForceMode.Acceleration);

            // --- Controls ---
            float forward = _throttleBias;
            float sideways = _steerBias;

            if (useAgentControls)
            {
                forward += Mathf.Clamp(AgentThrottle, -1f, 1f);
                sideways += Mathf.Clamp(AgentSteer, -1f, 1f);
            }
            else if (_playerControlled)
            {
#if INPUT_SYSTEM_ENABLED
                float rawForward = !Application.isFocused ? 0 :
                    (Keyboard.current.wKey.isPressed ? 1 : 0) + (Keyboard.current.sKey.isPressed ? -1 : 0);
                float reverseMult = (rawForward < 0f ? -1f : 1f);
                float rawRight = !Application.isFocused ? 0 :
                    (Keyboard.current.aKey.isPressed ? reverseMult * -1f : 0) +
                    (Keyboard.current.dKey.isPressed ? reverseMult * 1f : 0);
#else
                float rawForward = Input.GetAxis("Vertical");
                float reverseMult = (rawForward < 0f ? -1f : 1f);
                float rawRight = (Input.GetKey(KeyCode.A) ? reverseMult * -1f : 0) +
                                 (Input.GetKey(KeyCode.D) ? reverseMult * 1f : 0);
#endif
                forward += rawForward;
                sideways += rawRight;
            }

            // --- Engine & Turning ---
            _rb.AddForceAtPosition(transform.forward * _enginePower * forward, forcePosition, ForceMode.Acceleration);
            _rb.AddTorque(transform.up * _turnPower * sideways, ForceMode.Acceleration);

            // --- Align to water ---
            FixedUpdateOrientation(normal);

            UnityEngine.Profiling.Profiler.EndSample();
        }

        void FixedUpdateOrientation(Vector3 normalSideways)
        {
            Vector3 normal = normalSideways, normalLongitudinal = Vector3.up;

            if (_useBoatLength)
            {
                _sampleHeightHelperLengthwise.Init(transform.position, _boatLength, true);
                if (_sampleHeightHelperLengthwise.Sample(out _, out normalLongitudinal))
                {
                    var F = transform.forward; F.y = 0f; F.Normalize();
                    normal -= Vector3.Dot(F, normal) * F;

                    var R = transform.right; R.y = 0f; R.Normalize();
                    normalLongitudinal -= Vector3.Dot(R, normalLongitudinal) * R;
                }
            }

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

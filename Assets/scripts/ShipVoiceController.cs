using UnityEngine;

[DisallowMultipleComponent]
public class ShipVoiceController : MonoBehaviour
{
    [Header("Audio")]
    [Tooltip("المصدر اللي هيشغل الفويسات")]
    public AudioSource voiceSource;

    [Tooltip("أصوات الخبطات (5 تقريبًا)")]
    public AudioClip[] impactClips;

    [Tooltip("تحذير: عائق أمامي واليمين فاضي")]
    public AudioClip[] steerRightClips;

    [Tooltip("تحذير: عائق أمامي والشمال فاضي")]
    public AudioClip[] steerLeftClips;

    [Tooltip("تحذير: اقتراب اصطدام عام (عائق قريب أمامك)")]
    public AudioClip[] nearCollisionClips;

    [Tooltip("تحذير: سفينة/هدف قريب مننا")]
    public AudioClip[] proximityShipClips;

    [Header("Physics & Detection")]
    [Tooltip("RigidBody الخاص بالسفينة (لو موجود هنستخدم سرعته)")]
    public Rigidbody shipRb;

    [Tooltip("المسافة اللي نبحث فيها قدّام عن عوائق")]
    public float forwardCheckDistance = 25f;

    [Tooltip("المسافة الجانبية لفحص كون اليمين/الشمال فاضي")]
    public float sideCheckDistance = 12f;

    [Tooltip("نصف قطر فحص السفن/الأهداف القريبة")]
    public float proximityRadius = 18f;

    [Tooltip("طبقة العوائق (حط فيها الجبال/الجزر/الحواجز… إلخ)")]
    public LayerMask obstacleMask;

    [Tooltip("طبقة/Tag السفن الأخرى لو تحب تستخدم OverlapSphere عليها كلها")]
    public LayerMask shipMask;

    [Header("Thresholds")]
    [Tooltip("حد القوة لتشغيل صوت الخبطة")]
    public float impactMinSpeed = 2.5f;

    [Tooltip("لو العائق أقرب من كده → تشغيل تحذير اقتراب اصطدام")]
    public float nearCollisionDangerDist = 10f;

    [Tooltip("لو العائق قدّام ويمين فاضي هنرشّح يمين. نفس الفكرة للشمال")]
    public float clearSideDotThreshold = 0.2f;

    [Header("Cooldowns (ثواني) لمنع الرغي الكتير)")]
    public float impactCooldown = 0.5f;
    public float steerCooldown = 3.0f;
    public float nearCollisionCooldown = 2.5f;
    public float proximityCooldown = 3.0f;

    [Header("Volumes")]
    [Range(0f, 1f)] public float impactVolume = 1f;
    [Range(0f, 1f)] public float steerVolume = 1f;
    [Range(0f, 1f)] public float nearCollisionVolume = 1f;
    [Range(0f, 1f)] public float proximityVolume = 1f;

    // Timers
    float impactTimer;
    float steerTimer;
    float nearCollisionTimer;
    float proximityTimer;

    void Reset()
    {
        // محاولة تلقائية لجلب AudioSource وRigidbody
        if (!voiceSource) voiceSource = GetComponent<AudioSource>();
        if (!shipRb) shipRb = GetComponentInParent<Rigidbody>() ?? GetComponent<Rigidbody>();
    }

    void Awake()
    {
        if (!voiceSource)
        {
            voiceSource = gameObject.AddComponent<AudioSource>();
            voiceSource.playOnAwake = false;
            voiceSource.spatialBlend = 1f; // ثلاثي الأبعاد
        }
    }

    void Update()
    {
        // نزّل المؤقتات
        float dt = Time.deltaTime;
        impactTimer = Mathf.Max(0, impactTimer - dt);
        steerTimer = Mathf.Max(0, steerTimer - dt);
        nearCollisionTimer = Mathf.Max(0, nearCollisionTimer - dt);
        proximityTimer = Mathf.Max(0, proximityTimer - dt);

        // كشف قدّام
        ForwardChecks();

        // كشف سفن قريبة
        ProximityCheck();
    }

    void ForwardChecks()
    {
        Vector3 origin = transform.position + Vector3.up * 1.0f; // ارفع الراي كاست شوية
        Vector3 fwd = transform.forward;

        if (Physics.Raycast(origin, fwd, out RaycastHit hit, forwardCheckDistance, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            float dist = hit.distance;

            // 1) تحذير اقتراب اصطدام عام
            if (dist <= nearCollisionDangerDist && nearCollisionTimer <= 0f)
            {
                PlayRandom(nearCollisionClips, nearCollisionVolume);
                nearCollisionTimer = nearCollisionCooldown;
            }

            // 2) عائق قدام: اختبر اليمين/الشمال فاضي
            if (steerTimer <= 0f)
            {
                bool rightClear = !Physics.Raycast(origin, transform.right, sideCheckDistance, obstacleMask, QueryTriggerInteraction.Ignore);
                bool leftClear = !Physics.Raycast(origin, -transform.right, sideCheckDistance, obstacleMask, QueryTriggerInteraction.Ignore);

                if (rightClear && !leftClear)
                {
                    // اقترح يمين
                    PlayRandom(steerRightClips, steerVolume);
                    steerTimer = steerCooldown;
                }
                else if (leftClear && !rightClear)
                {
                    // اقترح شمال
                    PlayRandom(steerLeftClips, steerVolume);
                    steerTimer = steerCooldown;
                }
                else if (leftClear && rightClear)
                {
                    // الاتنين فاضيين: اختار واحد (مثلًا يمين)
                    PlayRandom(steerRightClips, steerVolume);
                    steerTimer = steerCooldown;
                }
                // لو الاتنين مش فاضيين، نسكت.
            }
        }
    }

    void ProximityCheck()
    {
        if (proximityTimer > 0f) return;

        Collider[] hits = Physics.OverlapSphere(transform.position, proximityRadius, shipMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            // تجاهل نفسك
            if (hits[i].attachedRigidbody && hits[i].attachedRigidbody == shipRb) continue;
            if (hits[i].transform == transform) continue;

            // أول سفينة قريبة نسمع تحذير
            PlayRandom(proximityShipClips, proximityVolume);
            proximityTimer = proximityCooldown;
            break;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (impactTimer > 0f || impactClips == null || impactClips.Length == 0) return;

        // قوة الخبطة بناءً على السرعة النسبية
        float relSpeed = collision.relativeVelocity.magnitude;
        if (relSpeed < impactMinSpeed) return;

        // حجم الصوت يتدرج حسب القوة
        float vol = Mathf.Clamp01(relSpeed / (impactMinSpeed * 3f)) * impactVolume;

        PlayRandom(impactClips, vol);
        impactTimer = impactCooldown;
    }

    // ===== Helpers =====
    void PlayRandom(AudioClip[] clips, float volume)
    {
        if (clips == null || clips.Length == 0 || voiceSource == null) return;
        int idx = Random.Range(0, clips.Length);
        voiceSource.PlayOneShot(clips[idx], volume);
    }

#if UNITY_EDITOR
    // رسم دوائر/أشعة مساعدة في المشهد
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position + Vector3.up, transform.position + Vector3.up + transform.forward * forwardCheckDistance);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position + Vector3.up, transform.position + Vector3.up + transform.right * sideCheckDistance);
        Gizmos.DrawLine(transform.position + Vector3.up, transform.position + Vector3.up - transform.right * sideCheckDistance);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, proximityRadius);
    }
#endif
}

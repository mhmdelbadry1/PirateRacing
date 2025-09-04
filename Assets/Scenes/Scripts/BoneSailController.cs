using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BoneEntry
{
    public Transform bone;          // assign manually OR auto-filled from rootBone
    public float minAngle = -30f;   // حد لدوران العظمة
    public float maxAngle = 30f;
    [HideInInspector] public float currentAngle = 0f;
}

public class BoneSailController : MonoBehaviour
{
    [Header("Auto-find settings")]
    public Transform rootBone;                // اسحب هنا tgt_root
    public string nameContains = "";          // لو تحب تملأ تلقائياً كل العظام اللي أسمها يحتوي هذه السلسلة، اتركها فارغة لتعطيل الفلتر

    [Header("Manual / Auto-filled bones")]
    public List<BoneEntry> bones = new List<BoneEntry>();

    [Header("Control")]
    public float rotateSpeed = 30f;           // درجات/ثانية
    public KeyCode rotateLeftKey = KeyCode.A;
    public KeyCode rotateRightKey = KeyCode.D;
    public bool controlAllBones = true;       // لو false هتتحكم بعظمة محددة (index)
    public int selectedBoneIndex = 0;

    void Start()
    {
        // لو القائمة فاضية وحطيت rootBone -> عبّيها تلقائياً
        if ((bones == null || bones.Count == 0) && rootBone != null)
        {
            FillBonesFromRoot();
        }

        // init currentAngle من زوايا العظام الحالية
        foreach (var b in bones)
        {
            if (b.bone != null)
            {
                b.currentAngle = GetNormalizedAngle(b.bone.localEulerAngles.x);
            }
        }
    }

    void Update()
    {
        float rotDelta = 0f;
        if (Input.GetKey(rotateRightKey)) rotDelta += rotateSpeed * Time.deltaTime;
        if (Input.GetKey(rotateLeftKey))  rotDelta -= rotateSpeed * Time.deltaTime;

        // اختيار عظمة بالارقام 1..n اختياري
        for (int i = 0; i < bones.Count && i < 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                selectedBoneIndex = i;
                controlAllBones = false;
            }
        }
        if (Input.GetKeyDown(KeyCode.X)) controlAllBones = true; // X => رجوع للتحكم في الكل

        if (Mathf.Approximately(rotDelta, 0f)) return;

        if (controlAllBones)
        {
            for (int i = 0; i < bones.Count; i++) ApplyRotationToBone(bones[i], rotDelta);
        }
        else
        {
            if (selectedBoneIndex >= 0 && selectedBoneIndex < bones.Count)
                ApplyRotationToBone(bones[selectedBoneIndex], rotDelta);
        }
    }

    void ApplyRotationToBone(BoneEntry be, float rotDelta)
    {
        if (be.bone == null) return;
        be.currentAngle = Mathf.Clamp(be.currentAngle + rotDelta, be.minAngle, be.maxAngle);

        Vector3 e = be.bone.localEulerAngles;
        e.x = be.currentAngle; // غالباً X لكن جرّب X أو Z أو Y حسب موديلك
        be.bone.localEulerAngles = e;
    }

    // يجمّع كل العظام تحت rootBone ويضيفها إلى القائمة
    void FillBonesFromRoot()
    {
        bones = new List<BoneEntry>();
        if (rootBone == null) return;
        Transform[] children = rootBone.GetComponentsInChildren<Transform>(true);
        foreach (var t in children)
        {
            if (t == rootBone) continue;
            if (!string.IsNullOrEmpty(nameContains))
            {
                if (t.name.ToLower().Contains(nameContains.ToLower()))
                    bones.Add(new BoneEntry() { bone = t, minAngle = -30f, maxAngle = 30f });
            }
            else
            {
                bones.Add(new BoneEntry() { bone = t, minAngle = -30f, maxAngle = 30f });
            }
        }
    }

    // يحول زاوية 0..360 إلى -180..180 لتسهيل clamp
    float GetNormalizedAngle(float a)
    {
        if (a > 180f) a -= 360f;
        return a;
    }
}

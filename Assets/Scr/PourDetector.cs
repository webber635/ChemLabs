using UnityEngine;
using TMPro;

public class PourDetector : MonoBehaviour
{
    [Header("Identitas & Warna Bahan Kimia")]
    public string chemicalName = "Akuades";
    public Color liquidColor = new Color(0.75f, 0.90f, 1.0f, 0.55f);

    [Header("Kapasitas & Parameter Tuang (ml)")]
    public float maxBottleVolume = 500f;
    public float currentBottleVolume = 500f;
    public float pourRateMlPerSec = 25f;
    [Range(-1f, 1f)]
    public float pourThreshold = -0.3f; // Mulai menuang saat dot product transform.up <= -0.3 (sekitar 107.5°)
    public float maxTiltAngle = 110f;

    [Header("Referensi Visual Cairan Dalam Labu")]
    public Renderer bottleLiquidRenderer;
    public TMP_Text[] bottleLabelTexts;

    [Header("Referensi Mulut Labu & Aliran Tuang")]
    public Transform pourSpout;
    public LineRenderer pourStreamLine;
    public float streamWidth = 0.012f;

    private bool isPouringNow = false;

    void Start()
    {
        SetupStreamVisual();
        SyncLabelTexts();
        UpdateBottleLiquidVisual();
    }

    private void SetupStreamVisual()
    {
        if (pourStreamLine == null)
        {
            pourStreamLine = GetComponent<LineRenderer>();
            if (pourStreamLine == null)
            {
                pourStreamLine = gameObject.AddComponent<LineRenderer>();
            }
        }

        pourStreamLine.positionCount = 10;
        pourStreamLine.startWidth = streamWidth;
        pourStreamLine.endWidth = streamWidth * 0.65f;
        pourStreamLine.useWorldSpace = true;
        pourStreamLine.enabled = false;

        if (pourStreamLine.sharedMaterial == null)
        {
            Shader streamShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            pourStreamLine.material = new Material(streamShader);
        }

        pourStreamLine.startColor = liquidColor;
        pourStreamLine.endColor = liquidColor;
    }

    private void SyncLabelTexts()
    {
        if (bottleLabelTexts == null) return;
        foreach (var txt in bottleLabelTexts)
        {
            if (txt != null) txt.text = chemicalName;
        }
    }

    void Update()
    {
        float upDot = Vector3.Dot(transform.up, Vector3.up);
        float tiltAngle = Vector3.Angle(Vector3.up, transform.up);

        // Perbarui visual cairan setiap frame agar selalu akurat baik saat dipegang, dimiringkan, maupun diletakkan
        UpdateBottleLiquidVisual();

        if (upDot <= pourThreshold && currentBottleVolume > 0.01f)
        {
            float minAngle = Mathf.Acos(Mathf.Clamp(pourThreshold, -1f, 1f)) * Mathf.Rad2Deg;
            float tiltFactor = Mathf.InverseLerp(minAngle, Mathf.Max(minAngle + 1f, maxTiltAngle), tiltAngle);
            float flowRate = Mathf.Lerp(pourRateMlPerSec * 0.4f, pourRateMlPerSec, tiltFactor);
            float amountThisFrame = Mathf.Min(flowRate * Time.deltaTime, currentBottleVolume);

            ProcessPouring(amountThisFrame);
        }
        else if (isPouringNow)
        {
            StopPourStream();
        }
    }

    

    private void ProcessPouring(float amount)
    {
        Transform origin = pourSpout != null ? pourSpout : transform;
        Vector3 startPos = origin.position;
        Vector3 hitPoint = startPos + Vector3.down * 0.45f;

        RaycastHit[] hits = Physics.RaycastAll(startPos, Vector3.down, 1.5f, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        BeakerContainer targetBeaker = null;

        foreach (var h in hits)
        {
            if (h.collider.transform.IsChildOf(transform) || h.collider.transform == transform)
                continue;

            BeakerContainer foundBeaker = h.collider.GetComponent<BeakerContainer>() ??
                                          h.collider.GetComponentInParent<BeakerContainer>();

            if (foundBeaker != null)
            {
                targetBeaker = foundBeaker;

                if (foundBeaker.liquidPivot != null && foundBeaker.liquidPivot.gameObject.activeInHierarchy)
                {
                    Renderer beakerLiqRend = foundBeaker.liquidPivot.GetComponentInChildren<Renderer>();
                    float surfaceY = beakerLiqRend != null ? beakerLiqRend.bounds.max.y : h.point.y - 0.08f;
                    hitPoint = new Vector3(startPos.x, surfaceY, startPos.z);
                }
                else
                {
                    hitPoint = h.point + Vector3.down * 0.12f;
                }
                break;
            }
            else if (!h.collider.isTrigger)
            {
                hitPoint = h.point;
                break;
            }
        }

        currentBottleVolume = Mathf.Max(0f, currentBottleVolume - amount);

        if (targetBeaker != null)
        {
            targetBeaker.AddLiquid(chemicalName, amount, liquidColor);
        }

        UpdateBottleLiquidVisual();
        UpdatePourStreamArc(startPos, hitPoint);
        isPouringNow = true;
    }

    private void UpdatePourStreamArc(Vector3 startPos, Vector3 endPos)
    {
        if (pourStreamLine == null) return;

        pourStreamLine.enabled = true;
        pourStreamLine.startColor = liquidColor;
        pourStreamLine.endColor = liquidColor;

        if (pourStreamLine.material != null)
        {
            pourStreamLine.material.color = liquidColor;
        }

        Vector3 outwardDir = (startPos - transform.position);
        outwardDir.y = 0f;
        if (outwardDir.sqrMagnitude > 0.0001f)
            outwardDir.Normalize();

        Vector3 arcTarget = new Vector3(endPos.x + outwardDir.x * 0.02f, endPos.y, endPos.z + outwardDir.z * 0.02f);

        int count = pourStreamLine.positionCount;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)(count - 1);
            float horizontalT = 1f - Mathf.Pow(1f - t, 1.6f);
            Vector3 pos = Vector3.Lerp(startPos, arcTarget, horizontalT);
            pos.y = Mathf.Lerp(startPos.y, arcTarget.y, t * t);
            pourStreamLine.SetPosition(i, pos);
        }
    }

    private void StopPourStream()
    {
        isPouringNow = false;
        if (pourStreamLine != null)
        {
            pourStreamLine.enabled = false;
        }
    }

    public void UpdateBottleLiquidVisual()
    {
        if (bottleLiquidRenderer == null) return;

        bool hasLiquid = currentBottleVolume > 0.1f;
        bottleLiquidRenderer.gameObject.SetActive(hasLiquid);
        if (!hasLiquid) return;

        float fillRatio = Mathf.Clamp01(currentBottleVolume / maxBottleVolume);

        // Hitung min Y dan max Y secara presisi dari sudut-sudut lokal mesh ke World Space
        // (mencegah bug bounds terlambat update saat botol di-parent ke tangan/holdPoint)
        float minY = float.MaxValue;
        float maxY = float.MinValue;

        MeshFilter mf = bottleLiquidRenderer.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            Bounds localB = mf.sharedMesh.bounds;
            Vector3 c = localB.center;
            Vector3 e = localB.extents;
            Transform t = bottleLiquidRenderer.transform;

            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 worldCorner = t.TransformPoint(c + new Vector3(e.x * x, e.y * y, e.z * z));
                        if (worldCorner.y < minY) minY = worldCorner.y;
                        if (worldCorner.y > maxY) maxY = worldCorner.y;
                    }
                }
            }
        }
        else
        {
            Bounds worldBounds = bottleLiquidRenderer.bounds;
            minY = worldBounds.min.y;
            maxY = worldBounds.max.y;
        }

        float worldFillY = Mathf.Lerp(minY, maxY, fillRatio);

        Material mat = bottleLiquidRenderer.material;
        mat.SetFloat("_FillPlaneY", worldFillY);
        mat.SetColor("_BaseColor", liquidColor);
        mat.SetColor("_SurfaceColor", Color.Lerp(liquidColor, Color.white, 0.25f));
    }

    public void RefillBottle()
    {
        currentBottleVolume = maxBottleVolume;
        UpdateBottleLiquidVisual();
    }
}
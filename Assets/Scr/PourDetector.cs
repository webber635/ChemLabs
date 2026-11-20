using UnityEngine;

public class PourDetector : MonoBehaviour
{
    [Header("Identitas Bahan Kimia")]
    public string chemicalName = "Aquades"; // Samakan persis dengan nama di resep!
    public Color liquidColor = new Color(0.8f, 0.95f, 1f, 0.85f);

    [Header("Pengaturan Penuangan")]
    public Transform pourPoint;
    [Range(-1f, 1f)]
    public float pourThreshold = 0.1f;
    public float basePourRate = 15f;

    void Update()
    {
        float tiltDot = Vector3.Dot(transform.up, Vector3.up);

        if (tiltDot < pourThreshold)
        {
            float intensity = Mathf.InverseLerp(pourThreshold, -1f, tiltDot);
            float dynamicRate = basePourRate * (0.5f + intensity);
            PourLiquid(dynamicRate);
        }
    }

    private void PourLiquid(float rate)
    {
        Vector3 origin = pourPoint != null ? pourPoint.position : transform.position;
        Debug.DrawRay(origin, Vector3.down * 1.0f, liquidColor);

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 1.0f))
        {
            if (hit.collider.TryGetComponent(out BeakerContainer beaker))
            {
                beaker.AddLiquid(chemicalName, rate * Time.deltaTime, liquidColor);
            }
        }
    }
}
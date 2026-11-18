using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class AnalyzerSlot : MonoBehaviour
{
    private XRSocketInteractor socketInteractor;

    void Awake()
    {
        socketInteractor = GetComponent<XRSocketInteractor>();
    }

    void OnEnable()
    {
        socketInteractor.selectEntered.AddListener(OnSamplePlaced);
    }

    void OnDisable()
    {
        socketInteractor.selectEntered.RemoveListener(OnSamplePlaced);
    }

    private void OnSamplePlaced(SelectEnterEventArgs args)
    {
        GameObject insertedObj = args.interactableObject.transform.gameObject;
        if (insertedObj.TryGetComponent(out BeakerContainer beaker))
        {
            Mission1Manager.Instance.CheckBeakerSample(beaker);
        }
    }
}
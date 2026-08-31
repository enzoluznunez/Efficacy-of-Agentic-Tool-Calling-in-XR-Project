using Oculus.Interaction;
using Oculus.Interaction.Grab;
using Oculus.Interaction.GrabAPI;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using UnityEngine;

[RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
public class SortLineProxy : MonoBehaviour
{
    public CreateSheet sheet;
    public bool columns;
    public int line;

    private Grabbable _grabbable;

    public bool IsGrabbed => _grabbable != null && _grabbable.SelectingPointsCount > 0;

    public float Coord => columns ? transform.localPosition.x : transform.localPosition.z;

    public static SortLineProxy Create(CreateSheet owner, bool columns, int line, float height)
    {
        if (owner == null) return null;

        float cell = owner.CellSize;
        if (cell <= 1e-6f || height <= 1e-6f) return null;

        int min = columns ? owner.colMin : owner.rowMin;
        int max = columns ? owner.colMax : owner.rowMax;
        if (line < min || line > max) return null;

        int perpMin = columns ? owner.rowMin : owner.colMin;
        int perpMax = columns ? owner.rowMax : owner.colMax;
        float span = (perpMax - perpMin + 1) * cell;

        GameObject go = new GameObject($"SortLine_{(columns ? "Col" : "Row")}_{line}");
        go.transform.SetParent(owner.transform, false);

        float coord = owner.LineCoord(columns, line);
        go.transform.localPosition = columns
            ? new Vector3(coord, height * 0.5f, 0f)
            : new Vector3(0f, height * 0.5f, coord);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        SortLineProxy proxy = go.AddComponent<SortLineProxy>();
        proxy.sheet = owner;
        proxy.columns = columns;
        proxy.line = line;
        proxy.Build(cell, span, height, (min - line) * cell, (max - line) * cell);
        return proxy;
    }

    private void Build(float cell, float span, float height, float back, float forward)
    {
        BoxCollider box = GetComponent<BoxCollider>();
        box.center = Vector3.zero;
        box.size = columns
            ? new Vector3(cell, height, span)
            : new Vector3(span, height, cell);

        Rigidbody body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        OneGrabTranslateTransformer slide = gameObject.AddComponent<OneGrabTranslateTransformer>();
        slide.InjectOptionalConstraints(Limits(back, forward));

        _grabbable = gameObject.AddComponent<Grabbable>();
        _grabbable.InjectOptionalRigidbody(body);
        _grabbable.InjectOptionalThrowWhenUnselected(false);
        _grabbable.InjectOptionalKinematicWhileSelected(true);
        _grabbable.MaxGrabPoints = 1;
        _grabbable.InjectOptionalOneGrabTransformer(slide);

        GrabbingRule pinch = new GrabbingRule(
            HandFingerFlags.Thumb | HandFingerFlags.Index, GrabbingRule.DefaultPinchRule);

        HandGrabInteractable handGrab = gameObject.AddComponent<HandGrabInteractable>();
        handGrab.InjectAllHandGrabInteractable(GrabTypeFlags.Pinch, body,
            pinch, GrabbingRule.DefaultPalmRule);
        handGrab.InjectOptionalPointableElement(_grabbable);
    }

    private OneGrabTranslateTransformer.OneGrabTranslateConstraints Limits(float back, float forward)
    {
        var limits = new OneGrabTranslateTransformer.OneGrabTranslateConstraints
        {
            ConstraintsAreRelative = true,
            MinX = Pin(0f),
            MaxX = Pin(0f),
            MinY = Pin(0f),
            MaxY = Pin(0f),
            MinZ = Pin(0f),
            MaxZ = Pin(0f)
        };

        if (columns) { limits.MinX = Pin(back); limits.MaxX = Pin(forward); }
        else { limits.MinZ = Pin(back); limits.MaxZ = Pin(forward); }

        return limits;
    }

    private static FloatConstraint Pin(float value) => new FloatConstraint { Constrain = true, Value = value };
}

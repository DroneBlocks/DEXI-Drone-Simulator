using UnityEngine;

public class FlattenMeshChildren : MonoBehaviour
{
    [ContextMenu("Flatten Mesh Children")]
    void FlattenMeshes()
    {
        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);
        foreach (MeshRenderer r in renderers)
            if (r.transform != transform)
                r.transform.SetParent(transform, true);
    }
}
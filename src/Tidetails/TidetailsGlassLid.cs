using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using UnityEngine;

namespace Tidetails
{
  internal class TidetailsGlassLid : BaseComponent, IFinishedStateListener
  {
    private static readonly string FinishedModelName = "#Finished";

    private static readonly string[] ShaderNames = { "Sprites/Default", "UI/Default" };

    private static readonly Color GlassColor = new Color(0.75f, 0.92f, 1f, 0.25f);

    private static readonly float DomeHeightRatio = 0.3f;

    private static readonly int DomeRings = 6;

    private static readonly int DomeSegments = 24;

    private static readonly float RimInset = 0.97f;

    private static Material _sharedMaterial;

    private static Mesh _domeMesh;

    private GameObject _lid;

    public void OnEnterFinishedState()
    {
      if (_lid == null)
      {
        CreateLid();
      }
    }

    public void OnExitFinishedState()
    {
    }

    private void CreateLid()
    {
      Transform finishedModel = FindChild(Transform, FinishedModelName);
      Material material = GetMaterial();
      if (finishedModel == null || material == null)
      {
        return;
      }
      Renderer[] renderers = finishedModel.GetComponentsInChildren<Renderer>(true);
      if (renderers.Length == 0)
      {
        return;
      }
      Bounds bounds = renderers[0].bounds;
      for (int i = 1; i < renderers.Length; i++)
      {
        bounds.Encapsulate(renderers[i].bounds);
      }
      _lid = new GameObject("TidetailsGlassLid");
      _lid.AddComponent<MeshFilter>().sharedMesh = GetDomeMesh();
      _lid.AddComponent<MeshRenderer>().sharedMaterial = material;
      float diameter = Mathf.Min(bounds.size.x, bounds.size.z) * RimInset;
      _lid.transform.SetParent(finishedModel, false);
      _lid.transform.position = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
      _lid.transform.rotation = Quaternion.identity;
      Vector3 parentScale = finishedModel.lossyScale;
      _lid.transform.localScale = new Vector3(diameter / parentScale.x, diameter * DomeHeightRatio / parentScale.y, diameter / parentScale.z);
    }

    private static Mesh GetDomeMesh()
    {
      if (_domeMesh != null)
      {
        return _domeMesh;
      }
      Vector3[] vertices = new Vector3[(DomeRings + 1) * (DomeSegments + 1)];
      for (int ring = 0; ring <= DomeRings; ring++)
      {
        float polar = ring * Mathf.PI * 0.5f / DomeRings;
        float radius = 0.5f * Mathf.Cos(polar);
        float height = Mathf.Sin(polar);
        for (int segment = 0; segment <= DomeSegments; segment++)
        {
          float azimuth = segment * Mathf.PI * 2f / DomeSegments;
          vertices[ring * (DomeSegments + 1) + segment] = new Vector3(radius * Mathf.Cos(azimuth), height, radius * Mathf.Sin(azimuth));
        }
      }
      int[] triangles = new int[DomeRings * DomeSegments * 6];
      int t = 0;
      for (int ring = 0; ring < DomeRings; ring++)
      {
        for (int segment = 0; segment < DomeSegments; segment++)
        {
          int a = ring * (DomeSegments + 1) + segment;
          int b = a + DomeSegments + 1;
          triangles[t++] = a;
          triangles[t++] = b;
          triangles[t++] = a + 1;
          triangles[t++] = a + 1;
          triangles[t++] = b;
          triangles[t++] = b + 1;
        }
      }
      _domeMesh = new Mesh { name = "TidetailsGlassDome", vertices = vertices, triangles = triangles };
      _domeMesh.RecalculateNormals();
      _domeMesh.RecalculateBounds();
      return _domeMesh;
    }

    private static Material GetMaterial()
    {
      if (_sharedMaterial != null)
      {
        return _sharedMaterial;
      }
      foreach (string shaderName in ShaderNames)
      {
        Shader shader = Shader.Find(shaderName);
        if (shader != null)
        {
          _sharedMaterial = new Material(shader) { color = GlassColor };
          return _sharedMaterial;
        }
      }
      Debug.LogWarning("[Tidetails] No transparent shader found for glass lids.");
      return null;
    }

    private static Transform FindChild(Transform parent, string name)
    {
      foreach (Transform child in parent)
      {
        if (child.name == name)
        {
          return child;
        }
        Transform found = FindChild(child, name);
        if (found != null)
        {
          return found;
        }
      }
      return null;
    }
  }
}

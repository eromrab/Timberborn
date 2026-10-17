using System.Collections.Generic;
using Timberborn.Common;
using Timberborn.PrefabOptimization;
using UnityEngine;

namespace Wetlands
{
  internal static class WetlandBlockMaterials
  {
    private static readonly Color WetDark = new Color(0.07f, 0.20f, 0.05f, 1f);

    private static readonly Color WetGrass = new Color(0.28f, 0.58f, 0.14f, 1f);

    private static readonly Color DryCrack = new Color(0.16f, 0.28f, 0.08f, 1f);

    private static readonly Color DryDirt = new Color(0.34f, 0.48f, 0.16f, 1f);

    private static readonly string[] AlbedoProperties =
    {
      "_BaseAlbedoTex", "_SplatAlbedoTex", "_DetailAlbedoTex"
    };

    private readonly struct TintKey
    {
      public TintKey(Material material, bool wet, int step)
      {
        Material = material;
        Wet = wet;
        Step = step;
      }

      public Material Material { get; }

      public bool Wet { get; }

      public int Step { get; }
    }

    private static readonly Dictionary<TintKey, Material> _tinted = new Dictionary<TintKey, Material>();

    private struct ClipVert
    {
      public Vector3 Position;
      public Vector3 Normal;
      public Vector4 Tangent;
      public Color32 Color;
      public Vector4 Uv0;
      public Vector4 Uv1;
      public Vector4 Uv2;
    }

    public static IntermediateMesh Apply(IntermediateMesh source, Vector3Int coordinates)
    {
      if (source?.Submeshes == null || source.Vertices == null)
      {
        return source;
      }
      Vector3Int east = coordinates + new Vector3Int(1, 0, 0);
      Vector3Int north = coordinates + new Vector3Int(0, 1, 0);
      Vector3Int northEast = coordinates + new Vector3Int(1, 1, 0);
      bool southWest = WetlandService.HasTileAtIndex(coordinates);
      bool southEast = WetlandService.HasTileAtIndex(east);
      bool northWest = WetlandService.HasTileAtIndex(north);
      bool northEastTile = WetlandService.HasTileAtIndex(northEast);
      if (!southWest && !southEast && !northWest && !northEastTile)
      {
        return source;
      }
      if (southWest && southEast && northWest && northEastTile)
      {
        bool wet = WetlandService.IsWetAtIndex(coordinates);
        if (wet == WetlandService.IsWetAtIndex(east)
            && wet == WetlandService.IsWetAtIndex(north)
            && wet == WetlandService.IsWetAtIndex(northEast))
        {
          return TintWhole(source, wet);
        }
      }
      List<ClipVert> verts = new List<ClipVert>();
      List<(NullableKey<Material>, int[])> rebuilt = new List<(NullableKey<Material>, int[])>();
      bool anyMarsh = false;
      for (int s = 0; s < source.Submeshes.Length; s++)
      {
        int[] indices = source.Submeshes[s].Item2;
        Material original = source.Submeshes[s].Item1.Key;
        if (indices == null || indices.Length < 3)
        {
          rebuilt.Add(source.Submeshes[s]);
          continue;
        }
        List<int> plain = new List<int>();
        List<int> wet = new List<int>();
        List<int> dry = new List<int>();
        List<ClipVert> triangle = new List<ClipVert>(3);
        List<ClipVert> negX = new List<ClipVert>();
        List<ClipVert> posX = new List<ClipVert>();
        List<ClipVert> q00 = new List<ClipVert>();
        List<ClipVert> q01 = new List<ClipVert>();
        List<ClipVert> q10 = new List<ClipVert>();
        List<ClipVert> q11 = new List<ClipVert>();
        for (int i = 0; i + 2 < indices.Length; i += 3)
        {
          triangle.Clear();
          triangle.Add(Read(source, indices[i]));
          triangle.Add(Read(source, indices[i + 1]));
          triangle.Add(Read(source, indices[i + 2]));
          Split(triangle, 0, 0f, negX, posX);
          Split(negX, 2, 0f, q00, q01);
          Split(posX, 2, 0f, q10, q11);
          Emit(q00, coordinates, original, verts, plain, wet, dry);
          Emit(q10, coordinates, original, verts, plain, wet, dry);
          Emit(q01, coordinates, original, verts, plain, wet, dry);
          Emit(q11, coordinates, original, verts, plain, wet, dry);
        }
        if (wet.Count > 0 || dry.Count > 0)
        {
          anyMarsh = true;
        }
        if (plain.Count > 0)
        {
          rebuilt.Add((source.Submeshes[s].Item1, plain.ToArray()));
        }
        if (wet.Count > 0)
        {
          rebuilt.Add((new NullableKey<Material>(original == null ? null : Tinted(original, true)), wet.ToArray()));
        }
        if (dry.Count > 0)
        {
          rebuilt.Add((new NullableKey<Material>(original == null ? null : Tinted(original, false)), dry.ToArray()));
        }
      }
      if (!anyMarsh)
      {
        return source;
      }
      return Pack(verts, rebuilt);
    }

    private static IntermediateMesh TintWhole(IntermediateMesh source, bool wet)
    {
      (NullableKey<Material>, int[])[] submeshes = source.Submeshes;
      (NullableKey<Material>, int[])[] tinted = new (NullableKey<Material>, int[])[submeshes.Length];
      for (int i = 0; i < submeshes.Length; i++)
      {
        Material original = submeshes[i].Item1.Key;
        tinted[i] = (new NullableKey<Material>(original == null ? null : Tinted(original, wet)), submeshes[i].Item2);
      }
      return new IntermediateMesh
      {
        VertexCount = source.VertexCount,
        Vertices = source.Vertices,
        Normals = source.Normals,
        Tangents = source.Tangents,
        Colors = source.Colors,
        UV0 = source.UV0,
        UV1 = source.UV1 == null ? null : (Vector4[])source.UV1.Clone(),
        UV2 = source.UV2,
        Submeshes = tinted
      };
    }

    private static void Emit(List<ClipVert> polygon, Vector3Int coordinates, Material original, List<ClipVert> verts, List<int> plain, List<int> wet, List<int> dry)
    {
      if (polygon.Count < 3)
      {
        return;
      }
      Vector3 mid = Vector3.zero;
      for (int i = 0; i < polygon.Count; i++)
      {
        mid += polygon[i].Position;
      }
      mid /= polygon.Count;
      Vector3Int cell = new Vector3Int(
        Mathf.FloorToInt(mid.x + coordinates.x + 1f),
        Mathf.FloorToInt(mid.z + coordinates.y + 1f),
        coordinates.z);
      List<int> target = plain;
      if (WetlandService.HasTileAtIndex(cell))
      {
        target = WetlandService.IsWetAtIndex(cell) ? wet : dry;
      }
      int first = verts.Count;
      verts.Add(polygon[0]);
      for (int i = 1; i + 1 < polygon.Count; i++)
      {
        Vector3 edgeA = polygon[i].Position - polygon[0].Position;
        Vector3 edgeB = polygon[i + 1].Position - polygon[0].Position;
        if (Vector3.Cross(edgeA, edgeB).sqrMagnitude < 0.0000001f)
        {
          continue;
        }
        verts.Add(polygon[i]);
        verts.Add(polygon[i + 1]);
        target.Add(first);
        target.Add(verts.Count - 2);
        target.Add(verts.Count - 1);
      }
    }

    private static void Split(List<ClipVert> input, int axis, float plane, List<ClipVert> negative, List<ClipVert> positive)
    {
      negative.Clear();
      positive.Clear();
      int count = input.Count;
      if (count == 0)
      {
        return;
      }
      for (int i = 0; i < count; i++)
      {
        ClipVert current = input[i];
        ClipVert next = input[(i + 1) % count];
        float currentValue = axis == 0 ? current.Position.x : current.Position.z;
        float nextValue = axis == 0 ? next.Position.x : next.Position.z;
        bool currentPositive = currentValue >= plane;
        if (currentPositive)
        {
          positive.Add(current);
        }
        else
        {
          negative.Add(current);
        }
        if (currentPositive == (nextValue >= plane))
        {
          continue;
        }
        float span = nextValue - currentValue;
        float t = Mathf.Abs(span) < 0.00001f ? 0.5f : (plane - currentValue) / span;
        ClipVert mid = Lerp(current, next, Mathf.Clamp01(t));
        negative.Add(mid);
        positive.Add(mid);
      }
    }

    private static ClipVert Lerp(ClipVert a, ClipVert b, float t)
    {
      return new ClipVert
      {
        Position = Vector3.Lerp(a.Position, b.Position, t),
        Normal = Vector3.Lerp(a.Normal, b.Normal, t),
        Tangent = Vector4.Lerp(a.Tangent, b.Tangent, t),
        Color = new Color32(
          (byte)Mathf.RoundToInt(a.Color.r + (b.Color.r - a.Color.r) * t),
          (byte)Mathf.RoundToInt(a.Color.g + (b.Color.g - a.Color.g) * t),
          (byte)Mathf.RoundToInt(a.Color.b + (b.Color.b - a.Color.b) * t),
          (byte)Mathf.RoundToInt(a.Color.a + (b.Color.a - a.Color.a) * t)),
        Uv0 = Vector4.Lerp(a.Uv0, b.Uv0, t),
        Uv1 = Vector4.Lerp(a.Uv1, b.Uv1, t),
        Uv2 = Vector4.Lerp(a.Uv2, b.Uv2, t)
      };
    }

    private static ClipVert Read(IntermediateMesh source, int index)
    {
      ClipVert vert = new ClipVert();
      if (source.Vertices != null && (uint)index < (uint)source.Vertices.Length)
      {
        vert.Position = source.Vertices[index];
      }
      if (source.Normals != null && (uint)index < (uint)source.Normals.Length)
      {
        vert.Normal = source.Normals[index];
      }
      if (source.Tangents != null && (uint)index < (uint)source.Tangents.Length)
      {
        vert.Tangent = source.Tangents[index];
      }
      if (source.Colors != null && (uint)index < (uint)source.Colors.Length)
      {
        vert.Color = source.Colors[index];
      }
      else
      {
        vert.Color = new Color32(255, 255, 255, 255);
      }
      if (source.UV0 != null && (uint)index < (uint)source.UV0.Length)
      {
        vert.Uv0 = source.UV0[index];
      }
      if (source.UV1 != null && (uint)index < (uint)source.UV1.Length)
      {
        vert.Uv1 = source.UV1[index];
      }
      if (source.UV2 != null && (uint)index < (uint)source.UV2.Length)
      {
        vert.Uv2 = source.UV2[index];
      }
      return vert;
    }

    private static IntermediateMesh Pack(List<ClipVert> verts, List<(NullableKey<Material>, int[])> submeshes)
    {
      int count = verts.Count;
      Vector3[] vertices = new Vector3[count];
      Vector3[] normals = new Vector3[count];
      Vector4[] tangents = new Vector4[count];
      Color32[] colors = new Color32[count];
      Vector4[] uv0 = new Vector4[count];
      Vector4[] uv1 = new Vector4[count];
      Vector4[] uv2 = new Vector4[count];
      for (int i = 0; i < count; i++)
      {
        ClipVert vert = verts[i];
        vertices[i] = vert.Position;
        normals[i] = vert.Normal;
        tangents[i] = vert.Tangent;
        colors[i] = vert.Color;
        uv0[i] = vert.Uv0;
        uv1[i] = vert.Uv1;
        uv2[i] = vert.Uv2;
      }
      return new IntermediateMesh
      {
        VertexCount = count,
        Vertices = vertices,
        Normals = normals,
        Tangents = tangents,
        Colors = colors,
        UV0 = uv0,
        UV1 = uv1,
        UV2 = uv2,
        Submeshes = submeshes.ToArray()
      };
    }

    private static Material Tinted(Material original, bool wet)
    {
      TintKey key = new TintKey(original, wet, 0);
      if (_tinted.TryGetValue(key, out Material existing))
      {
        return existing;
      }
      Material clone = new Material(original)
      {
        name = wet ? "Wetlands.WetDirt" : "Wetlands.DryDirt"
      };
      if (clone.HasProperty("_SplatAlbedoColor"))
      {
        clone.SetColor("_SplatAlbedoColor", Color.white);
      }
      Color dark = wet ? WetDark : DryCrack;
      Color light = wet ? WetGrass : DryDirt;
      foreach (string property in AlbedoProperties)
      {
        if (!clone.HasProperty(property))
        {
          continue;
        }
        Texture source = original.GetTexture(property);
        if (source == null || source.width < 8 || source.height < 8)
        {
          continue;
        }
        Texture2D recoloured = Recolor(source, dark, light);
        clone.SetTexture(property, recoloured);
      }
      _tinted[key] = clone;
      return clone;
    }

    private static Texture2D Recolor(Texture source, Color dark, Color light)
    {
      Texture2D readable = MakeReadable(source);
      Color[] pixels = readable.GetPixels();
      float min = 1f;
      float max = 0f;
      for (int i = 0; i < pixels.Length; i++)
      {
        float lum = pixels[i].grayscale;
        if (lum < min)
        {
          min = lum;
        }
        if (lum > max)
        {
          max = lum;
        }
      }
      float span = Mathf.Max(0.02f, max - min);
      for (int i = 0; i < pixels.Length; i++)
      {
        Color src = pixels[i];
        float t = Mathf.Clamp01((src.grayscale - min) / span);
        float blade = Mathf.Lerp(0.42f, 1.35f, t);
        Color green = Color.Lerp(dark, light, t);
        pixels[i] = new Color(
          Mathf.Clamp01(green.r * blade),
          Mathf.Clamp01(green.g * blade),
          Mathf.Clamp01(green.b * blade),
          src.a);
      }
      Texture2D result = new Texture2D(readable.width, readable.height, TextureFormat.RGBA32, true)
      {
        wrapMode = readable.wrapMode,
        filterMode = readable.filterMode,
        anisoLevel = readable.anisoLevel,
        name = "Wetlands.DirtAlbedo"
      };
      result.SetPixels(pixels);
      result.Apply(true, true);
      return result;
    }

    internal static Texture2D MakeReadable(Texture source)
    {
      if (source is Texture2D texture2D)
      {
        try
        {
          if (texture2D.isReadable)
          {
            return texture2D;
          }
        }
        catch
        {
        }
      }
      RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
      Graphics.Blit(source, rt);
      RenderTexture previous = RenderTexture.active;
      RenderTexture.active = rt;
      Texture2D copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
      copy.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0);
      copy.Apply(false, false);
      RenderTexture.active = previous;
      RenderTexture.ReleaseTemporary(rt);
      return copy;
    }
  }
}

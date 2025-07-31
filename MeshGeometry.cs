using System.Collections.Generic;
using UnityEngine;


public class Vertex
{
    public readonly int Index;

    public Particle Particle;

    private Vector3 Position; 

    public List<Edge> IncidentEdges = new List<Edge>();
    public List<Triangle> IncidentTriangles = new List<Triangle>();

    public Vertex(Vector3 pos, int idx)
    {
        Position = pos;
        Index = idx;
    }
}

public class Edge
{
    public Vertex V0, V1;
    public Triangle T0, T1;               

    public Edge(Vertex v0, Vertex v1)
    {
        V0 = v0; V1 = v1;
        v0.IncidentEdges.Add(this);
        v1.IncidentEdges.Add(this);
    }

    public Vertex Other(Vertex v) => v == V0 ? V1 : V0;
    public Triangle Other(Triangle t) => t == T0 ? T1 : T0;
}

public class Triangle
{
    public Vertex A, B, C;
    public Edge E0, E1, E2;

    public Triangle(Vertex a, Vertex b, Vertex c, Edge e0, Edge e1, Edge e2)
    {
        A = a; B = b; C = c;
        E0 = e0; E1 = e1; E2 = e2;

        a.IncidentTriangles.Add(this);
        b.IncidentTriangles.Add(this);
        c.IncidentTriangles.Add(this);

        if (e0.T0 == null) e0.T0 = this; else e0.T1 = this;
        if (e1.T0 == null) e1.T0 = this; else e1.T1 = this;
        if (e2.T0 == null) e2.T0 = this; else e2.T1 = this;
    }

    public IEnumerable<Triangle> AdjacentTriangles()
    {
        if (E0.Other(this) != null) yield return E0.Other(this);
        if (E1.Other(this) != null) yield return E1.Other(this);
        if (E2.Other(this) != null) yield return E2.Other(this);
    }
}



public class MeshGeometry
{
    public Transform transform;
    private readonly List<Vertex> vertices = new List<Vertex>();
    private readonly List<Edge> edges = new List<Edge>();
    private readonly List<Triangle> triangles = new List<Triangle>();
    private readonly Dictionary<(int, int), Edge> edgeMap = new Dictionary<(int, int), Edge>();

    public Vertex AddVertex(Vector3 pos)
    {
        var v = new Vertex(pos, vertices.Count);
        vertices.Add(v);
        return v;
    }

    public Edge GetOrCreateEdge(Vertex a, Vertex b)
    {
        var key = a.Index < b.Index ? (a.Index, b.Index) : (b.Index, a.Index);
        if (edgeMap.TryGetValue(key, out var e))
            return e;

        e = new Edge(a, b);
        edges.Add(e);
        edgeMap[key] = e;
        return e;
    }

    public Triangle AddTriangle(Vertex a, Vertex b, Vertex c)
    {
        var e0 = GetOrCreateEdge(a, b);
        var e1 = GetOrCreateEdge(b, c);
        var e2 = GetOrCreateEdge(c, a);

        var tri = new Triangle(a, b, c, e0, e1, e2);
        triangles.Add(tri);
        return tri;
    }

    public Mesh BuildUnityMesh()
    {
        var mesh = new Mesh { name = "ProceduralMesh" };

        mesh.vertices = vertices.ConvertAll(v => transform.InverseTransformPoint(v.Particle.positionX)).ToArray();

        var indices = new int[triangles.Count * 3];
        for (int i = 0; i < triangles.Count; i++)
        {
            var t = triangles[i];
            indices[3 * i + 0] = t.A.Index;
            indices[3 * i + 1] = t.B.Index;
            indices[3 * i + 2] = t.C.Index;
        }
        mesh.triangles = indices;

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    public Mesh BuildUnityMesh(ICollection<Edge> tornEdges)
    {
        var mesh = new Mesh { name = "ProceduralMesh" };

        mesh.vertices = vertices.ConvertAll(v => transform.InverseTransformPoint(v.Particle.positionX)).ToArray();

        var indexList = new List<int>(triangles.Count * 3);
        foreach (var t in triangles)
        {
            if (tornEdges.Contains(t.E0) ||
                tornEdges.Contains(t.E1) ||
                tornEdges.Contains(t.E2))
                continue;

            indexList.Add(t.A.Index);
            indexList.Add(t.B.Index);
            indexList.Add(t.C.Index);
        }
        mesh.triangles = indexList.ToArray();

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }
}

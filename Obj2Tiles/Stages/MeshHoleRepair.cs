using MeshDecimatorCore.Math;
using Mesh = MeshDecimatorCore.Mesh;

namespace Obj2Tiles.Stages;

/// <summary>
/// Detects and closes a very specific, narrow class of decimation defect: a single missing
/// triangle whose three vertices are all still in use by the surrounding mesh (each pairwise
/// edge is owned by exactly one other, different triangle), but no triangle joins all three.
///
/// This only ever adds a triangle built from vertices that already exist and are already used
/// elsewhere in the mesh -- it never creates a new vertex and never touches any other
/// triangle's own attribute (UV/normal/color) data, since one vertex index already carries a
/// complete position+UV+normal bundle in this pipeline. That keeps the fix scoped to exactly
/// the missing geometry, with no way for it to corrupt texturing elsewhere.
///
/// A candidate is only repaired when several independent geometric checks agree it is a
/// legitimate, simple single-triangle gap (consistent winding, size and normal comparable to
/// its neighbors, all neighbors on the same sub-mesh/material). Anything that fails a check is
/// left alone and reported, rather than guessed at.
/// </summary>
public static class MeshHoleRepair
{
    private readonly struct Edge : IEquatable<Edge>
    {
        public readonly int A, B;

        private Edge(int a, int b)
        {
            A = a;
            B = b;
        }

        public static Edge Of(int a, int b) => a < b ? new Edge(a, b) : new Edge(b, a);

        public bool Equals(Edge other) => A == other.A && B == other.B;
        public override bool Equals(object? obj) => obj is Edge other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(A, B);
    }

    /// <summary>
    /// Finds and fills single-triangle holes in place. Returns the number of triangles added.
    /// </summary>
    public static int CloseSingleTriangleHoles(Mesh mesh)
    {
        var vertices = mesh.Vertices;
        var subMeshIndices = mesh.GetSubMeshIndices();

        // Flatten every triangle across all sub-meshes so edge ownership can be resolved
        // mesh-wide, remembering which sub-mesh and which corner order each one came from.
        var triSubMesh = new List<int>();
        var triV0 = new List<int>();
        var triV1 = new List<int>();
        var triV2 = new List<int>();
        for (var sm = 0; sm < subMeshIndices.Length; sm++)
        {
            var idx = subMeshIndices[sm];
            for (var i = 0; i < idx.Length; i += 3)
            {
                triSubMesh.Add(sm);
                triV0.Add(idx[i]);
                triV1.Add(idx[i + 1]);
                triV2.Add(idx[i + 2]);
            }
        }

        var triangleCount = triSubMesh.Count;

        var edgeCount = new Dictionary<Edge, int>();
        var edgeOwner = new Dictionary<Edge, int>();

        void AddEdge(int a, int b, int triIndex)
        {
            var e = Edge.Of(a, b);
            edgeCount[e] = edgeCount.TryGetValue(e, out var c) ? c + 1 : 1;
            edgeOwner[e] = triIndex;
        }

        for (var t = 0; t < triangleCount; t++)
        {
            AddEdge(triV0[t], triV1[t], t);
            AddEdge(triV1[t], triV2[t], t);
            AddEdge(triV2[t], triV0[t], t);
        }

        var borderEdges = edgeCount.Where(kv => kv.Value == 1).Select(kv => kv.Key).ToList();

        // Union-find the border edges by shared vertex so each connected boundary loop can be
        // examined on its own. Only loops made of exactly three edges (i.e. exactly the shape
        // of a single missing triangle) are ever candidates below.
        var parent = new Dictionary<int, int>();

        int Find(int x)
        {
            if (!parent.ContainsKey(x)) parent[x] = x;
            while (parent[x] != x)
            {
                parent[x] = parent[parent[x]];
                x = parent[x];
            }

            return x;
        }

        void Union(int a, int b)
        {
            var ra = Find(a);
            var rb = Find(b);
            if (ra != rb) parent[ra] = rb;
        }

        foreach (var e in borderEdges) Union(e.A, e.B);

        var components = new Dictionary<int, List<Edge>>();
        foreach (var e in borderEdges)
        {
            var root = Find(e.A);
            if (!components.TryGetValue(root, out var list)) components[root] = list = new List<Edge>();
            list.Add(e);
        }

        Vector3d Pos(int v) => vertices[v];

        double TriArea(Vector3d p0, Vector3d p1, Vector3d p2)
        {
            var e1 = p1 - p0;
            var e2 = p2 - p0;
            Vector3d.Cross(ref e1, ref e2, out var cross);
            return cross.Magnitude * 0.5;
        }

        bool TryTriNormal(Vector3d p0, Vector3d p1, Vector3d p2, out Vector3d normal)
        {
            var e1 = p1 - p0;
            var e2 = p2 - p0;
            Vector3d.Cross(ref e1, ref e2, out var cross);
            if (cross.MagnitudeSqr < 1e-20)
            {
                normal = Vector3d.zero;
                return false;
            }

            Vector3d.Normalize(ref cross, out normal);
            return true;
        }

        // True if the triangle at triIndex traverses its corners in the order a -> b (as
        // opposed to b -> a), which is the only other possibility for an edge it owns.
        bool OwnerGoesAToB(int triIndex, int a, int b)
        {
            var v0 = triV0[triIndex];
            var v1 = triV1[triIndex];
            var v2 = triV2[triIndex];
            if (v0 == a && v1 == b) return true;
            if (v1 == a && v2 == b) return true;
            if (v2 == a && v0 == b) return true;
            return false;
        }

        // A consistently-oriented mesh traverses a shared edge in opposite directions on its
        // two sides, so for the fill triangle to be valid here, its owner must traverse the
        // edge as q -> p wherever the fill traverses p -> q.
        bool EdgeOpposesOwner(int p, int q) => OwnerGoesAToB(edgeOwner[Edge.Of(p, q)], q, p);

        var additions = new Dictionary<int, List<int>>();
        var repaired = 0;
        var skipped = 0;
        var skipReasons = new Dictionary<string, int>();
        void Skip(string reason)
        {
            skipped++;
            skipReasons[reason] = skipReasons.GetValueOrDefault(reason) + 1;
        }

        foreach (var edges in components.Values)
        {
            if (edges.Count != 3) continue;

            var loopVerts = new HashSet<int>();
            foreach (var e in edges)
            {
                loopVerts.Add(e.A);
                loopVerts.Add(e.B);
            }

            if (loopVerts.Count != 3) continue;

            // If all three border edges are owned by one and the same triangle, that triangle
            // is a standalone, already-existing island (all of its own edges are unmatched) --
            // not a gap between three different neighbors. There's nothing missing to add here;
            // the underlying defect is that this triangle is disconnected from the rest of the
            // mesh, which an additive-only repair can't (and shouldn't try to) fix.
            var owners = new HashSet<int> { edgeOwner[edges[0]], edgeOwner[edges[1]], edgeOwner[edges[2]] };
            if (owners.Count == 1)
            {
                Skip("isolated-triangle");
                continue;
            }

            var v = loopVerts.ToArray();
            int a = v[0], b = v[1], c = v[2];

            int x, y, z;
            if (EdgeOpposesOwner(a, b) && EdgeOpposesOwner(b, c) && EdgeOpposesOwner(c, a))
            {
                (x, y, z) = (a, b, c);
            }
            else if (EdgeOpposesOwner(a, c) && EdgeOpposesOwner(c, b) && EdgeOpposesOwner(b, a))
            {
                (x, y, z) = (a, c, b);
            }
            else
            {
                // No consistent winding closes this loop as a single triangle -- this isn't
                // the simple defect we know how to repair safely.
                Skip("winding");
                continue;
            }

            double neighborAreaSum = 0;
            var neighborNormalSum = Vector3d.zero;
            var neighborSubMeshes = new HashSet<int>();
            foreach (var e in edges)
            {
                var owner = edgeOwner[e];
                var p0 = Pos(triV0[owner]);
                var p1 = Pos(triV1[owner]);
                var p2 = Pos(triV2[owner]);
                neighborAreaSum += TriArea(p0, p1, p2);
                if (TryTriNormal(p0, p1, p2, out var n)) neighborNormalSum += n;
                neighborSubMeshes.Add(triSubMesh[owner]);
            }

            if (neighborSubMeshes.Count != 1)
            {
                // The three neighbors disagree on material -- this looks like a real
                // material/UV boundary, not a decimation artifact. Leave it alone.
                Skip("submesh");
                continue;
            }

            var avgNeighborArea = neighborAreaSum / edges.Count;

            var fillP0 = Pos(x);
            var fillP1 = Pos(y);
            var fillP2 = Pos(z);
            var fillArea = TriArea(fillP0, fillP1, fillP2);

            if (fillArea < avgNeighborArea * 1e-6)
            {
                Skip("degenerate"); // near-collinear fill
                continue;
            }

            var areaRatio = fillArea / avgNeighborArea;
            if (areaRatio > 8.0 || areaRatio < 1.0 / 8.0)
            {
                Skip("scale"); // wildly different size than its neighbors
                continue;
            }

            if (!TryTriNormal(fillP0, fillP1, fillP2, out var fillNormal) ||
                neighborNormalSum.MagnitudeSqr < 1e-20)
            {
                Skip("badnormal");
                continue;
            }

            // A correctly-oriented fill in a high-curvature area (e.g. near a molding or
            // window frame) can still deviate a fair bit from its neighbors' average normal,
            // so this threshold is deliberately permissive -- it only needs to distinguish a
            // plausibly-correct orientation from a wrong or flipped one, which shows up as a
            // strongly negative (near -1) alignment, not a merely moderate one.
            Vector3d.Normalize(ref neighborNormalSum, out var avgNeighborNormal);
            var alignment = Vector3d.Dot(ref fillNormal, ref avgNeighborNormal);
            if (alignment < 0.15)
            {
                Skip("normal"); // fill's orientation doesn't match the surrounding surface
                continue;
            }

            var subMeshIndex = neighborSubMeshes.First();
            if (!additions.TryGetValue(subMeshIndex, out var list)) additions[subMeshIndex] = list = new List<int>();
            list.Add(x);
            list.Add(y);
            list.Add(z);
            repaired++;
        }

        foreach (var (subMeshIndex, newIndices) in additions)
        {
            var existing = mesh.GetIndices(subMeshIndex);
            var combined = new int[existing.Length + newIndices.Count];
            Array.Copy(existing, combined, existing.Length);
            newIndices.CopyTo(combined, existing.Length);
            mesh.SetIndices(subMeshIndex, combined);
        }

        if (repaired > 0 || skipped > 0)
        {
            var reasons = string.Join(", ", skipReasons.Select(kv => $"{kv.Key}={kv.Value}"));
            Console.WriteLine($" ?> Hole repair: closed {repaired} single-triangle gap(s), left {skipped} unresolved candidate(s) ({reasons})");
        }

        return repaired;
    }
}

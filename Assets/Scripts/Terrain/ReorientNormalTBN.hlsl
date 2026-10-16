//------------------------------------------------------------------------------
// ReorientNormalTBN
//
// Re-expresses a normal `n` from one full reference frame to another.
//
// Each plane of reference is given as a complete TBN basis:
//   T = tangent   (lies in the plane)
//   B = bitangent (lies in the plane, perpendicular to T)
//   N = plane normal
//
// The normal is decomposed into its (t, b, n) coordinates in the OLD basis,
// then reconstructed from those same coordinates in the NEW basis. Unlike a
// shortest-arc rotation between the two plane normals, this also respects
// the twist around each plane's normal, because the tangent directions of
// both frames are taken into account.
//
// Both bases are expected to be orthonormal. If they are, the operation is a
// pure rotation (or rotation+reflection if a basis is left-handed) and
// preserves the length of `n`.
//------------------------------------------------------------------------------
float3 ReorientNormalTBN(float3 n,
                         float3 oldT, float3 oldB, float3 oldN,
                         float3 newT, float3 newB, float3 newN)
{
    // Decompose in the old basis (world -> old tangent space)
    float3 local = float3(dot(n, oldT),
                          dot(n, oldB),
                          dot(n, oldN));

    // Reconstruct in the new basis (new tangent space -> world)
    return newT * local.x + newB * local.y + newN * local.z;
}

//------------------------------------------------------------------------------
// Matrix overload
//
// Convention used here: each TBN matrix is row-major with rows = {T, B, N},
// i.e. the matrix you'd build with float3x3(T, B, N). With that convention,
// mul(M, v) transforms world -> tangent space, and mul(v, M) transforms
// tangent space -> world (for orthonormal M, transpose == inverse).
//------------------------------------------------------------------------------
float3 ReorientNormalTBN(float3 n, float3x3 oldTBN, float3x3 newTBN)
{
    float3 local = mul(oldTBN, n);   // world -> old tangent space
    return mul(local, newTBN);       // new tangent space -> world
}

//------------------------------------------------------------------------------
// BuildTBN
//
// Convenience helper: builds an orthonormal TBN basis from a plane normal and
// an approximate tangent hint (Gram-Schmidt). Use this when you have plane
// normals plus a reference direction, rather than authored tangent frames.
//------------------------------------------------------------------------------
float3x3 BuildTBN(float3 planeN, float3 tangentHint)
{
    float3 N = normalize(planeN);
    float3 T = normalize(tangentHint - N * dot(tangentHint, N));
    float3 B = cross(N, T);
    return float3x3(T, B, N);
}

//------------------------------------------------------------------------------
// Example usage
//------------------------------------------------------------------------------
// // With authored frames (e.g. mesh TBN at two surface points):
// float3 nRelToNew = ReorientNormalTBN(n, oldT, oldB, oldN, newT, newB, newN);
//
// // With plane normals + a shared tangent hint:
// float3x3 oldTBN = BuildTBN(float3(0, 1, 0), float3(1, 0, 0));
// float3x3 newTBN = BuildTBN(float3(0, 0, 1), float3(1, 0, 0));
// float3 nRelToNew = ReorientNormalTBN(n, oldTBN, newTBN);
//
// Sanity checks:
//   dot(nRelToNew, newN) == dot(n, oldN)   // same lift off the plane
//   dot(nRelToNew, newT) == dot(n, oldT)   // same twist / in-plane direction
//------------------------------------------------------------------------------

//------------------------------------------------------------------------------
// ReorientNormal (previous version, kept for reference)
//
// Shortest-arc variant: only the two plane normals are known, twist around
// the normal is ignored. See ReorientNormalTBN above for the frame-aware one.
//------------------------------------------------------------------------------
float3 ReorientNormal(float3 n, float3 oldPlaneN, float3 newPlaneN)
{
    float d = dot(oldPlaneN, newPlaneN);

    // Degenerate case: planes face opposite directions (d ~= -1).
    if (d < -0.999999)
    {
        float3 helper = (abs(oldPlaneN.x) < 0.9) ? float3(1, 0, 0)
                                                 : float3(0, 1, 0);
        float3 axis = normalize(cross(oldPlaneN, helper));
        return 2.0 * dot(axis, n) * axis - n;
    }

    float3 c = cross(oldPlaneN, newPlaneN);
    return n * d + cross(c, n) + c * (dot(c, n) / (1.0 + d));
}
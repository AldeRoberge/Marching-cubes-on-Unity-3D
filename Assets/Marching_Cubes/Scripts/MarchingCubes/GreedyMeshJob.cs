using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>
/// Job that applies greedy meshing optimization to reduce triangle count
/// by merging coplanar, adjacent faces with the same material into quads.
/// </summary>
[BurstCompile]
public struct GreedyMeshJob : IJob
{
    [ReadOnly] public NativeArray<float3> inputVertices;
    [ReadOnly] public NativeArray<float2> inputUVs;
    
    [WriteOnly] public NativeList<float3> outputVertices;
    [WriteOnly] public NativeList<float2> outputUVs;
    
    public void Execute()
    {
        // For marching cubes, triangles come in groups of 3 vertices
        int triangleCount = inputVertices.Length / 3;
        
        if (triangleCount == 0)
            return;
        
        // Track which triangles have been merged
        NativeArray<bool> merged = new NativeArray<bool>(triangleCount, Allocator.Temp);
        
        for (int i = 0; i < triangleCount; i++)
        {
            if (merged[i])
                continue;
                
            int baseIdx = i * 3;
            float3 v0 = inputVertices[baseIdx];
            float3 v1 = inputVertices[baseIdx + 1];
            float3 v2 = inputVertices[baseIdx + 2];
            float2 uv0 = inputUVs[baseIdx];
            float2 uv1 = inputUVs[baseIdx + 1];
            float2 uv2 = inputUVs[baseIdx + 2];
            
            // Calculate normal for this triangle
            float3 edge1 = v1 - v0;
            float3 edge2 = v2 - v0;
            float3 normal = math.normalize(math.cross(edge1, edge2));
            
            // Calculate material index from UV (since material is encoded in UV coordinates)
            int materialIdx = GetMaterialIndex(uv0);
            
            // Try to find one adjacent triangle to form a quad
            bool foundPair = false;
            for (int j = i + 1; j < triangleCount; j++)
            {
                if (merged[j])
                    continue;
                    
                int otherIdx = j * 3;
                float3 ov0 = inputVertices[otherIdx];
                float3 ov1 = inputVertices[otherIdx + 1];
                float3 ov2 = inputVertices[otherIdx + 2];
                float2 oUV0 = inputUVs[otherIdx];
                
                // Check if same material
                if (GetMaterialIndex(oUV0) != materialIdx)
                    continue;
                
                // Calculate normal for other triangle
                float3 oEdge1 = ov1 - ov0;
                float3 oEdge2 = ov2 - ov0;
                float3 otherNormal = math.normalize(math.cross(oEdge1, oEdge2));
                
                // Check if coplanar (same normal direction within tolerance)
                float normalDot = math.dot(normal, otherNormal);
                if (normalDot < 0.95f)  // More relaxed threshold for better merging
                    continue;
                
                // Check if triangles share exactly 2 vertices (form a quad)
                if (CanFormQuad(v0, v1, v2, ov0, ov1, ov2, out float3 q0, out float3 q1, out float3 q2, out float3 q3))
                {
                    // Create a quad from two triangles (output as 2 triangles with 4 unique vertices)
                    float2 quadUV = uv0; // Use material from first triangle
                    
                    // Triangle 1: q0, q1, q2
                    outputVertices.Add(q0);
                    outputVertices.Add(q1);
                    outputVertices.Add(q2);
                    outputUVs.Add(quadUV);
                    outputUVs.Add(quadUV);
                    outputUVs.Add(quadUV);
                    
                    // Triangle 2: q0, q2, q3
                    outputVertices.Add(q0);
                    outputVertices.Add(q2);
                    outputVertices.Add(q3);
                    outputUVs.Add(quadUV);
                    outputUVs.Add(quadUV);
                    outputUVs.Add(quadUV);
                    
                    merged[j] = true;
                    foundPair = true;
                    break;
                }
            }
            
            if (!foundPair)
            {
                // Output original triangle if no merge partner found
                outputVertices.Add(v0);
                outputVertices.Add(v1);
                outputVertices.Add(v2);
                outputUVs.Add(uv0);
                outputUVs.Add(uv1);
                outputUVs.Add(uv2);
            }
            
            merged[i] = true;
        }
        
        merged.Dispose();
    }
    
    private bool CanFormQuad(float3 v0, float3 v1, float3 v2, 
                             float3 ov0, float3 ov1, float3 ov2,
                             out float3 q0, out float3 q1, out float3 q2, out float3 q3)
    {
        const float epsilon = 0.01f;
        
        // Collect all 6 vertices from both triangles
        NativeArray<float3> allVerts = new NativeArray<float3>(6, Allocator.Temp);
        allVerts[0] = v0;
        allVerts[1] = v1;
        allVerts[2] = v2;
        allVerts[3] = ov0;
        allVerts[4] = ov1;
        allVerts[5] = ov2;
        
        // Find unique vertices (should be 4 for a valid quad)
        NativeList<float3> uniqueVerts = new NativeList<float3>(4, Allocator.Temp);
        
        for (int i = 0; i < 6; i++)
        {
            bool isDuplicate = false;
            for (int j = 0; j < uniqueVerts.Length; j++)
            {
                if (IsVertexEqual(allVerts[i], uniqueVerts[j], epsilon))
                {
                    isDuplicate = true;
                    break;
                }
            }
            if (!isDuplicate)
            {
                uniqueVerts.Add(allVerts[i]);
            }
        }
        
        // Must have exactly 4 unique vertices to form a quad
        bool canForm = uniqueVerts.Length == 4;
        
        if (canForm)
        {
            // Sort vertices to form a proper quad (counter-clockwise order)
            // Use the centroid to order vertices
            float3 centroid = float3.zero;
            for (int i = 0; i < 4; i++)
                centroid += uniqueVerts[i];
            centroid /= 4.0f;
            
            // Calculate normal from first triangle
            float3 normal = math.normalize(math.cross(v1 - v0, v2 - v0));
            
            // Sort vertices by angle around centroid
            SortQuadVertices(uniqueVerts, centroid, normal);
            
            q0 = uniqueVerts[0];
            q1 = uniqueVerts[1];
            q2 = uniqueVerts[2];
            q3 = uniqueVerts[3];
        }
        else
        {
            q0 = q1 = q2 = q3 = float3.zero;
        }
        
        uniqueVerts.Dispose();
        allVerts.Dispose();
        
        return canForm;
    }
    
    private void SortQuadVertices(NativeList<float3> verts, float3 centroid, float3 normal)
    {
        // Sort vertices by angle for proper winding order
        NativeArray<float> angles = new NativeArray<float>(verts.Length, Allocator.Temp);
        
        // Use first vertex as reference
        float3 reference = math.normalize(verts[0] - centroid);
        float3 tangent = math.normalize(math.cross(normal, reference));
        
        for (int i = 0; i < verts.Length; i++)
        {
            float3 toVert = math.normalize(verts[i] - centroid);
            float x = math.dot(toVert, reference);
            float y = math.dot(toVert, tangent);
            angles[i] = math.atan2(y, x);
        }
        
        // Simple bubble sort (only 4 elements)
        for (int i = 0; i < verts.Length - 1; i++)
        {
            for (int j = 0; j < verts.Length - i - 1; j++)
            {
                if (angles[j] > angles[j + 1])
                {
                    // Swap angles
                    float tempAngle = angles[j];
                    angles[j] = angles[j + 1];
                    angles[j + 1] = tempAngle;
                    
                    // Swap vertices
                    float3 tempVert = verts[j];
                    verts[j] = verts[j + 1];
                    verts[j + 1] = tempVert;
                }
            }
        }
        
        angles.Dispose();
    }
    
    private int GetMaterialIndex(float2 uv)
    {
        // Material is encoded in UV coordinates
        // Extract material index from UV position
        int row = (int)((1.0f - uv.y) / Constants.MATERIAL_SIZE);
        int col = (int)(uv.x / Constants.MATERIAL_SIZE);
        row = math.clamp(row, 0, Constants.NUMBER_MATERIALS - 1);
        col = math.clamp(col, 0, Constants.MATERIAL_FOR_ROW - 1);
        return row * Constants.MATERIAL_FOR_ROW + col;
    }
    
    private bool IsVertexEqual(float3 a, float3 b, float epsilon)
    {
        return math.lengthsq(a - b) < epsilon * epsilon;
    }
}

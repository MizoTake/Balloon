using UnityEngine;
using Unity.Mathematics;

namespace BalloonSimulation.JobSystem
{
    /// <summary>
    /// Generates optimized balloon mesh for instanced rendering
    /// </summary>
    public static class BalloonMeshGenerator
    {
        /// <summary>
        /// Creates a balloon mesh with specified resolution
        /// </summary>
        public static Mesh CreateBalloonMesh(int segments = 16, int rings = 16)
        {
            Mesh mesh = new Mesh();
            mesh.name = "Balloon";
            
            // Calculate vertex count
            int vertexCount = (segments + 1) * (rings + 1);
            Vector3[] vertices = new Vector3[vertexCount];
            Vector3[] normals = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            
            // Generate vertices
            int vertexIndex = 0;
            for (int ring = 0; ring <= rings; ring++)
            {
                float v = (float)ring / rings;
                float theta = v * Mathf.PI;
                float sinTheta = Mathf.Sin(theta);
                float cosTheta = Mathf.Cos(theta);
                
                for (int segment = 0; segment <= segments; segment++)
                {
                    float u = (float)segment / segments;
                    float phi = u * 2f * Mathf.PI;
                    float sinPhi = Mathf.Sin(phi);
                    float cosPhi = Mathf.Cos(phi);
                    
                    // Apply balloon shape deformation
                    float balloonShape = GetBalloonShape(v);
                    
                    // Calculate vertex position
                    float x = balloonShape * sinTheta * cosPhi;
                    float y = cosTheta;
                    float z = balloonShape * sinTheta * sinPhi;
                    
                    vertices[vertexIndex] = new Vector3(x * 0.5f, y * 0.5f, z * 0.5f);
                    normals[vertexIndex] = new Vector3(x, y, z).normalized;
                    uvs[vertexIndex] = new Vector2(u, v);
                    
                    vertexIndex++;
                }
            }
            
            // Generate triangles
            int triangleCount = segments * rings * 2;
            int[] triangles = new int[triangleCount * 3];
            int triangleIndex = 0;
            
            for (int ring = 0; ring < rings; ring++)
            {
                for (int segment = 0; segment < segments; segment++)
                {
                    int current = ring * (segments + 1) + segment;
                    int next = current + segments + 1;
                    
                    // First triangle
                    triangles[triangleIndex++] = current;
                    triangles[triangleIndex++] = next + 1;
                    triangles[triangleIndex++] = current + 1;
                    
                    // Second triangle
                    triangles[triangleIndex++] = current;
                    triangles[triangleIndex++] = next;
                    triangles[triangleIndex++] = next + 1;
                }
            }
            
            // Assign mesh data
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            
            // Optimize mesh
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            mesh.Optimize();
            
            return mesh;
        }
        
        /// <summary>
        /// Creates a simple sphere mesh for performance testing
        /// </summary>
        public static Mesh CreateSimpleSphere(int subdivisions = 2)
        {
            Mesh mesh = new Mesh();
            mesh.name = "SimpleBalloon";
            
            // Start with icosahedron
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            float s = 1f / Mathf.Sqrt(1f + t * t);
            
            Vector3[] baseVertices = new Vector3[]
            {
                new Vector3(-s, t * s, 0),
                new Vector3(s, t * s, 0),
                new Vector3(-s, -t * s, 0),
                new Vector3(s, -t * s, 0),
                new Vector3(0, -s, t * s),
                new Vector3(0, s, t * s),
                new Vector3(0, -s, -t * s),
                new Vector3(0, s, -t * s),
                new Vector3(t * s, 0, -s),
                new Vector3(t * s, 0, s),
                new Vector3(-t * s, 0, -s),
                new Vector3(-t * s, 0, s)
            };
            
            int[] baseTriangles = new int[]
            {
                0, 11, 5,  0, 5, 1,   0, 1, 7,   0, 7, 10,  0, 10, 11,
                1, 5, 9,   5, 11, 4,  11, 10, 2, 10, 7, 6,  7, 1, 8,
                3, 9, 4,   3, 4, 2,   3, 2, 6,   3, 6, 8,   3, 8, 9,
                4, 9, 5,   2, 4, 11,  6, 2, 10,  8, 6, 7,   9, 8, 1
            };
            
            // Apply subdivisions
            for (int i = 0; i < subdivisions; i++)
            {
                SubdivideMesh(ref baseVertices, ref baseTriangles);
            }
            
            // Normalize vertices to create sphere and apply balloon shape
            Vector3[] finalVertices = new Vector3[baseVertices.Length];
            Vector3[] normals = new Vector3[baseVertices.Length];
            Vector2[] uvs = new Vector2[baseVertices.Length];
            
            for (int i = 0; i < baseVertices.Length; i++)
            {
                Vector3 normalized = baseVertices[i].normalized;
                
                // Calculate UV coordinates
                float u = 0.5f + Mathf.Atan2(normalized.z, normalized.x) / (2f * Mathf.PI);
                float v = 0.5f + Mathf.Asin(normalized.y) / Mathf.PI;
                
                // Apply balloon shape
                float balloonShape = GetBalloonShape(v);
                
                finalVertices[i] = normalized * 0.5f * Mathf.Lerp(1f, balloonShape, 0.7f);
                normals[i] = normalized;
                uvs[i] = new Vector2(u, v);
            }
            
            mesh.vertices = finalVertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = baseTriangles;
            
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            mesh.Optimize();
            
            return mesh;
        }
        
        /// <summary>
        /// Returns the balloon shape factor for a given vertical position
        /// </summary>
        private static float GetBalloonShape(float v)
        {
            // Create teardrop/balloon shape
            // Bottom is wider, top is narrower
            float shape = Mathf.Sin(v * Mathf.PI);
            
            // Add slight pear shape
            float pearShape = 1f - (v * v * 0.3f);
            
            return shape * pearShape;
        }
        
        /// <summary>
        /// Subdivides a mesh by splitting each triangle into 4 smaller triangles
        /// </summary>
        private static void SubdivideMesh(ref Vector3[] vertices, ref int[] triangles)
        {
            // Cache for edge midpoints
            var midpointCache = new System.Collections.Generic.Dictionary<long, int>();
            var newVertices = new System.Collections.Generic.List<Vector3>(vertices);
            var newTriangles = new System.Collections.Generic.List<int>();
            
            // Process each triangle
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i];
                int b = triangles[i + 1];
                int c = triangles[i + 2];
                
                // Get midpoints
                int ab = GetMidpoint(a, b, ref newVertices, ref midpointCache);
                int bc = GetMidpoint(b, c, ref newVertices, ref midpointCache);
                int ca = GetMidpoint(c, a, ref newVertices, ref midpointCache);
                
                // Create 4 new triangles
                newTriangles.AddRange(new int[] { a, ab, ca });
                newTriangles.AddRange(new int[] { b, bc, ab });
                newTriangles.AddRange(new int[] { c, ca, bc });
                newTriangles.AddRange(new int[] { ab, bc, ca });
            }
            
            vertices = newVertices.ToArray();
            triangles = newTriangles.ToArray();
        }
        
        /// <summary>
        /// Gets or creates a midpoint between two vertices
        /// </summary>
        private static int GetMidpoint(int a, int b, 
            ref System.Collections.Generic.List<Vector3> vertices, 
            ref System.Collections.Generic.Dictionary<long, int> cache)
        {
            // Create unique key for edge
            long key = ((long)Mathf.Min(a, b) << 32) + Mathf.Max(a, b);
            
            // Check cache
            if (cache.TryGetValue(key, out int midpoint))
                return midpoint;
            
            // Create new midpoint
            Vector3 midPos = (vertices[a] + vertices[b]) * 0.5f;
            midpoint = vertices.Count;
            vertices.Add(midPos);
            
            // Cache it
            cache[key] = midpoint;
            
            return midpoint;
        }
    }
}
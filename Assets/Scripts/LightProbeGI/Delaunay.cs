using UnityEngine;
using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;

namespace GutEngine
{
    [BurstCompile(FloatPrecision.High, FloatMode.Strict)]
    public struct Delaunay3DJob : IJob
    {
        [System.Serializable]
        public struct Tetrahedron
        {
            public struct Face : System.IEquatable<Face>
            {
                public int _vIndex0;
                public int _vIndex1;
                public int _vIndex2;

                public Face(int vIndex0, int vIndex1, int vIndex2)
                {
                    this._vIndex0 = vIndex0;
                    this._vIndex1 = vIndex1;
                    this._vIndex2 = vIndex2;
                }

                public bool Equals(Face other)
                {
                    bool has0 = this._vIndex0 == other._vIndex0 || this._vIndex0 == other._vIndex1 || this._vIndex0 == other._vIndex2;
                    bool has1 = this._vIndex1 == other._vIndex0 || this._vIndex1 == other._vIndex1 || this._vIndex1 == other._vIndex2;
                    bool has2 = this._vIndex2 == other._vIndex0 || this._vIndex2 == other._vIndex1 || this._vIndex2 == other._vIndex2;
                    return has0 && has1 && has2;
                }
            }

            public int _vIndex0;
            public int _vIndex1;
            public int _vIndex2;
            public int _vIndex3;
            public Face _face0;
            public Face _face1;
            public Face _face2;
            public Face _face3;
            public double3 _circumCenter;
            public double _circumRadius;

            public Tetrahedron(int vIndex0, int vIndex1, int vIndex2, int vIndex3, double3 v0, double3 v1, double3 v2, double3 v3)
            {
                this._vIndex0 = vIndex0;
                this._vIndex1 = vIndex1;
                this._vIndex2 = vIndex2;
                this._vIndex3 = vIndex3;
                this._face0 = new Face(vIndex0, vIndex1, vIndex2);
                this._face1 = new Face(vIndex0, vIndex2, vIndex3);
                this._face2 = new Face(vIndex0, vIndex3, vIndex1);
                this._face3 = new Face(vIndex1, vIndex3, vIndex2);
                this._circumCenter = new double3(0);
                this._circumRadius = 0;
                Circumsphere(v0, v1, v2, v3, out this._circumCenter, out this._circumRadius);
            }

            public bool IsInsideCircumsphere(double3 point)
            {
                return math.distance(point, _circumCenter) < (_circumRadius + EPSILON_RADIUS);
            }

            // https://gamedev.stackexchange.com/questions/110223/how-do-i-find-the-circumsphere-of-a-tetrahedron
            void Circumsphere(double3 v0, double3 v1, double3 v2, double3 v3, out double3 center, out double radius)
            {
                // Create the rows of our "unrolled" 3x3 matrix
                double3 Row1 = v1 - v0;
                double sqLength1 = math.lengthsq(Row1);
                double3 Row2 = v2 - v0;
                double sqLength2 = math.lengthsq(Row2);
                double3 Row3 = v3 - v0;
                double sqLength3 = math.lengthsq(Row3);

                // Compute the determinant of said matrix
                double determinant = Row1.x * (Row2.y * Row3.z - Row3.y * Row2.z)
                                    - Row2.x * (Row1.y * Row3.z - Row3.y * Row1.z)
                                    + Row3.x * (Row1.y * Row2.z - Row2.y * Row1.z);

                // Compute the volume of the tetrahedron, and precompute a scalar quantity for re-use in the formula
                double volume = determinant / (double)6;
                double iTwelveVolume = 1 / ((double)volume * 12);

                center.x = v0.x + iTwelveVolume * ((Row2.y * Row3.z - Row3.y * Row2.z) * sqLength1 - (Row1.y * Row3.z - Row3.y * Row1.z) * sqLength2 + (Row1.y * Row2.z - Row2.y * Row1.z) * sqLength3);
                center.y = v0.y + iTwelveVolume * (-(Row2.x * Row3.z - Row3.x * Row2.z) * sqLength1 + (Row1.x * Row3.z - Row3.x * Row1.z) * sqLength2 - (Row1.x * Row2.z - Row2.x * Row1.z) * sqLength3);
                center.z = v0.z + iTwelveVolume * ((Row2.x * Row3.y - Row3.x * Row2.y) * sqLength1 - (Row1.x * Row3.y - Row3.x * Row1.y) * sqLength2 + (Row1.x * Row2.y - Row2.x * Row1.y) * sqLength3);

                // Once we know the center, the radius is clearly the distance to any vertex
                radius = math.distance(center, v0);
            }
        }

        [ReadOnly] public NativeArray<Vector3> inVertices;
        public NativeList<Tetrahedron> outTetrahedra;

        static readonly double EPSILON_RADIUS = 0.00001d;
        static readonly double EPSILON_COPLANAR = 0.0001d;

        // https://en.wikipedia.org/wiki/Bowyer%E2%80%93Watson_algorithm
        public void Execute()
        {
            NativeArray<double3> verticesTemp = new NativeArray<double3>(inVertices.Length + 4, Allocator.Temp);
            for (int i = 0; i < inVertices.Length; i++)
                verticesTemp[i] = new double3(inVertices[i]);

            // add super tetra
            int superTetrahedronVIndex = verticesTemp.Length - 4;
            {
                double maxVal = (double)10000;
                double3 v0 = new double3(0, maxVal, 0);
                double3 v1 = new double3(0, -maxVal, maxVal);
                double3 v2 = new double3(maxVal, -maxVal, -maxVal);
                double3 v3 = new double3(-maxVal, -maxVal, -maxVal);

                verticesTemp[superTetrahedronVIndex + 0] = v0;
                verticesTemp[superTetrahedronVIndex + 1] = v1;
                verticesTemp[superTetrahedronVIndex + 2] = v2;
                verticesTemp[superTetrahedronVIndex + 3] = v3;

                outTetrahedra.Add(new Tetrahedron(superTetrahedronVIndex + 0, superTetrahedronVIndex + 1, superTetrahedronVIndex + 2, superTetrahedronVIndex + 3, v0, v1, v2, v3));
            }

            for (int i = 0; i < verticesTemp.Length - 4; i++)
            {
                double3 currentVertex = verticesTemp[i];
                NativeList<int> badTetras = new NativeList<int>(Allocator.Temp);
                NativeList<Tetrahedron.Face> polygon = new NativeList<Tetrahedron.Face>(Allocator.Temp);

                // make list of tetras colliding with current point
                for (int j = 0; j < outTetrahedra.Length; j++)
                {
                    Tetrahedron currentTetrahedron = outTetrahedra[j];

                    if (currentTetrahedron.IsInsideCircumsphere(currentVertex))
                        badTetras.Add(j);
                }

                // find tetras faces
                for (int j = 0; j < badTetras.Length; j++)
                {
                    Tetrahedron currentTetrahedron = outTetrahedra[badTetras[j]];

                    bool wouldCoplanar0 = IsCoplanar(verticesTemp[currentTetrahedron._face0._vIndex0], verticesTemp[currentTetrahedron._face0._vIndex1], verticesTemp[currentTetrahedron._face0._vIndex2], currentVertex);
                    bool wouldCoplanar1 = IsCoplanar(verticesTemp[currentTetrahedron._face1._vIndex0], verticesTemp[currentTetrahedron._face1._vIndex1], verticesTemp[currentTetrahedron._face1._vIndex2], currentVertex);
                    bool wouldCoplanar2 = IsCoplanar(verticesTemp[currentTetrahedron._face2._vIndex0], verticesTemp[currentTetrahedron._face2._vIndex1], verticesTemp[currentTetrahedron._face2._vIndex2], currentVertex);
                    bool wouldCoplanar3 = IsCoplanar(verticesTemp[currentTetrahedron._face3._vIndex0], verticesTemp[currentTetrahedron._face3._vIndex1], verticesTemp[currentTetrahedron._face3._vIndex2], currentVertex);

                    // check faces
                    if (!wouldCoplanar0 && !HasSharingFace(badTetras, currentTetrahedron._face0, j))
                        polygon.Add(currentTetrahedron._face0);
                    if (!wouldCoplanar1 && !HasSharingFace(badTetras, currentTetrahedron._face1, j))
                        polygon.Add(currentTetrahedron._face1);
                    if (!wouldCoplanar2 && !HasSharingFace(badTetras, currentTetrahedron._face2, j))
                        polygon.Add(currentTetrahedron._face2);
                    if (!wouldCoplanar3 && !HasSharingFace(badTetras, currentTetrahedron._face3, j))
                        polygon.Add(currentTetrahedron._face3);
                }

                // sort to remove last ones first, so we dont lose indices
                badTetras.Sort();
                for (int j = badTetras.Length - 1; j > -1; j--)
                    outTetrahedra.RemoveAt(badTetras[j]);

                // build new tetras
                for (int j = 0; j < polygon.Length; j++)
                {
                    Tetrahedron.Face currentFace = polygon[j];

                    double3 faceVertex0 = verticesTemp[currentFace._vIndex0];
                    double3 faceVertex1 = verticesTemp[currentFace._vIndex1];
                    double3 faceVertex2 = verticesTemp[currentFace._vIndex2];
                    if (!IsCoplanar(faceVertex0, faceVertex1, faceVertex2, currentVertex))
                    {
                        Tetrahedron newTetrahedra = new Tetrahedron(currentFace._vIndex0, currentFace._vIndex1, currentFace._vIndex2, i, faceVertex0, faceVertex1, faceVertex2, currentVertex);
                        outTetrahedra.Add(newTetrahedra);
                    }
                }
            }

            // remove super tetra
            // same strategey as aboeve, save indices to list then sortand remove lasts first
            NativeList<int> superTetrahedronIndices = new NativeList<int>(Allocator.Temp);
            for (int i = 0; i < outTetrahedra.Length; i++)
            {
                Tetrahedron currentTetrahedron = outTetrahedra[i];

                bool isSuperTetra0 = (currentTetrahedron._vIndex0 == superTetrahedronVIndex + 0) || (currentTetrahedron._vIndex0 == superTetrahedronVIndex + 1) || (currentTetrahedron._vIndex0 == superTetrahedronVIndex + 2) || (currentTetrahedron._vIndex0 == superTetrahedronVIndex + 3);
                bool isSuperTetra1 = (currentTetrahedron._vIndex1 == superTetrahedronVIndex + 0) || (currentTetrahedron._vIndex1 == superTetrahedronVIndex + 1) || (currentTetrahedron._vIndex1 == superTetrahedronVIndex + 2) || (currentTetrahedron._vIndex1 == superTetrahedronVIndex + 3);
                bool isSuperTetra2 = (currentTetrahedron._vIndex2 == superTetrahedronVIndex + 0) || (currentTetrahedron._vIndex2 == superTetrahedronVIndex + 1) || (currentTetrahedron._vIndex2 == superTetrahedronVIndex + 2) || (currentTetrahedron._vIndex2 == superTetrahedronVIndex + 3);
                bool isSuperTetra3 = (currentTetrahedron._vIndex3 == superTetrahedronVIndex + 0) || (currentTetrahedron._vIndex3 == superTetrahedronVIndex + 1) || (currentTetrahedron._vIndex3 == superTetrahedronVIndex + 2) || (currentTetrahedron._vIndex3 == superTetrahedronVIndex + 3);

                if (isSuperTetra0 || isSuperTetra1 || isSuperTetra2 || isSuperTetra3)
                    superTetrahedronIndices.Add(i);
            }

            superTetrahedronIndices.Sort();
            for (int i = superTetrahedronIndices.Length - 1; i > -1; i--)
                outTetrahedra.RemoveAt(superTetrahedronIndices[i]);
        }

        public bool HasSharingFace(NativeList<int> badTetras, Tetrahedron.Face face, int ignoreIndex)
        {
            for (int i = 0; i < badTetras.Length; i++)
            {
                if (i == ignoreIndex) continue;

                Tetrahedron otherTetrahedra = outTetrahedra[badTetras[i]];

                if (face.Equals(otherTetrahedra._face0) || face.Equals(otherTetrahedra._face1) || face.Equals(otherTetrahedra._face2) || face.Equals(otherTetrahedra._face3))
                    return true;
            }

            return false;
        }

        // Function to find equation of plane.
        // https://www.geeksforgeeks.org/program-to-check-whether-4-points-in-a-3-d-plane-are-coplanar/#:~:text=To%20check%20whether%204%20points%20are%20coplanar%20or%20not%2C%20first,equation%20obtained%20in%20step%201.
        public bool IsCoplanar(double3 p0, double3 p1, double3 p2, double3 p3)
        {
            double a1 = p1.x - p0.x;
            double b1 = p1.y - p0.y;
            double c1 = p1.z - p0.z;
            double a2 = p2.x - p0.x;
            double b2 = p2.y - p0.y;
            double c2 = p2.z - p0.z;
            double a = b1 * c2 - b2 * c1;
            double b = a2 * c1 - a1 * c2;
            double c = a1 * b2 - b1 * a2;
            double d = (-a * p0.x - b * p0.y - c * p0.z);

            // equation of plane is: a*x + b*y + c*z = 0 #

            // checking if the 4th point satisfies
            // the above equation
            double term = a * p3.x + b * p3.y + c * p3.z + d;
            if (approximately(term, 0d, EPSILON_COPLANAR))
                return true;

            return false;
        }

        bool approximately(double val0, double val1, double epsilon)
        {
            return val0 > (val1 - epsilon) && val0 < (val1 + epsilon);
        }
    }
}
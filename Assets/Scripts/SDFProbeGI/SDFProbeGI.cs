using UnityEngine;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Collections;
using System.Collections;

namespace GutEngine
{
    [ExecuteInEditMode]
    public class SDFProbeGI : MonoBehaviour
    {
        [SerializeField] private bool _updateTetrahedra;
        [SerializeField] private LightProbe[] _lightProbes;
        [SerializeField] private Vector3[] _vertices;
        [SerializeField] private LightProbe.SHL2[] _shl2s;
        [SerializeField] private Delaunay3DJob.Tetrahedron[] _tetrahedra;
        [SerializeField] private BoundingSphere[] _tetrahedraBoundingSpheres;
        [SerializeField] private Material[] _tetrahedraMaterials;
        [SerializeField] private CullingGroup _tetrahedraCullingGroup;

        private static Shader _lightProbeGIShader;

        public int TetrahedraCount { get => _tetrahedra.Length; }
        public CullingGroup TetrahedraCullingGroup { get => _tetrahedraCullingGroup; }
        public Material[] TetrahedraMaterials { get => _tetrahedraMaterials; }
        public BoundingSphere[] TetrahedraBoundingSpheres { get => _tetrahedraBoundingSpheres; }

        void Start()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
                StartCoroutine(BakeAsync());
#endif
        }

        public IEnumerator BakeAsync()
        {
            yield return StartCoroutine(BakeTetrahedraAsync(true, true));
            yield return StartCoroutine(BakeShaderDataAsync(true));
        }

        IEnumerator BakeTetrahedraAsync(bool isAsync, bool bakeCubeMaps)
        {
            if (_lightProbeGIShader == null)
                _lightProbeGIShader = Shader.Find("hickv/LightProbeGI");

            _lightProbes = transform.GetComponentsInChildren<LightProbe>(false);

            if (bakeCubeMaps)
                for (int i = 0; i < _lightProbes.Length; i++)
                    yield return StartCoroutine(_lightProbes[i].RenderCubemapAsync());

            // create verts list
            _vertices = new Vector3[_lightProbes.Length];
            _shl2s = new LightProbe.SHL2[_lightProbes.Length];
            for (int i = 0; i < _lightProbes.Length; i++)
            {
                _vertices[i] = _lightProbes[i].transform.position;
                _shl2s[i] = _lightProbes[i].GenerateSHL2();
            }

            // Bake tetras
            // _tetrahedra = Delaunay.BowyerWatson3D(_vertices).ToArray();
            Delaunay3DJob delaunayJob = new Delaunay3DJob()
            {
                inVertices = new NativeArray<Vector3>(_vertices, Allocator.TempJob),
                outTetrahedra = new NativeList<Delaunay3DJob.Tetrahedron>(Allocator.TempJob),
            };
            JobHandle jobHandle = delaunayJob.Schedule();

            if (isAsync)
                while (!jobHandle.IsCompleted)
                    yield return null;

            jobHandle.Complete();

            _tetrahedra = delaunayJob.outTetrahedra.ToArray();

            delaunayJob.inVertices.Dispose();
            delaunayJob.outTetrahedra.Dispose();
        }

        IEnumerator BakeShaderDataAsync(bool isAsync)
        {
            _tetrahedraBoundingSpheres = new BoundingSphere[_tetrahedra.Length];
            if (_tetrahedraMaterials != null)
                for (int i = 0; i < _tetrahedraMaterials.Length; i++)
                    _tetrahedraMaterials[i]?.DestroySelf();
            _tetrahedraMaterials = new Material[_tetrahedra.Length];

            for (int i = 0; i < _tetrahedra.Length; i++)
            {
                Delaunay3DJob.Tetrahedron tetrahedron = _tetrahedra[i];

                float3 a = _vertices[tetrahedron._vIndex0];
                float3 b = _vertices[tetrahedron._vIndex1];
                float3 c = _vertices[tetrahedron._vIndex2];
                float3 d = _vertices[tetrahedron._vIndex3];

                float3 abc = math.cross(b - a, c - a);
                float3 abd = math.cross(b - a, d - a);
                float3 acd = math.cross(c - a, d - a);
                float3 bdc = math.cross(d - b, c - b);
                float totalVolume = math.abs(math.dot(a - d, abc) / 6);

                float3 pos = (float3)tetrahedron._circumCenter;
                float rad = (float)tetrahedron._circumRadius;
                _tetrahedraBoundingSpheres[i] = new BoundingSphere(new Vector3(pos.x, pos.y, pos.z), rad);

                Material tetramat = new Material(_lightProbeGIShader);
                _tetrahedraMaterials[i] = tetramat;

                LightProbe.SHL2 shl2a = _shl2s[tetrahedron._vIndex0];
                LightProbe.SHL2 shl2b = _shl2s[tetrahedron._vIndex1];
                LightProbe.SHL2 shl2c = _shl2s[tetrahedron._vIndex2];
                LightProbe.SHL2 shl2d = _shl2s[tetrahedron._vIndex3];

                // tetrahedron
                tetramat.SetVector("a", (Vector3)a);
                tetramat.SetVector("b", (Vector3)b);
                tetramat.SetVector("abc", (Vector3)abc);
                tetramat.SetVector("abd", (Vector3)abd);
                tetramat.SetVector("acd", (Vector3)acd);
                tetramat.SetVector("bdc", (Vector3)bdc);
                tetramat.SetFloat("totalVolume", totalVolume);

                // sha
                tetramat.SetVector("sha_y0", (Vector3)shl2a.y0);
                tetramat.SetVector("sha_y1", (Vector3)shl2a.y1);
                tetramat.SetVector("sha_y2", (Vector3)shl2a.y2);
                tetramat.SetVector("sha_y3", (Vector3)shl2a.y3);
                tetramat.SetVector("sha_y4", (Vector3)shl2a.y4);
                tetramat.SetVector("sha_y5", (Vector3)shl2a.y5);
                tetramat.SetVector("sha_y6", (Vector3)shl2a.y6);
                tetramat.SetVector("sha_y7", (Vector3)shl2a.y7);
                tetramat.SetVector("sha_y8", (Vector3)shl2a.y8);

                // shb
                tetramat.SetVector("shb_y0", (Vector3)shl2b.y0);
                tetramat.SetVector("shb_y1", (Vector3)shl2b.y1);
                tetramat.SetVector("shb_y2", (Vector3)shl2b.y2);
                tetramat.SetVector("shb_y3", (Vector3)shl2b.y3);
                tetramat.SetVector("shb_y4", (Vector3)shl2b.y4);
                tetramat.SetVector("shb_y5", (Vector3)shl2b.y5);
                tetramat.SetVector("shb_y6", (Vector3)shl2b.y6);
                tetramat.SetVector("shb_y7", (Vector3)shl2b.y7);
                tetramat.SetVector("shb_y8", (Vector3)shl2b.y8);

                // shc
                tetramat.SetVector("shc_y0", (Vector3)shl2c.y0);
                tetramat.SetVector("shc_y1", (Vector3)shl2c.y1);
                tetramat.SetVector("shc_y2", (Vector3)shl2c.y2);
                tetramat.SetVector("shc_y3", (Vector3)shl2c.y3);
                tetramat.SetVector("shc_y4", (Vector3)shl2c.y4);
                tetramat.SetVector("shc_y5", (Vector3)shl2c.y5);
                tetramat.SetVector("shc_y6", (Vector3)shl2c.y6);
                tetramat.SetVector("shc_y7", (Vector3)shl2c.y7);
                tetramat.SetVector("shc_y8", (Vector3)shl2c.y8);

                // shd
                tetramat.SetVector("shd_y0", (Vector3)shl2d.y0);
                tetramat.SetVector("shd_y1", (Vector3)shl2d.y1);
                tetramat.SetVector("shd_y2", (Vector3)shl2d.y2);
                tetramat.SetVector("shd_y3", (Vector3)shl2d.y3);
                tetramat.SetVector("shd_y4", (Vector3)shl2d.y4);
                tetramat.SetVector("shd_y5", (Vector3)shl2d.y5);
                tetramat.SetVector("shd_y6", (Vector3)shl2d.y6);
                tetramat.SetVector("shd_y7", (Vector3)shl2d.y7);
                tetramat.SetVector("shd_y8", (Vector3)shl2d.y8);

                if (isAsync)
                    yield return null;
            }

            if (_tetrahedraCullingGroup != null)
                _tetrahedraCullingGroup.Dispose();
            _tetrahedraCullingGroup = new CullingGroup();
            _tetrahedraCullingGroup.targetCamera = Camera.main;
            _tetrahedraCullingGroup.SetBoundingSphereCount(_tetrahedra.Length);
            _tetrahedraCullingGroup.SetBoundingSpheres(_tetrahedraBoundingSpheres);
        }

        void OnDestroy()
        {
            _tetrahedraCullingGroup?.Dispose();
            if (_tetrahedraMaterials != null)
                for (int i = 0; i < _tetrahedraMaterials.Length; i++)
                    _tetrahedraMaterials[i]?.DestroySelf();
        }

#if UNITY_EDITOR
        void Update()
        {
            if (_updateTetrahedra && !Application.isPlaying)
                StartCoroutine(BakeTetrahedraAsync(false, false));
        }
#endif

        void OnDrawGizmos()
        {
            if (_tetrahedra == null)
                return;

            Gizmos.color = Gizmos.color = Color.yellow;
            for (int i = 0; i < _tetrahedra.Length; i++)
            {
                Delaunay3DJob.Tetrahedron tetra = _tetrahedra[i];

                Gizmos.DrawLine(_vertices[tetra._vIndex0], _vertices[tetra._vIndex1]);
                Gizmos.DrawLine(_vertices[tetra._vIndex0], _vertices[tetra._vIndex2]);
                Gizmos.DrawLine(_vertices[tetra._vIndex0], _vertices[tetra._vIndex3]);
                Gizmos.DrawLine(_vertices[tetra._vIndex3], _vertices[tetra._vIndex1]);
                Gizmos.DrawLine(_vertices[tetra._vIndex3], _vertices[tetra._vIndex2]);
                Gizmos.DrawLine(_vertices[tetra._vIndex2], _vertices[tetra._vIndex1]);
            }
        }
    }
}
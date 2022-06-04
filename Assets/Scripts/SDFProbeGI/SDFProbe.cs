using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using Unity.Mathematics;
using System.Collections;
using System.Runtime.InteropServices;

namespace global_illumination
{
    [RequireComponent(typeof(BoxCollider))]
    [ExecuteInEditMode]
    public class SDFProbe : MonoBehaviour
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct SHL2
        {
            public float3 y0;
            public float3 y1;
            public float3 y2;
            public float3 y3;
            public float3 y4;
            public float3 y5;
            public float3 y6;
            public float3 y7;
            public float3 y8;
        }

        [System.Serializable]
        public enum SDFProbeType
        {
            Occlusion,
            Light,
        }

        [SerializeField] private Shader _sdfProbeShader;
        [SerializeField] private ComputeShader _shBakerShader;
        [SerializeField] private Shader _shPreviewShader;
        [SerializeField] private Mesh _shPreviewMesh;
        [SerializeField] private SDFProbeType _type;
        [SerializeField][Range(0, 2)] private float _intensity = 1f;
        [SerializeField][Range(0, 2)] private float _radius = 0f;
        [SerializeField][Range(0.01f, 5)] private float _radiust = 1f;
        [SerializeField] private BoxCollider _boxCollider;
        [SerializeField] private float3[] _shCoefficients;

        private Material _shPreviewMaterial;

        const int RESOLUTION = 128;

        public SDFProbeType Type { get => _type; }
        public float Intensity { get => _intensity; }
        public float Radius { get => _radius; }
        public float RadiusT { get => _radiust; }
        public BoxCollider BoxCollider { get => _boxCollider; }
        public float3[] SHCoefficients { get => _shCoefficients; }
        public Shader SDFProbeShader { get => _sdfProbeShader; }

#if UNITY_EDITOR
        void OnValidate()
        {
            _boxCollider = gameObject.TryGetAddComponent<BoxCollider>();
        }
#endif

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1, 1, 1, 0.1f);

            Gizmos.matrix = Matrix4x4.TRS(transform.TransformPoint(BoxCollider.center), transform.rotation, Vector3.one);
            Gizmos.DrawCube(Vector3.zero, BoxCollider.size);

            if (Type == SDFProbeType.Light)
                Gizmos.DrawSphere(Vector3.zero, GenerateBoundingSphereRadius());
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.clear;
            Gizmos.DrawSphere(transform.position, 0.067f);

            if (_shCoefficients == null || _shCoefficients.Length < 9)
                return;

            if (_shPreviewMaterial == null)
                _shPreviewMaterial = new Material(_shPreviewShader);

            _shPreviewMaterial.SetVector("_y0", new Vector4(_shCoefficients[0].x, _shCoefficients[0].y, _shCoefficients[0].z, 0));
            _shPreviewMaterial.SetVector("_y1", new Vector4(_shCoefficients[1].x, _shCoefficients[1].y, _shCoefficients[1].z, 0));
            _shPreviewMaterial.SetVector("_y2", new Vector4(_shCoefficients[2].x, _shCoefficients[2].y, _shCoefficients[2].z, 0));
            _shPreviewMaterial.SetVector("_y3", new Vector4(_shCoefficients[3].x, _shCoefficients[3].y, _shCoefficients[3].z, 0));
            _shPreviewMaterial.SetVector("_y4", new Vector4(_shCoefficients[4].x, _shCoefficients[4].y, _shCoefficients[4].z, 0));
            _shPreviewMaterial.SetVector("_y5", new Vector4(_shCoefficients[5].x, _shCoefficients[5].y, _shCoefficients[5].z, 0));
            _shPreviewMaterial.SetVector("_y6", new Vector4(_shCoefficients[6].x, _shCoefficients[6].y, _shCoefficients[6].z, 0));
            _shPreviewMaterial.SetVector("_y7", new Vector4(_shCoefficients[7].x, _shCoefficients[7].y, _shCoefficients[7].z, 0));
            _shPreviewMaterial.SetVector("_y8", new Vector4(_shCoefficients[8].x, _shCoefficients[8].y, _shCoefficients[8].z, 0));
            _shPreviewMaterial.SetPass(0);
            Matrix4x4 matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one * 0.1f);
            Graphics.DrawMeshNow(_shPreviewMesh, matrix, 0);
        }

        void OnDestroy()
        {
            if (_shPreviewMaterial != null)
                _shPreviewMaterial.DestroySelf();
        }

        public SHL2 GenerateSHL2()
        {
            return new SHL2()
            {
                y0 = _shCoefficients[0],
                y1 = _shCoefficients[1],
                y2 = _shCoefficients[2],
                y3 = _shCoefficients[3],
                y4 = _shCoefficients[4],
                y5 = _shCoefficients[5],
                y6 = _shCoefficients[6],
                y7 = _shCoefficients[7],
                y8 = _shCoefficients[8],
            };
        }

        public float GenerateBoundingSphereRadius()
        {
            Vector3 boxSize = BoxCollider.size / 2;
            float sphereRadius = math.max(math.max(boxSize.x, boxSize.y), boxSize.z) + 2 * Radius + RadiusT;
            return sphereRadius;
        }

        [NaughtyAttributes.Button]
        [ContextMenu("BakeCubemap")]
        public void BakeCubemap()
        {
            StartCoroutine(BakeCubemapAsync());
        }

        public IEnumerator BakeCubemapAsync()
        {
            Cubemap cubemap = new Cubemap(RESOLUTION, UnityEngine.Experimental.Rendering.DefaultFormat.HDR, UnityEngine.Experimental.Rendering.TextureCreationFlags.None);
            ComputeBuffer shBuffer = new ComputeBuffer(9, Marshal.SizeOf(typeof(float3)));

            // render cubemap using camera proxy object
            Camera camera = new GameObject("temp_camera", typeof(Camera)).GetComponent<Camera>();
            camera.allowHDR = true;
            camera.transform.position = transform.position;
            camera.transform.rotation = Quaternion.identity;
            camera.nearClipPlane = 0.001f;
            camera.farClipPlane = 1000;
            float oldIntensity = RenderSettings.ambientIntensity;
            RenderSettings.ambientIntensity = 0;
            camera.RenderToCubemap(cubemap);
            // for (int i = 0; i < 6; i++)
            // {
            //     camera.RenderToCubemap(cubemap, i);
            //     yield return null;
            // }
            RenderSettings.ambientIntensity = oldIntensity;
            camera.gameObject.DestroySelf();

            // bake cubemap to spherical harmonics
            int kernel = _shBakerShader.FindKernel("CSMain");
            _shBakerShader.SetBuffer(kernel, "_SHBuffer", shBuffer);
            _shBakerShader.SetTexture(kernel, "_CubeMap", cubemap);
            _shBakerShader.Dispatch(kernel, 9, 1, 1);

            // cache bake
            _shCoefficients = new float3[9];
            shBuffer.GetData(_shCoefficients);

            shBuffer.Dispose();
            cubemap.DestroySelf();

            yield return null;
        }
    }
}
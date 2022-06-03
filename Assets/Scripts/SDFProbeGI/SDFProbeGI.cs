using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Collections;
using System.Collections;
using System.Collections.Generic;

namespace global_illumination
{
    [ExecuteInEditMode]
    public class SDFProbeGI : MonoBehaviour
    {
        [System.Serializable]
        public class ProbesBuffer : System.IDisposable
        {
            [SerializeField] private List<SDFProbe> _probes;
            [SerializeField] private List<Material> _materials;
            [SerializeField] private CullingGroup _cullingGroup;
            [SerializeField] private List<BoundingSphere> _boundingSpheres;

            const float EPSILON = 0.01f;

            public List<SDFProbe> Probes { get => _probes; }
            public List<Material> Materials { get => _materials; }
            public CullingGroup CullingGroup { get => _cullingGroup; }
            public List<BoundingSphere> BoundingSpheres { get => _boundingSpheres; }

            public ProbesBuffer()
            {
                _probes = new List<SDFProbe>();
                _materials = new List<Material>();
                _boundingSpheres = new List<BoundingSphere>();
            }

            public void BakeCullingGroup(Camera camera)
            {
                _cullingGroup = new CullingGroup();
                _cullingGroup.targetCamera = camera;
                _cullingGroup.SetBoundingSpheres(_boundingSpheres.ToArray());
            }

            ~ProbesBuffer()
            {
                Dispose();
            }

            public void Add(SDFProbe newProbe, bool useSH)
            {
                Vector3 boxSize = Vector3.Lerp(newProbe.BoxCollider.size, newProbe.BoxCollider.size + Vector3.one * newProbe.Radius, newProbe.RadiusT) / 2;
                bool isSphere = (boxSize.x + boxSize.y + boxSize.z) <= EPSILON;
                Vector3 spherePosition = newProbe.transform.position + newProbe.BoxCollider.center;
                Matrix4x4 matrix = Matrix4x4.Translate(newProbe.BoxCollider.center).inverse * newProbe.transform.worldToLocalMatrix;
                if (isSphere)
                    matrix = Matrix4x4.Rotate(newProbe.transform.localRotation) * matrix;
                Material newMaterial = new Material(newProbe.SDFProbeShader);
                float sphereRadius = newProbe.GenerateBoundingSphereRadius();

                newMaterial.SetMatrix("_BoxMatrix", matrix);
                newMaterial.SetVector("_BoxSize", new Vector4(boxSize.x, boxSize.y, boxSize.z, 0));
                newMaterial.SetVector("_BoxRadius", new Vector4(newProbe.Radius, newProbe.RadiusT, 0, 0));
                newMaterial.SetFloat("_BoxIntensity", newProbe.Intensity);
                newMaterial.SetKeyword(new LocalKeyword(newMaterial.shader, "_IS_SPHERE"), isSphere);
                if (useSH)
                {
                    SDFProbe.SHL2 shl2 = newProbe.GenerateSHL2();
                    newMaterial.SetVector("_y0", (Vector3)shl2.y0);
                    newMaterial.SetVector("_y1", (Vector3)shl2.y1);
                    newMaterial.SetVector("_y2", (Vector3)shl2.y2);
                    newMaterial.SetVector("_y3", (Vector3)shl2.y3);
                    newMaterial.SetVector("_y4", (Vector3)shl2.y4);
                    newMaterial.SetVector("_y5", (Vector3)shl2.y5);
                    newMaterial.SetVector("_y6", (Vector3)shl2.y6);
                    newMaterial.SetVector("_y7", (Vector3)shl2.y7);
                    newMaterial.SetVector("_y8", (Vector3)shl2.y8);
                }

                _probes.Add(newProbe);
                _materials.Add(newMaterial);
                _boundingSpheres.Add(new BoundingSphere(spherePosition, sphereRadius));
            }

            public void Dispose()
            {
                _cullingGroup?.Dispose();
                if (_materials != null)
                    for (int i = 0; i < _materials.Count; i++)
                        _materials[i]?.DestroySelf();
            }
        }
        [SerializeField] private ProbesBuffer _lightProbesBuffer;
        [SerializeField] private ProbesBuffer _occlusionProbesBuffer;
        [SerializeField] private bool _update;

        public ProbesBuffer LightProbesBuffer { get => _lightProbesBuffer; }
        public ProbesBuffer OcclusionProbesBuffer { get => _occlusionProbesBuffer; }

        void Start()
        {
            // StartCoroutine(BakeAsync(true));
            Bake(true);
        }

        void OnDestroy()
        {
            _lightProbesBuffer?.Dispose();
            _occlusionProbesBuffer?.Dispose();
        }

        void Update()
        {
            if (_update)
                Bake(false);
        }

        [NaughtyAttributes.Button]
        public void Bake(bool bakeCubeMaps)
        {
            _lightProbesBuffer?.Dispose();
            _occlusionProbesBuffer?.Dispose();

            _lightProbesBuffer = new ProbesBuffer();
            _occlusionProbesBuffer = new ProbesBuffer();

            SDFProbe[] rawProbes = FindObjectsOfType<SDFProbe>();

            if (bakeCubeMaps)
                for (int i = 0; i < rawProbes.Length; i++)
                    StartCoroutine(rawProbes[i].BakeCubemapAsync());

            for (int i = 0; i < rawProbes.Length; i++)
            {
                SDFProbe probe = rawProbes[i];

                if (!probe.gameObject.activeSelf || !probe.enabled)
                    continue;

                if (probe.Type == SDFProbe.SDFProbeType.Light)
                    _lightProbesBuffer.Add(probe, true);
                else if (probe.Type == SDFProbe.SDFProbeType.Occlusion)
                    _occlusionProbesBuffer.Add(probe, false);
            }

            _lightProbesBuffer.BakeCullingGroup(Camera.main);
            _occlusionProbesBuffer.BakeCullingGroup(Camera.main);
        }
    }
}
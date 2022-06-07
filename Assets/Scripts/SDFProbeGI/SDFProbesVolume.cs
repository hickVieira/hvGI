#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace global_illumination
{
    [RequireComponent(typeof(BoxCollider))]
    [ExecuteInEditMode]
    public class SDFProbesVolume : MonoBehaviour
    {
        [Range(0, 10)] public float _size = 0.5f;
        [Range(0, 10)] public float _radius = 0.5f;
        [Range(0, 10)] public float _radiust = 2f;
        [Range(0, 4)] public float _probeDensity = 0.5f;
        [Range(1, 512)] public float _probeDensityX = 5;
        [Range(1, 512)] public float _probeDensityY = 5;
        [Range(1, 512)] public float _probeDensityZ = 5;
        public Vector3 _probeOffset = Vector3.one * 0.25f;
        public Vector3 _globalOffset = Vector3.zero;

        private BoxCollider _boxCollider;

#if UNITY_EDITOR
        void OnValidate()
        {
            _boxCollider = gameObject.TryGetAddComponent<BoxCollider>();
        }
#endif

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1, 1, 1, 0.1f);

            Gizmos.matrix = Matrix4x4.TRS(transform.TransformPoint(_boxCollider.center), transform.rotation, Vector3.one);
            Gizmos.DrawCube(Vector3.zero, _boxCollider.size);

            float xStep = _boxCollider.size.x / _probeDensityX;
            float yStep = _boxCollider.size.y / _probeDensityY;
            float zStep = _boxCollider.size.z / _probeDensityZ;

            if (xStep == 0 || yStep == 0 || zStep == 0)
                return;

            Gizmos.color = Color.magenta;

            float x = 0;
            while (x <= _boxCollider.size.x)
            {
                float y = 0;
                while (y <= _boxCollider.size.y)
                {
                    float z = 0;
                    while (z <= _boxCollider.size.z)
                    {
                        Gizmos.DrawSphere(
                                _globalOffset + Vector3.Scale(
                                    (new Vector3(x, y, z) - (_boxCollider.size / 2)),
                                        new Vector3((_boxCollider.size.x - _probeOffset.x) / _boxCollider.size.x,
                                                    (_boxCollider.size.y - _probeOffset.y) / _boxCollider.size.y,
                                                    (_boxCollider.size.z - _probeOffset.z) / _boxCollider.size.z)), 0.1f);
                        z += zStep;
                    }
                    y += yStep;
                }
                x += xStep;
            }
        }

        [NaughtyAttributes.Button()]
        public void AutoProbeDensity()
        {
            _probeDensityX = Mathf.Max(1, Mathf.RoundToInt(_boxCollider.size.x * _probeDensity));
            _probeDensityY = Mathf.Max(1, Mathf.RoundToInt(_boxCollider.size.y * _probeDensity));
            _probeDensityZ = Mathf.Max(1, Mathf.RoundToInt(_boxCollider.size.z * _probeDensity));
        }

        [NaughtyAttributes.Button()]
        public void CreateProbes()
        {
            // remove existing probes
            while (transform.childCount > 0)
                transform.GetChild(0).gameObject.DestroySelf();

            float xStep = _boxCollider.size.x / _probeDensityX;
            float yStep = _boxCollider.size.y / _probeDensityY;
            float zStep = _boxCollider.size.z / _probeDensityZ;

            if (xStep <= 0 || yStep <= 0 || zStep <= 0)
                return;

            float x = 0;
            while (x <= _boxCollider.size.x)
            {
                float y = 0;
                while (y <= _boxCollider.size.y)
                {
                    float z = 0;
                    while (z <= _boxCollider.size.z)
                    {
                        Vector3 pPos = new Vector3(x, y, z) - (_boxCollider.size / 2) + _boxCollider.center;
                        Vector3 pOffset = new Vector3((_boxCollider.size.x - _probeOffset.x) / _boxCollider.size.x, (_boxCollider.size.y - _probeOffset.y) / _boxCollider.size.y, (_boxCollider.size.z - _probeOffset.z) / _boxCollider.size.z);
                        Vector3 probePos = Vector3.Scale(pPos, pOffset);

                        SDFProbe newProbe = new GameObject($"probe({x}, {y}, {z})", typeof(SDFProbe)).GetComponent<SDFProbe>();
                        newProbe.transform.SetParent(transform, false);
                        newProbe.transform.localPosition = _globalOffset + probePos;
                        newProbe.Type = SDFProbe.SDFProbeType.Light;
                        newProbe.BoxCollider.size = new Vector3(xStep, yStep, zStep) * _size;
                        newProbe.Radius = Mathf.Max(Mathf.Max(xStep, yStep), zStep) * _radius;
                        newProbe.RadiusT = Mathf.Max(Mathf.Max(xStep, yStep), zStep) * _radiust;
                        z += zStep;
                    }
                    y += yStep;
                }
                x += xStep;
            }
        }

        [NaughtyAttributes.Button]
        void BakeProbes()
        {
            SDFProbe[] probes = transform.GetComponentsInChildren<SDFProbe>();
            for (int i = 0; i < probes.Length; i++)
                probes[i].BakeCubemap();
        }
    }
}
#endif
#if UNITY_EDITOR
using UnityEngine;

namespace GutEngine
{
    [ExecuteInEditMode]
    public class LightProbeProxy : MonoBehaviour
    {
        [SerializeField][Range(0.1f, 10f)] private float _distance = 2f;
        [SerializeField][Range(1, 10)] private int _count = 1;
        [SerializeField] private Vector3 _vector = Vector3.up;
        [SerializeField] private Vector3 _lightSampleLocalPosition;
        [SerializeField] private LightProbe[] _lightProbes;

        void Start()
        {
            Solve();
        }

        [NaughtyAttributes.Button]
        public void Solve()
        {
            if (_lightProbes == null || _lightProbes.Length != _count || transform.childCount != _count)
            {
                Purge();

                _lightProbes = new LightProbe[_count];

                Vector3 start = transform.position - _vector.normalized * _distance;
                Vector3 end = transform.position + _vector.normalized * _distance;

                for (int i = 0; i < _count; i++)
                {
                    int colorIndex = (_count - 1) - i;
                    LightProbe newProbe = new GameObject($"probe_{i}", typeof(LightProbe)).GetComponent<LightProbe>();
                    newProbe.transform.SetParent(transform, false);
                    newProbe.transform.position = _count == 1 ? Vector3.Lerp(start, end, 0.5f) : Vector3.Lerp(start, end, (float)i / (float)(_count - 1));
                    newProbe.LightSampleLocalPosition = newProbe.transform.InverseTransformPoint(transform.TransformPoint(_lightSampleLocalPosition));

                    _lightProbes[i] = newProbe;
                }
            }
            else
            {
                for (int i = 0; i < _count; i++)
                {
                    _lightProbes[i].LightSampleLocalPosition = _lightProbes[i].transform.InverseTransformPoint(transform.TransformPoint(_lightSampleLocalPosition));
                }

            }
        }

        void Purge()
        {
            while (transform.childCount > 0)
                transform.GetChild(0).gameObject.DestroySelf();
        }

        void Update()
        {
            if (transform.hasChanged)
                Solve();
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawCube(transform.position, new Vector3(0.1f, _distance * 2, 0.1f));
            Gizmos.DrawSphere(transform.TransformPoint(_lightSampleLocalPosition), 0.1f);
        }
    }
}
#endif
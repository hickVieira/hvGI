#if UNITY_EDITOR
using UnityEditor;
#endif
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace global_illumination
{
    public static partial class Extensions
    {

        public static T TryGetAddComponent<T>(this GameObject gameObject) where T : Component
        {
            T comp;
            if (gameObject.TryGetComponent<T>(out comp))
                return comp;
            return gameObject.AddComponent<T>();
        }

        public static void ResetLocal(this Transform transform)
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        public static void DestroySelf(this UnityEngine.Object obj, bool allowDestroyingAssets = false)
        {
            if (obj is Transform transform) obj = transform.gameObject;
#if UNITY_EDITOR
            UnityEngine.Object.DestroyImmediate(obj, allowDestroyingAssets);
#else
            UnityEngine.Object.Destroy(obj);
#endif
        }

        public static Vector3 CapsuleDirection(int dir, float height)
        {
            if (dir == 0)
                return new Vector3(height, 0, 0);
            else if (dir == 1)
                return new Vector3(0, height, 0);
            else
                return new Vector3(0, 0, height);
        }

        public static void DrawWireCapsule(Vector3 _pos, Quaternion _rot, float _radius, float _height, Color _color = default(Color))
        {
            if (_color != default(Color))
                UnityEditor.Handles.color = _color;
            Matrix4x4 angleMatrix = Matrix4x4.TRS(_pos, _rot, UnityEditor.Handles.matrix.lossyScale);
            using (new UnityEditor.Handles.DrawingScope(angleMatrix))
            {
                var pointOffset = (_height - (_radius * 2)) / 2;

                //draw sideways
                UnityEditor.Handles.DrawWireArc(Vector3.up * pointOffset, Vector3.left, Vector3.back, -180, _radius);
                UnityEditor.Handles.DrawLine(new Vector3(0, pointOffset, -_radius), new Vector3(0, -pointOffset, -_radius));
                UnityEditor.Handles.DrawLine(new Vector3(0, pointOffset, _radius), new Vector3(0, -pointOffset, _radius));
                UnityEditor.Handles.DrawWireArc(Vector3.down * pointOffset, Vector3.left, Vector3.back, 180, _radius);
                //draw frontways
                UnityEditor.Handles.DrawWireArc(Vector3.up * pointOffset, Vector3.back, Vector3.left, 180, _radius);
                UnityEditor.Handles.DrawLine(new Vector3(-_radius, pointOffset, 0), new Vector3(-_radius, -pointOffset, 0));
                UnityEditor.Handles.DrawLine(new Vector3(_radius, pointOffset, 0), new Vector3(_radius, -pointOffset, 0));
                UnityEditor.Handles.DrawWireArc(Vector3.down * pointOffset, Vector3.back, Vector3.left, -180, _radius);
                //draw center
                UnityEditor.Handles.DrawWireDisc(Vector3.up * pointOffset, Vector3.up, _radius);
                UnityEditor.Handles.DrawWireDisc(Vector3.down * pointOffset, Vector3.up, _radius);

            }
        }

        public static void DrawWireCapsule(Vector3 _pos, Vector3 _pos2, float _radius, Color _color = default)
        {
            if (_color != default) UnityEditor.Handles.color = _color;

            var forward = _pos2 - _pos;
            var _rot = Quaternion.LookRotation(forward);
            var pointOffset = _radius / 2f;
            var length = forward.magnitude;
            var center2 = new Vector3(0f, 0, length);

            Matrix4x4 angleMatrix = Matrix4x4.TRS(_pos, _rot, UnityEditor.Handles.matrix.lossyScale);

            using (new UnityEditor.Handles.DrawingScope(angleMatrix))
            {
                UnityEditor.Handles.DrawWireDisc(Vector3.zero, Vector3.forward, _radius);
                UnityEditor.Handles.DrawWireArc(Vector3.zero, Vector3.up, Vector3.left * pointOffset, -180f, _radius);
                UnityEditor.Handles.DrawWireArc(Vector3.zero, Vector3.left, Vector3.down * pointOffset, -180f, _radius);
                UnityEditor.Handles.DrawWireDisc(center2, Vector3.forward, _radius);
                UnityEditor.Handles.DrawWireArc(center2, Vector3.up, Vector3.right * pointOffset, -180f, _radius);
                UnityEditor.Handles.DrawWireArc(center2, Vector3.left, Vector3.up * pointOffset, -180f, _radius);

                DrawLine(_radius, 0f, length);
                DrawLine(-_radius, 0f, length);
                DrawLine(0f, _radius, length);
                DrawLine(0f, -_radius, length);
            }
        }

        public static void DrawWireCone(Vector3 _pos, Vector3 _pos2, float _radius0, float _radius1, Color _color = default)
        {
            if (_color != default) UnityEditor.Handles.color = _color;

            var forward = _pos2 - _pos;
            var _rot = Quaternion.LookRotation(forward);
            var length = forward.magnitude;
            var center2 = new Vector3(0f, 0, length);

            Matrix4x4 angleMatrix = Matrix4x4.TRS(_pos, _rot, UnityEditor.Handles.matrix.lossyScale);

            using (new UnityEditor.Handles.DrawingScope(angleMatrix))
            {
                UnityEditor.Handles.DrawWireDisc(Vector3.zero, Vector3.forward, _radius0);
                UnityEditor.Handles.DrawWireDisc(center2, Vector3.forward, _radius1);

                UnityEditor.Handles.DrawLine(new Vector3(_radius0, 0, 0), new Vector3(_radius1, 0, length));
                UnityEditor.Handles.DrawLine(new Vector3(-_radius0, 0, 0), new Vector3(-_radius1, 0, length));
                UnityEditor.Handles.DrawLine(new Vector3(0, _radius0, 0), new Vector3(0, _radius1, length));
                UnityEditor.Handles.DrawLine(new Vector3(0, -_radius0, 0), new Vector3(0, -_radius1, length));

                // DrawLine(_radius0, _radius1, length);
                // DrawLine(-_radius0, -_radius1, length);
                // DrawLine(_radius0, _radius1, length);
                // DrawLine(-_radius0, -_radius1, length);
            }
        }

        private static void DrawLine(float arg1, float arg2, float forward)
        {
            UnityEditor.Handles.DrawLine(new Vector3(arg1, arg2, 0f), new Vector3(arg1, arg2, forward));
        }
    }
}
using System;
using UnityEngine;

using Unity.Mathematics;
using static Unity.Mathematics.math;

#if UNITY_EDITOR
using UnityEditor;
#endif


namespace IzPhysBone.Collider {

	/** IzBone専用のコライダー */
	[AddComponentMenu("IzBone/IzCollider")]
	public sealed class IzCollider : MonoBehaviour {
		// ------------------------------- inspectorに公開しているフィールド ------------------------

		public enum Mode { Sphere, Capsule, Box, Plane, }
		[SerializeField] Mode _mode = Mode.Sphere;
		[SerializeField] float3 _center = float3(0,0,0);
		[SerializeField] float3 _r = float3(1,1,1);
		[SerializeField] quaternion _rot = Unity.Mathematics.quaternion.identity;


		// --------------------------------------- publicメンバ -------------------------------------

		public Mode mode => _mode;
		public float3 center => _center;
		public float3 r => _r;
		public quaternion rot => _rot;

		new public Transform transform {get{
			if (_transform == null) _transform = ((MonoBehaviour)this).transform;
			return _transform;
		}}

		// 高速化のために、実機ではフィールドアクセスに変化させる
	#if UNITY_EDITOR
		public float4x4 l2gMat {get; private set;}
		public float3 l2gMatClmNorm {get; private set;}
	#else
		[NonSerialized] public float4x4 l2gMat;
		[NonSerialized] public float3 l2gMatClmNorm;
	#endif

		/** 更新処理。l2gMat等を呼ぶ前に必ずこれを読んで更新すること */
		public void update_phase0() { checkRebuildL2GMat(); }
		public void update_phase1() { _transform.hasChanged = false; }


		// ----------------------------------- private/protected メンバ -------------------------------

		Transform _transform;
		float3 _ctrCache = default;
		quaternion _rotCache = Unity.Mathematics.quaternion.identity;

		void checkRebuildL2GMat() {
			var trans = transform;
			if (_mode == Mode.Sphere) {
				if (!trans.hasChanged && _ctrCache.Equals(_center)) return;
				var tr = Unity.Mathematics.float4x4.identity;
				tr.c3.xyz = _center;
				l2gMat = mul(trans.localToWorldMatrix, tr);
			} else {
				if (!trans.hasChanged && _ctrCache.Equals(_center) && _rotCache.Equals(_rot)) return;
				var tr = float4x4(_rot, _center);
				l2gMat = mul(trans.localToWorldMatrix, tr);
				_rotCache = _rot;
			}
			_ctrCache = _center;

			l2gMatClmNorm = float3(
				length( l2gMat.c0.xyz ),
				length( l2gMat.c1.xyz ),
				length( l2gMat.c2.xyz )
			);
		}


		// --------------------------------------------------------------------------------------------
#if UNITY_EDITOR
		void OnDrawGizmos() {
			if ( !Selection.Contains( gameObject.GetInstanceID() ) ) return;
			DEBUG_drawGizmos();
		}

		public void DEBUG_drawGizmos() {
			if ( !Application.isPlaying ) checkRebuildL2GMat();
			Gizmos.color = new Color(0.3f,1f,0.2f);

			if (_mode == Mode.Sphere) {
				var pos = l2gMat.c3.xyz;
				var size = mul( (float3x3)l2gMat, (float3)(_r.x / Mathf.Sqrt(3)) );
				Gizmos.DrawWireSphere( pos, length(size) );

			} else if (_mode == Mode.Capsule) {
				var pos = l2gMat.c3.xyz;
				var l2gMat3x3 = (float3x3)l2gMat;
				var sizeX  = mul( l2gMat3x3, float3(_r.x,0,0) );
				var sizeY0 = mul( l2gMat3x3, float3(0,_r.y-_r.x,0) );
				var sizeY1 = mul( l2gMat3x3, float3(0,_r.x,0) );
				var sizeZ  = mul( l2gMat3x3, float3(0,0,_r.x) );
				float3 p0 = default;
				float3 p1 = default;
				for (int i=0; i<=30; ++i) {
					var theta = (float)i/30 * (Mathf.PI*2);
					var c = Mathf.Cos(theta);
					var s = Mathf.Sin(theta);
					var pN0 = pos + sizeY0 + c*sizeX + s*sizeZ;
					var pN1 = pos - sizeY0 + c*sizeX + s*sizeZ;
					if (i!=0) {
						Gizmos.DrawLine( p0, pN0 );
						Gizmos.DrawLine( p1, pN1 );
					}
					p0 = pN0;
					p1 = pN1;
				}
				for (int i=0; i<=15; ++i) {
					var theta = (float)i/15 * Mathf.PI;
					var c = Mathf.Cos(theta);
					var s = Mathf.Sin(theta);
					var pN0 = c*sizeX + s*sizeY1;
					var pN1 = c*sizeZ + s*sizeY1;
					if (i!=0) {
						Gizmos.DrawLine(pos + sizeY0+p0, pos + sizeY0+pN0 );
						Gizmos.DrawLine(pos + sizeY0+p1, pos + sizeY0+pN1 );
						Gizmos.DrawLine(pos - sizeY0-p0, pos - sizeY0-pN0 );
						Gizmos.DrawLine(pos - sizeY0-p1, pos - sizeY0-pN1 );
					}
					p0 = pN0;
					p1 = pN1;
				}
				Gizmos.DrawLine( pos +sizeY0+sizeX, pos -sizeY0+sizeX );
				Gizmos.DrawLine( pos +sizeY0-sizeX, pos -sizeY0-sizeX );
				Gizmos.DrawLine( pos +sizeY0+sizeZ, pos -sizeY0+sizeZ );
				Gizmos.DrawLine( pos +sizeY0-sizeZ, pos -sizeY0-sizeZ );

			} else if (_mode == Mode.Box) {
				var pos = l2gMat.c3.xyz;
				var l2gMat3x3 = (float3x3)l2gMat;
				var sizeX = mul( l2gMat3x3, float3(_r.x,0,0) );
				var sizeY = mul( l2gMat3x3, float3(0,_r.y,0) );
				var sizeZ = mul( l2gMat3x3, float3(0,0,_r.z) );
				var ppp =  sizeX +sizeY +sizeZ + pos;
				var ppm =  sizeX +sizeY -sizeZ + pos;
				var pmp =  sizeX -sizeY +sizeZ + pos;
				var pmm =  sizeX -sizeY -sizeZ + pos;
				var mpp = -sizeX +sizeY +sizeZ + pos;
				var mpm = -sizeX +sizeY -sizeZ + pos;
				var mmp = -sizeX -sizeY +sizeZ + pos;
				var mmm = -sizeX -sizeY -sizeZ + pos;
				Gizmos.DrawLine( ppp, ppm );
				Gizmos.DrawLine( pmp, pmm );
				Gizmos.DrawLine( mpp, mpm );
				Gizmos.DrawLine( mmp, mmm );
				Gizmos.DrawLine( ppp, pmp );
				Gizmos.DrawLine( ppm, pmm );
				Gizmos.DrawLine( mpp, mmp );
				Gizmos.DrawLine( mpm, mmm );
				Gizmos.DrawLine( ppp, mpp );
				Gizmos.DrawLine( ppm, mpm );
				Gizmos.DrawLine( pmp, mmp );
				Gizmos.DrawLine( pmm, mmm );

			} else if (_mode == Mode.Plane) {
				var pos = l2gMat.c3.xyz;
				var l2gMat3x3 = (float3x3)l2gMat;
				var x = mul( l2gMat3x3, float3(0.05f,0,0) );
				var y = mul( l2gMat3x3, float3(0,0.05f,0) );
				var z = mul( l2gMat3x3, float3(0,0,0.02f) );
				Gizmos.DrawLine( pos-x, pos+x );
				Gizmos.DrawLine( pos-y, pos+y );
				Gizmos.DrawLine( pos, pos-z );

			} else { throw new InvalidProgramException(); }
		}

		[CustomEditor(typeof(IzCollider))]
		sealed class CustomInspector : Editor {
			public override void OnInspectorGUI() {
				var tgt = target as IzCollider;
				if (tgt == null) return;

				serializedObject.Update();

				// 形状タイプ
				var sfMode = serializedObject.FindProperty( "_mode" );
				EditorGUILayout.PropertyField( sfMode );

				// 中心位置オフセット
				var sfCenter = serializedObject.FindProperty( "_center" );
				EditorGUILayout.PropertyField( sfCenter );

				// 半径
				var sfR = serializedObject.FindProperty( "_r" );
				if (tgt._mode == Mode.Sphere) {
					using (var check = new EditorGUI.ChangeCheckScope()) {
						var r = EditorGUILayout.FloatField( "Radius", tgt._r.x );
						if (check.changed)
							setPropFloat3( sfR, float3(r, tgt._r.y, tgt._r.z) );
					}
				} else if (tgt._mode == Mode.Capsule) {
					using (var check = new EditorGUI.ChangeCheckScope()) {
						var r = EditorGUILayout.FloatField( "Radius", tgt._r.x );
						var h = EditorGUILayout.FloatField( "Height", tgt._r.y*2 );
						if (check.changed)
							setPropFloat3( sfR, float3(r, h/2, tgt._r.z) );
					}
				} else if (tgt._mode == Mode.Box) {
					using (var check = new EditorGUI.ChangeCheckScope()) {
						var r = EditorGUILayout.Vector3Field( "Size", tgt._r*2 );
						if (check.changed)
							setPropFloat3( sfR, r/2 );
					}
				} else if (tgt._mode == Mode.Plane) {
				} else { throw new InvalidProgramException(); }

				// 回転
				if (tgt._mode != Mode.Sphere) {
					var sfRot = serializedObject.FindProperty( "_rot" );
					using (var check = new EditorGUI.ChangeCheckScope()) {
						
						var euler = ((Quaternion)tgt._rot).eulerAngles;
						euler = EditorGUILayout.Vector3Field("Rotation", euler);
						if (check.changed)
							setPropQuaternion( sfRot, Quaternion.Euler(euler) );
					}
				}

				serializedObject.ApplyModifiedProperties();
			}

			void setPropFloat3(SerializedProperty prop, float3 v) {
				prop.FindPropertyRelative("x").floatValue = v.x;
				prop.FindPropertyRelative("y").floatValue = v.y;
				prop.FindPropertyRelative("z").floatValue = v.z;
			}
			void setPropFloat4(SerializedProperty prop, float4 v) {
				setPropFloat3(prop, v.xyz);
				prop.FindPropertyRelative("w").floatValue = v.w;
			}
			void setPropQuaternion(SerializedProperty prop, quaternion q) {
				setPropFloat4(prop.FindPropertyRelative("value"), q.value);
			}
		}
#endif
	}

}


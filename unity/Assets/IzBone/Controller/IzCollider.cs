using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif


namespace IzBone.Controller {

	/** IzBone専用のコライダー */
	[AddComponentMenu("IzBone/IzCollider")]
	public sealed class IzCollider : MonoBehaviour {
		// ------------------------------- inspectorに公開しているフィールド ------------------------

		public enum Mode { Sphere, Capsule, Box, Plane, }
		[SerializeField] Mode _mode = Mode.Sphere;
		[SerializeField] Vector3 _center = new Vector3(0,0,0);
		[SerializeField] Vector3 _r = new Vector3(1,1,1);
		[SerializeField] Quaternion _rot = Quaternion.identity;


		// --------------------------------------- publicメンバ -------------------------------------

		public Mode mode => _mode;
		public Vector3 center => _center;
		public Vector3 r => _r;
		public Quaternion rot => _rot;

		new public Transform transform {get{
			if (_transform == null) _transform = ((MonoBehaviour)this).transform;
			return _transform;
		}}

		// 高速化のために、実機ではフィールドアクセスに変化させる
	#if UNITY_EDITOR
		public Matrix4x4 l2gMat {get; private set;}
		public Vector3 l2gMatClmNorm {get; private set;}
	#else
		public Matrix4x4 l2gMat;
		public Vector3 l2gMatClmNorm;
	#endif

		/** 更新処理。l2gMat等を呼ぶ前に必ずこれを読んで更新すること */
		public void update_phase0() { checkRebuildL2GMat(); }
		public void update_phase1() { _transform.hasChanged = false; }


		// ----------------------------------- private/protected メンバ -------------------------------

		Transform _transform;
		Vector3 _ctrCache = new Vector3(0,0,0);
		Quaternion _rotCache = Quaternion.identity;

		void checkRebuildL2GMat() {
			var trans = transform;
			if (_mode == Mode.Sphere) {
				if (!trans.hasChanged && _ctrCache==_center) return;
				var tr = Matrix4x4.identity;
				tr.m03 = _center.x;
				tr.m13 = _center.y;
				tr.m23 = _center.z;
				l2gMat = trans.localToWorldMatrix * tr;
			} else {
				if (!trans.hasChanged && _ctrCache==_center && _rotCache==_rot) return;
				var tr = Matrix4x4.Rotate(_rot);
				tr.m03 = _center.x;
				tr.m13 = _center.y;
				tr.m23 = _center.z;
				l2gMat = trans.localToWorldMatrix * tr;
				_rotCache = _rot;
			}
			_ctrCache = _center;

			l2gMatClmNorm = new Vector3(
				new Vector3(l2gMat.m00, l2gMat.m10, l2gMat.m20).magnitude,
				new Vector3(l2gMat.m01, l2gMat.m11, l2gMat.m21).magnitude,
				new Vector3(l2gMat.m02, l2gMat.m12, l2gMat.m22).magnitude
			);
		}


#if UNITY_EDITOR
		void OnDrawGizmos() {
			if ( !UnityEditor.Selection.Contains( gameObject.GetInstanceID() ) ) return;
			DEBUG_drawGizmos();
		}

		public void DEBUG_drawGizmos() {
			if ( !Application.isPlaying ) checkRebuildL2GMat();
			Gizmos.color = new Color(0.3f,1f,0.2f);

			if (_mode == Mode.Sphere) {
				var pos = new Vector3(l2gMat.m03,l2gMat.m13,l2gMat.m23);
				var size = l2gMat.MultiplyVector( Vector3.one * (_r.x / Mathf.Sqrt(3)) );
				Gizmos.DrawWireSphere( pos, size.magnitude );

			} else if (_mode == Mode.Capsule) {
				var pos = new Vector3(l2gMat.m03,l2gMat.m13,l2gMat.m23);
				var sizeX = l2gMat.MultiplyVector(new Vector3(_r.x,0,0));
				var sizeY0 = l2gMat.MultiplyVector(new Vector3(0,_r.y-_r.x,0));
				var sizeY1 = l2gMat.MultiplyVector(new Vector3(0,_r.x,0));
				var sizeZ = l2gMat.MultiplyVector(new Vector3(0,0,_r.x));
				Vector3 p0 = default(Vector3);
				Vector3 p1 = default(Vector3);
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
				var pos = new Vector3(l2gMat.m03,l2gMat.m13,l2gMat.m23);
				var sizeX = l2gMat.MultiplyVector(new Vector3(_r.x,0,0));
				var sizeY = l2gMat.MultiplyVector(new Vector3(0,_r.y,0));
				var sizeZ = l2gMat.MultiplyVector(new Vector3(0,0,_r.z));
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
				var pos = new Vector3(l2gMat.m03,l2gMat.m13,l2gMat.m23);
				var x = l2gMat.MultiplyVector(new Vector3(0.05f,0,0));
				var y = l2gMat.MultiplyVector(new Vector3(0,0.05f,0));
				var z = l2gMat.MultiplyVector(new Vector3(0,0,0.02f));
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
						if (check.changed) sfR.vector3Value = new Vector3(r, tgt._r.y, tgt._r.z);
					}
				} else if (tgt._mode == Mode.Capsule) {
					using (var check = new EditorGUI.ChangeCheckScope()) {
						var r = EditorGUILayout.FloatField( "Radius", tgt._r.x );
						var h = EditorGUILayout.FloatField( "Height", tgt._r.y*2 );
						if (check.changed) sfR.vector3Value = new Vector3(r, h/2, tgt._r.z);
					}
				} else if (tgt._mode == Mode.Box) {
					using (var check = new EditorGUI.ChangeCheckScope()) {
						var r = EditorGUILayout.Vector3Field( "Size", tgt._r*2 );
						if (check.changed) sfR.vector3Value = r/2;
					}
				} else if (tgt._mode == Mode.Plane) {
				} else { throw new InvalidProgramException(); }

				// 回転
				if (tgt._mode != Mode.Sphere) {
					var sfRot = serializedObject.FindProperty( "_rot" );
					using (var check = new EditorGUI.ChangeCheckScope()) {
						var euler = tgt._rot.eulerAngles;
						euler = EditorGUILayout.Vector3Field("Rotation", euler);
						if (check.changed) sfRot.quaternionValue = Quaternion.Euler(euler);
					}
				}

				serializedObject.ApplyModifiedProperties();
			}
		}
#endif

		// --------------------------------------------------------------------------------------------
	}

}


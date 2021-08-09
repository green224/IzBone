using System;
using UnityEngine;

using Unity.Mathematics;
using static Unity.Mathematics.math;

#if UNITY_EDITOR
using UnityEditor;
#endif


namespace IzBone.Common.Collider {

	/** IzBone専用のコライダー */
	[AddComponentMenu("IzBone/IzBone_Collider")]
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
		internal void DEBUG_drawGizmos() {
			if ( !Application.isPlaying ) checkRebuildL2GMat();
			Gizmos8.drawMode = Gizmos8.DrawMode.Handle;

			Gizmos8.color = Gizmos8.Colors.Collider;

			if (_mode == Mode.Sphere) {
				var pos = l2gMat.c3.xyz;
				var size = mul( (float3x3)l2gMat, (float3)(_r.x / Mathf.Sqrt(3)) );
				Gizmos8.drawWireSphere( pos, length(size) );

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
						Gizmos8.drawLine( p0, pN0 );
						Gizmos8.drawLine( p1, pN1 );
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
						Gizmos8.drawLine(pos + sizeY0+p0, pos + sizeY0+pN0 );
						Gizmos8.drawLine(pos + sizeY0+p1, pos + sizeY0+pN1 );
						Gizmos8.drawLine(pos - sizeY0-p0, pos - sizeY0-pN0 );
						Gizmos8.drawLine(pos - sizeY0-p1, pos - sizeY0-pN1 );
					}
					p0 = pN0;
					p1 = pN1;
				}
				Gizmos8.drawLine( pos +sizeY0+sizeX, pos -sizeY0+sizeX );
				Gizmos8.drawLine( pos +sizeY0-sizeX, pos -sizeY0-sizeX );
				Gizmos8.drawLine( pos +sizeY0+sizeZ, pos -sizeY0+sizeZ );
				Gizmos8.drawLine( pos +sizeY0-sizeZ, pos -sizeY0-sizeZ );

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
				Gizmos8.drawLine( ppp, ppm );
				Gizmos8.drawLine( pmp, pmm );
				Gizmos8.drawLine( mpp, mpm );
				Gizmos8.drawLine( mmp, mmm );
				Gizmos8.drawLine( ppp, pmp );
				Gizmos8.drawLine( ppm, pmm );
				Gizmos8.drawLine( mpp, mmp );
				Gizmos8.drawLine( mpm, mmm );
				Gizmos8.drawLine( ppp, mpp );
				Gizmos8.drawLine( ppm, mpm );
				Gizmos8.drawLine( pmp, mmp );
				Gizmos8.drawLine( pmm, mmm );

			} else if (_mode == Mode.Plane) {
				var pos = l2gMat.c3.xyz;
				var l2gMat3x3 = (float3x3)l2gMat;
				var x = mul( l2gMat3x3, float3(0.05f,0,0) );
				var y = mul( l2gMat3x3, float3(0,0.05f,0) );
				var z = mul( l2gMat3x3, float3(0,0,0.02f) );
				Gizmos8.drawLine( pos-x, pos+x );
				Gizmos8.drawLine( pos-y, pos+y );
				Gizmos8.drawLine( pos, pos-z );

			} else { throw new InvalidProgramException(); }
		}
#endif
	}

}


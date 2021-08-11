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
		// --------------------------------------- publicメンバ -------------------------------------

		public enum Mode { Sphere, Capsule, Box, Plane, }

		[UnityEngine.Serialization.FormerlySerializedAs("_mode")]
		public Mode mode = Mode.Sphere;
		[UnityEngine.Serialization.FormerlySerializedAs("_center")]
		public float3 center = float3(0,0,0);
		[UnityEngine.Serialization.FormerlySerializedAs("_r")]
		public float3 r = float3(1,1,1);
		[UnityEngine.Serialization.FormerlySerializedAs("_rot")]
		public quaternion rot = Unity.Mathematics.quaternion.identity;
		public bool forcePeneCancel = false;		// 中央に仮想板を配置して、パーティクルが絶対に初期位置から見て逆側に浸透しないようにする


		new public Transform transform {get{
			if (_transform == null) _transform = ((MonoBehaviour)this).transform;
			return _transform;
		}}

		// 高速化のために、実機ではフィールドアクセスに変化させる
	#if UNITY_EDITOR
		public float4x4 l2wMtx {get; private set;}
		public float3 l2wMtxClmNorm {get; private set;}
	#else
		[NonSerialized] public float4x4 l2wMtx;
		[NonSerialized] public float3 l2wMtxClmNorm;
	#endif

		/** 更新処理。l2gMtx等を呼ぶ前に必ずこれを読んで更新すること */
		public void update_phase0() { checkRebuildL2GMat(); }
		public void update_phase1() { _transform.hasChanged = false; }


		// ----------------------------------- private/protected メンバ -------------------------------

		Transform _transform;
		float3 _ctrCache = default;
		quaternion rotCache = Unity.Mathematics.quaternion.identity;

		void checkRebuildL2GMat() {
			var trans = transform;
			if (mode == Mode.Sphere) {
				if (!trans.hasChanged && _ctrCache.Equals(center)) return;
				var tr = Unity.Mathematics.float4x4.identity;
				tr.c3.xyz = center;
				l2wMtx = mul(trans.localToWorldMatrix, tr);
			} else {
				if (!trans.hasChanged && _ctrCache.Equals(center) && rotCache.Equals(rot)) return;
				var tr = float4x4(rot, center);
				l2wMtx = mul(trans.localToWorldMatrix, tr);
				rotCache = rot;
			}
			_ctrCache = center;

			l2wMtxClmNorm = float3(
				length( l2wMtx.c0.xyz ),
				length( l2wMtx.c1.xyz ),
				length( l2wMtx.c2.xyz )
			);
		}


		// --------------------------------------------------------------------------------------------
#if UNITY_EDITOR
		internal void DEBUG_drawGizmos() {
			if ( !Application.isPlaying ) checkRebuildL2GMat();
			Gizmos8.drawMode = Gizmos8.DrawMode.Handle;

			Gizmos8.color = Gizmos8.Colors.Collider;

			if (mode == Mode.Sphere) {
				var pos = l2wMtx.c3.xyz;
				var size = mul( (float3x3)l2wMtx, (float3)(r.x / Mathf.Sqrt(3)) );
				Gizmos8.drawWireSphere( pos, length(size) );

			} else if (mode == Mode.Capsule) {
				var pos = l2wMtx.c3.xyz;
				var l2gMat3x3 = (float3x3)l2wMtx;
				var sizeX  = mul( l2gMat3x3, float3(r.x,0,0) );
				var sizeY0 = mul( l2gMat3x3, float3(0,r.y-r.x,0) );
				var sizeY1 = mul( l2gMat3x3, float3(0,r.x,0) );
				var sizeZ  = mul( l2gMat3x3, float3(0,0,r.x) );
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

			} else if (mode == Mode.Box) {
				var pos = l2wMtx.c3.xyz;
				var l2gMat3x3 = (float3x3)l2wMtx;
				var sizeX = mul( l2gMat3x3, float3(r.x,0,0) );
				var sizeY = mul( l2gMat3x3, float3(0,r.y,0) );
				var sizeZ = mul( l2gMat3x3, float3(0,0,r.z) );
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

			} else if (mode == Mode.Plane) {
				var pos = l2wMtx.c3.xyz;
				var l2gMat3x3 = (float3x3)l2wMtx;
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

